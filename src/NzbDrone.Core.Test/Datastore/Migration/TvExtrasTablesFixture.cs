using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    // Migration 006 adds the tables the TV module's extras write. It only creates tables: the movie
    // module's equivalents and every existing row stay as they were.
    [TestFixture]
    public class TvExtrasTablesFixture : MigrationTest<tv_extras_tables>
    {
        private static readonly string[] _tables =
        {
            "Tv_Metadata",
            "Tv_MetadataFiles",
            "Tv_ExtraFiles",
            "Tv_SubtitleFiles"
        };

        [TestCaseSource(nameof(_tables))]
        public void should_create_the_table_empty(string table)
        {
            var db = WithMigrationTestDb();

            db.Query($"SELECT * FROM \"{table}\"").Should().BeEmpty();
        }

        [Test]
        public void should_leave_the_movie_module_tables_alone()
        {
            var db = WithMigrationTestDb();

            foreach (var table in new[] { "Movies_Metadata", "Movies_MetadataFiles", "Movies_ExtraFiles", "Movies_SubtitleFiles" })
            {
                db.Query($"SELECT * FROM \"{table}\"").Should().BeEmpty();
            }
        }

        [Test]
        public void should_default_a_subtitle_copy_to_zero_and_leave_the_optional_columns_null()
        {
            using var conn = WithDapperMigrationTestDb();

            conn.Execute("INSERT INTO \"Tv_SubtitleFiles\" (\"SeriesId\", \"RelativePath\", \"Extension\", \"Added\", \"LastUpdated\", \"Language\") " +
                         "VALUES (1, 'Season 01/ep.en.srt', '.srt', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', 1)");

            var row = conn.QuerySingle("SELECT \"Copy\", \"EpisodeFileId\", \"SeasonNumber\", \"LanguageTags\", \"Title\" FROM \"Tv_SubtitleFiles\"");

            ((int)row.Copy).Should().Be(0);
            ((object)row.EpisodeFileId).Should().BeNull();
            ((object)row.SeasonNumber).Should().BeNull();
            ((object)row.LanguageTags).Should().BeNull();
            ((object)row.Title).Should().BeNull();
        }
    }
}
