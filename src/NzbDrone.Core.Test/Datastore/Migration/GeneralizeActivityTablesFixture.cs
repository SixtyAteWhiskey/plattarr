using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class GeneralizeActivityTablesFixture : MigrationTest<generalize_activity_tables>
    {
        private static readonly string[] ActivityTables = { "History", "DownloadHistory", "Blocklist", "PendingReleases" };

        private static readonly string[] MovieIndexes =
        {
            "IX_History_MediaKind_MovieId_Date",
            "IX_DownloadHistory_MediaKind_MovieId",
            "IX_Blocklist_MediaKind_MovieId"
        };

        private static readonly string[] ParentIndexes =
        {
            "IX_History_MediaKind_ParentId_Date",
            "IX_DownloadHistory_MediaKind_ParentId",
            "IX_Blocklist_MediaKind_ParentId"
        };

        private IDirectDataMapper GivenActivityRows()
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
                    MovieId = 7,
                    Languages = "[]"
                });

                m.Insert.IntoTable("DownloadHistory").Row(new
                {
                    MediaKind = 1,
                    EventType = 1,
                    MovieId = 8,
                    DownloadId = "download-history",
                    SourceTitle = "Movie.2024.1080p",
                    Date = DateTime.UtcNow
                });

                m.Insert.IntoTable("Blocklist").Row(new
                {
                    MediaKind = 1,
                    SourceTitle = "Movie.2024.1080p",
                    Quality = "{}",
                    Date = DateTime.UtcNow,
                    MovieId = 9,
                    Languages = "[]"
                });

                m.Insert.IntoTable("PendingReleases").Row(new
                {
                    MediaKind = 1,
                    Title = "Movie.2024.1080p",
                    Added = DateTime.UtcNow,
                    Release = "{}",
                    MovieId = 10,
                    ParsedMovieInfo = "{\"movieTitles\":[\"Movie\"],\"year\":2024}",
                    Reason = 1
                });
            });
        }

        private static int ParentIdOf(IDirectDataMapper db, string table)
        {
            return Convert.ToInt32(db.Query($"SELECT \"ParentId\" FROM \"{table}\"").Single()["ParentId"]);
        }

        [TestCase("History", 7)]
        [TestCase("DownloadHistory", 8)]
        [TestCase("Blocklist", 9)]
        [TestCase("PendingReleases", 10)]
        public void should_keep_the_parent_of_every_row(string table, int parentId)
        {
            var db = GivenActivityRows();

            ParentIdOf(db, table).Should().Be(parentId);
        }

        [Test]
        public void should_keep_the_media_kind_of_every_row()
        {
            var db = GivenActivityRows();

            foreach (var table in ActivityTables)
            {
                db.Query($"SELECT \"MediaKind\" FROM \"{table}\"")
                  .Select(r => Convert.ToInt32(r["MediaKind"]))
                  .Should().Equal(1);
            }
        }

        [Test]
        public void should_keep_the_parsed_release_of_a_pending_release()
        {
            var db = GivenActivityRows();

            db.Query("SELECT \"ParsedInfo\" FROM \"PendingReleases\"").Single()["ParsedInfo"]
              .Should().Be("{\"movieTitles\":[\"Movie\"],\"year\":2024}");
        }

        [TestCase("History", "MovieId")]
        [TestCase("DownloadHistory", "MovieId")]
        [TestCase("Blocklist", "MovieId")]
        [TestCase("PendingReleases", "MovieId")]
        [TestCase("PendingReleases", "ParsedMovieInfo")]
        public void should_not_leave_a_movie_column_on_a_shared_table(string table, string column)
        {
            var db = GivenActivityRows();

            Action act = () => db.Query($"SELECT \"{column}\" FROM \"{table}\"");

            act.Should().Throw<Exception>();
        }

        [Test]
        public void should_replace_the_movie_indexes_with_parent_indexes()
        {
            var db = GivenActivityRows();

            var sql = Mocker.Resolve<IMainDatabase>().DatabaseType == DatabaseType.PostgreSQL
                ? "SELECT indexname AS name, indexdef AS definition FROM pg_indexes WHERE schemaname = current_schema()"
                : "SELECT name, sql AS definition FROM sqlite_master WHERE type = 'index' AND sql IS NOT NULL";

            var indexes = db.Query(sql).ToDictionary(r => (string)r["name"], r => (string)r["definition"]);

            indexes.Keys.Should().NotContain(MovieIndexes);
            indexes.Keys.Should().Contain(ParentIndexes);

            foreach (var name in ParentIndexes)
            {
                indexes[name].Should().Contain("MediaKind").And.Contain("ParentId");
            }

            indexes["IX_History_MediaKind_ParentId_Date"].Should().Contain("Date");
        }

        [Test]
        public void should_migrate_empty_tables()
        {
            var db = WithMigrationTestDb();

            foreach (var table in ActivityTables)
            {
                db.Query($"SELECT \"ParentId\" FROM \"{table}\"").Should().BeEmpty();
            }
        }
    }
}
