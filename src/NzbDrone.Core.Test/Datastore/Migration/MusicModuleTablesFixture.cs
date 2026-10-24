using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Music;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    // Migration 009 adds the Music module's tables to a database that already holds movie and TV
    // rows. It changes no existing row.
    [TestFixture]
    public class MusicModuleTablesFixture : MigrationTest<music_module_tables>
    {
        private static readonly string[] MusicTables =
        {
            "Music_ArtistMetadata",
            "Music_Artists",
            "Music_Albums",
            "Music_AlbumReleases",
            "Music_Tracks",
            "Music_TrackFiles",
            "Music_QualityDefinitions",
            "Music_QualityProfiles",
            "Music_MetadataProfiles",
            "Music_RootFolders",
            "Music_NamingConfig"
        };

        [Test]
        public void should_create_every_music_table_empty()
        {
            var db = WithMigrationTestDb();

            foreach (var table in MusicTables)
            {
                db.Query($"SELECT \"Id\" FROM \"{table}\"").Should().BeEmpty(table);
            }
        }

        [Test]
        public void should_keep_existing_activity_rows_untouched()
        {
            var db = WithMigrationTestDb(m =>
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
                    ItemId = 0,
                    Languages = "[]"
                });

                m.Insert.IntoTable("Blocklist").Row(new
                {
                    MediaKind = 2,
                    SourceTitle = "Show.S01.720p",
                    Quality = "{}",
                    Date = DateTime.UtcNow,
                    ParentId = 9,
                    ItemIds = "[1,2]",
                    ReleaseType = 3,
                    Languages = "[]",
                    IndexerFlags = 4
                });
            });

            var history = db.Query("SELECT \"MediaKind\", \"ParentId\", \"ItemId\", \"DownloadId\" FROM \"History\"").Single();
            Convert.ToInt32(history["MediaKind"]).Should().Be(1);
            Convert.ToInt32(history["ParentId"]).Should().Be(7);
            history["DownloadId"].Should().Be("history-download");

            var blocklist = db.Query("SELECT \"MediaKind\", \"ParentId\", \"ItemIds\", \"ReleaseType\", \"SourceTitle\" FROM \"Blocklist\"").Single();
            Convert.ToInt32(blocklist["MediaKind"]).Should().Be(2);
            Convert.ToInt32(blocklist["ParentId"]).Should().Be(9);
            blocklist["ItemIds"].Should().Be("[1,2]");
            Convert.ToInt32(blocklist["ReleaseType"]).Should().Be(3);
        }

        [Test]
        public void should_give_every_mapped_music_property_a_column()
        {
            using var conn = WithDapperMigrationTestDb();

            var mapper = new TableMapper();
            new MusicTableMap().MapTables(mapper);

            var tables = mapper.TableMap.Where(t => t.Value.StartsWith("Music_", StringComparison.Ordinal)).ToList();
            tables.Select(t => t.Value).Should().BeEquivalentTo(MusicTables);

            foreach (var (type, table) in tables.Select(t => (t.Key, t.Value)))
            {
                var excluded = mapper.ExcludeProperties(type).Select(p => p.Name).ToHashSet();
                var properties = type.GetProperties()
                    .Where(p => p.IsMappableProperty() && !excluded.Contains(p.Name))
                    .Select(p => p.Name)
                    .ToList();

                var columns = Columns(conn, table);

                properties.Should().BeSubsetOf(columns, $"every {type.Name} property is a {table} column");
            }
        }

        // Columns the entity doesn't write must have a default (or be nullable), or an insert fails.
        [Test]
        public void should_default_the_columns_an_artist_insert_leaves_out()
        {
            using var conn = WithDapperMigrationTestDb();

            conn.Execute("INSERT INTO \"Music_ArtistMetadata\" (\"ForeignArtistId\", \"Name\") VALUES ('a-1', 'Artist')");
            var row = conn.QuerySingle("SELECT \"OldForeignArtistIds\", \"Aliases\", \"Images\", \"Links\", \"Genres\", \"Members\", \"Status\" FROM \"Music_ArtistMetadata\"");

            ((string)row.OldForeignArtistIds).Should().Be("[]");
            ((string)row.Aliases).Should().Be("[]");
            ((string)row.Images).Should().Be("[]");
            ((string)row.Links).Should().Be("[]");
            ((string)row.Genres).Should().Be("[]");
            ((string)row.Members).Should().Be("[]");
            ((int)row.Status).Should().Be(0);
        }

        [Test]
        public void should_default_the_root_folder_and_naming_config()
        {
            using var conn = WithDapperMigrationTestDb();

            conn.Execute("INSERT INTO \"Music_RootFolders\" (\"Path\") VALUES ('/data/music')");
            conn.Execute("INSERT INTO \"Music_NamingConfig\" (\"StandardTrackFormat\") VALUES ('x')");

            var folder = conn.QuerySingle("SELECT \"DefaultTags\", \"DefaultMonitorOption\", \"DefaultNewItemMonitorOption\" FROM \"Music_RootFolders\"");
            ((string)folder.DefaultTags).Should().Be("[]");
            ((int)folder.DefaultMonitorOption).Should().Be(0);

            var naming = conn.QuerySingle("SELECT \"RenameTracks\", \"ReplaceIllegalCharacters\", \"ColonReplacementFormat\" FROM \"Music_NamingConfig\"");
            Convert.ToInt32((object)naming.ReplaceIllegalCharacters).Should().Be(1);
            ((int)naming.ColonReplacementFormat).Should().Be(4, "Smart, Lidarr's default");
        }

        [TestCase("INSERT INTO \"Music_ArtistMetadata\" (\"ForeignArtistId\", \"Name\") VALUES ('dup', 'A')")]
        [TestCase("INSERT INTO \"Music_Albums\" (\"ArtistMetadataId\", \"ForeignAlbumId\", \"Title\", \"Monitored\", \"Added\") VALUES (1, 'dup', 'T', true, '2026-01-01')")]
        [TestCase("INSERT INTO \"Music_AlbumReleases\" (\"AlbumId\", \"ForeignReleaseId\", \"Title\", \"Monitored\") VALUES (1, 'dup', 'T', true)")]
        [TestCase("INSERT INTO \"Music_Tracks\" (\"ForeignTrackId\", \"AlbumReleaseId\", \"ArtistMetadataId\") VALUES ('dup', 1, 1)")]
        [TestCase("INSERT INTO \"Music_QualityDefinitions\" (\"Quality\", \"Title\") VALUES (201, 'dup')")]
        [TestCase("INSERT INTO \"Music_QualityProfiles\" (\"Name\", \"Cutoff\", \"Items\") VALUES ('dup', 1, '[]')")]
        [TestCase("INSERT INTO \"Music_MetadataProfiles\" (\"Name\", \"PrimaryAlbumTypes\", \"SecondaryAlbumTypes\", \"ReleaseStatuses\") VALUES ('dup', '[]', '[]', '[]')")]
        [TestCase("INSERT INTO \"Music_RootFolders\" (\"Path\") VALUES ('/dup')")]
        public void should_refuse_a_duplicate_of_a_unique_value(string insert)
        {
            using var conn = WithDapperMigrationTestDb();

            conn.Execute(insert);

            Action act = () => conn.Execute(insert);

            act.Should().Throw<Exception>();
        }

        [Test]
        public void should_refuse_two_artists_for_one_metadata_row()
        {
            using var conn = WithDapperMigrationTestDb();

            const string insert = "INSERT INTO \"Music_Artists\" (\"ArtistMetadataId\", \"Monitored\", \"Path\", \"Added\", \"QualityProfileId\") VALUES (1, true, '/a', '2026-01-01', 1)";
            conn.Execute(insert);

            Action act = () => conn.Execute(insert.Replace("'/a'", "'/b'"));

            act.Should().Throw<Exception>();
        }

        [Test]
        public void should_create_the_lookup_indexes()
        {
            var db = WithMigrationTestDb();

            var sql = Mocker.Resolve<IMainDatabase>().DatabaseType == DatabaseType.PostgreSQL
                ? "SELECT indexname AS name FROM pg_indexes WHERE schemaname = current_schema()"
                : "SELECT name FROM sqlite_master WHERE type = 'index' AND sql IS NOT NULL";

            var indexes = db.Query(sql).Select(r => (string)r["name"]).ToList();

            indexes.Should().Contain(new[]
            {
                "IX_Music_Artists_Path",
                "IX_Music_Artists_CleanName",
                "IX_Music_Albums_ArtistMetadataId",
                "IX_Music_Albums_CleanTitle",
                "IX_Music_AlbumReleases_AlbumId",
                "IX_Music_Tracks_AlbumReleaseId",
                "IX_Music_Tracks_ArtistMetadataId",
                "IX_Music_Tracks_TrackFileId",
                "IX_Music_TrackFiles_AlbumId",
                "IX_History_MediaKind_ItemId_Date"
            });
        }

        private List<string> Columns(System.Data.IDbConnection conn, string table)
        {
            if (Mocker.Resolve<IMainDatabase>().DatabaseType == DatabaseType.PostgreSQL)
            {
                return conn.Query<string>("SELECT column_name FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = @table", new { table }).ToList();
            }

            return conn.Query($"PRAGMA table_info('{table}')").Select(r => (string)r.name).ToList();
        }
    }
}
