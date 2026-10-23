using System;
using System.Linq;
using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    // Migration 008 adds the series auto tag table. The movie AutoTagging rows are not touched.
    [TestFixture]
    public class TvAutoTaggingFixture : MigrationTest<tv_auto_tagging>
    {
        [Test]
        public void should_create_an_empty_table_and_leave_the_movie_auto_tags_alone()
        {
            var db = WithMigrationTestDb(m =>
            {
                m.Insert.IntoTable("AutoTagging").Row(new
                {
                    Name = "Movie comedy",
                    Specifications = "[]",
                    RemoveTagsAutomatically = true,
                    Tags = "[1]"
                });
            });

            db.Query("SELECT * FROM \"Tv_AutoTagging\"").Should().BeEmpty();

            var movieRow = db.Query("SELECT \"Name\", \"Tags\" FROM \"AutoTagging\"").Single();
            movieRow["Name"].Should().Be("Movie comedy");
            movieRow["Tags"].Should().Be("[1]");
        }

        [Test]
        public void should_default_the_columns_and_keep_names_unique()
        {
            using var conn = WithDapperMigrationTestDb();

            conn.Execute("INSERT INTO \"Tv_AutoTagging\" (\"Name\") VALUES ('One')");

            var row = conn.QuerySingle("SELECT \"Specifications\", \"RemoveTagsAutomatically\", \"Tags\" FROM \"Tv_AutoTagging\"");
            ((string)row.Specifications).Should().Be("[]");
            Convert.ToBoolean((object)row.RemoveTagsAutomatically).Should().BeFalse();
            ((string)row.Tags).Should().Be("[]");

            var duplicate = () => conn.Execute("INSERT INTO \"Tv_AutoTagging\" (\"Name\") VALUES ('One')");
            duplicate.Should().Throw<Exception>();
        }
    }
}
