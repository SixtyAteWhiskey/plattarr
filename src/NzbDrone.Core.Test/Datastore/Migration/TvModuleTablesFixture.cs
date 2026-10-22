using System;
using System.Linq;
using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    // Migration 004 upgrades a database that already holds movie activity rows. The rows are the
    // shape migration 003 leaves: ParentId, no ItemId, ItemIds or ReleaseType.
    [TestFixture]
    public class TvModuleTablesFixture : MigrationTest<tv_module_tables>
    {
        private static readonly string[] TvTables =
        {
            "Tv_Series",
            "Tv_SeriesTranslations",
            "Tv_Episodes",
            "Tv_EpisodeFiles",
            "Tv_SceneMappings",
            "Tv_RootFolders",
            "Tv_NamingConfig"
        };

        private IDirectDataMapper GivenMovieActivityRows()
        {
            return WithMigrationTestDb(m =>
            {
                m.Insert.IntoTable("History").Row(new
                {
                    MediaKind = 1,
                    SourceTitle = "Movie.2024.1080p",
                    Date = DateTime.UtcNow,
                    Quality = "{}",
                    Data = "{}",
                    EventType = 1,
                    DownloadId = "history-download",
                    ParentId = 7,
                    Languages = "[]"
                });

                m.Insert.IntoTable("Blocklist").Row(new
                {
                    MediaKind = 1,
                    SourceTitle = "Movie.2024.1080p",
                    Quality = "{}",
                    Date = DateTime.UtcNow,
                    ParentId = 9,
                    Languages = "[]",
                    IndexerFlags = 4
                });
            });
        }

        private static void InsertSeries(System.Data.IDbConnection conn, int tvdbId, string slug = null)
        {
            conn.Execute(
                "INSERT INTO \"Tv_Series\" (\"TvdbId\", \"Title\", \"Status\", \"Monitored\", \"QualityProfileId\", \"SeasonFolder\", \"Path\", \"Added\", \"TitleSlug\") " +
                "VALUES (@tvdbId, 'Series', 0, @monitored, 1, @seasonFolder, '/data/tv/Series', @added, @slug)",
                new { tvdbId, monitored = true, seasonFolder = true, added = DateTime.UtcNow, slug });
        }

        [Test]
        public void should_keep_a_history_row_and_default_its_item_to_zero()
        {
            var db = GivenMovieActivityRows();

            var row = db.Query("SELECT \"MediaKind\", \"ParentId\", \"ItemId\", \"SourceTitle\", \"DownloadId\" FROM \"History\"").Single();

            Convert.ToInt32(row["MediaKind"]).Should().Be(1);
            Convert.ToInt32(row["ParentId"]).Should().Be(7);
            Convert.ToInt32(row["ItemId"]).Should().Be(0);
            row["SourceTitle"].Should().Be("Movie.2024.1080p");
            row["DownloadId"].Should().Be("history-download");
        }

        [Test]
        public void should_keep_a_blocklist_row_and_default_the_new_columns()
        {
            var db = GivenMovieActivityRows();

            var row = db.Query("SELECT \"MediaKind\", \"ParentId\", \"ItemIds\", \"ReleaseType\", \"IndexerFlags\", \"SourceTitle\" FROM \"Blocklist\"").Single();

            Convert.ToInt32(row["MediaKind"]).Should().Be(1);
            Convert.ToInt32(row["ParentId"]).Should().Be(9);
            Convert.ToInt32(row["IndexerFlags"]).Should().Be(4);
            row["ItemIds"].Should().Be("[]");
            Convert.ToInt32(row["ReleaseType"]).Should().Be(0);
            row["SourceTitle"].Should().Be("Movie.2024.1080p");
        }

        [Test]
        public void should_index_history_by_kind_item_and_date()
        {
            var db = GivenMovieActivityRows();

            var sql = Mocker.Resolve<IMainDatabase>().DatabaseType == DatabaseType.PostgreSQL
                ? "SELECT indexname AS name, indexdef AS definition FROM pg_indexes WHERE schemaname = current_schema()"
                : "SELECT name, sql AS definition FROM sqlite_master WHERE type = 'index' AND sql IS NOT NULL";

            var indexes = db.Query(sql).ToDictionary(r => (string)r["name"], r => (string)r["definition"]);

            indexes.Keys.Should().Contain("IX_History_MediaKind_ItemId_Date");
            indexes["IX_History_MediaKind_ItemId_Date"].Should().Contain("MediaKind").And.Contain("ItemId").And.Contain("Date");
            indexes.Keys.Should().Contain("IX_History_MediaKind_ParentId_Date", "migration 002's index stays");
        }

        [Test]
        public void should_create_every_tv_table_empty()
        {
            var db = WithMigrationTestDb();

            foreach (var table in TvTables)
            {
                db.Query($"SELECT \"Id\" FROM \"{table}\"").Should().BeEmpty(table);
            }
        }

        [Test]
        public void should_default_a_series_to_english_official_seasons_and_no_tags()
        {
            using var conn = WithDapperMigrationTestDb();

            InsertSeries(conn, 81189, "breaking-bad");

            var row = conn.QuerySingle("SELECT \"Language\", \"OriginalLanguage\", \"SeasonType\", \"SeasonTypes\", \"Tags\", \"Seasons\", \"Images\", \"MalIds\" FROM \"Tv_Series\"");

            ((int)row.Language).Should().Be(14);
            ((int)row.OriginalLanguage).Should().Be(14);
            ((string)row.SeasonType).Should().Be("official");
            ((string)row.SeasonTypes).Should().Be("[]");
            ((string)row.Tags).Should().Be("[]");
            ((string)row.Seasons).Should().Be("[]");
            ((string)row.Images).Should().Be("[]");
            ((string)row.MalIds).Should().Be("[]");
        }

        [Test]
        public void should_refuse_two_series_with_the_same_tvdb_id()
        {
            using var conn = WithDapperMigrationTestDb();

            InsertSeries(conn, 81189, "breaking-bad");

            Action act = () => InsertSeries(conn, 81189, "breaking-bad-2");

            act.Should().Throw<Exception>();
        }

        [Test]
        public void should_refuse_two_series_with_the_same_title_slug()
        {
            using var conn = WithDapperMigrationTestDb();

            InsertSeries(conn, 81189, "breaking-bad");

            Action act = () => InsertSeries(conn, 81190, "breaking-bad");

            act.Should().Throw<Exception>();
        }

        [Test]
        public void should_refuse_a_root_folder_path_twice()
        {
            using var conn = WithDapperMigrationTestDb();

            conn.Execute("INSERT INTO \"Tv_RootFolders\" (\"Path\") VALUES ('/data/tv')");

            Action act = () => conn.Execute("INSERT INTO \"Tv_RootFolders\" (\"Path\") VALUES ('/data/tv')");

            act.Should().Throw<Exception>();
        }
    }
}
