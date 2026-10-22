using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.History;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore
{
    [TestFixture]
    public class ActivityFilterSqlFixture : DbTest<HistoryRepository, MovieHistory>
    {
        private const string Table = "History";

        private int Insert(string languagesJson, Quality quality = null)
        {
            var row = new MovieHistory
            {
                MediaKind = MediaKind.Movies,
                ParentId = 1,
                SourceTitle = "t",
                Quality = new QualityModel(quality ?? Quality.HDTV720p),
                Date = DateTime.UtcNow,
                Languages = new List<Language>()
            };

            Subject.Insert(row);

            using var conn = Mocker.Resolve<IMainDatabase>().OpenConnection();
            conn.Execute($"UPDATE \"{Table}\" SET \"Languages\" = @json WHERE \"Id\" = @id", new { json = languagesJson, id = row.Id });

            return row.Id;
        }

        private List<int> Matching(string clause)
        {
            using var conn = Mocker.Resolve<IMainDatabase>().OpenConnection();

            return conn.Query<int>($"SELECT \"Id\" FROM \"{Table}\" WHERE {clause} ORDER BY \"Id\"").ToList();
        }

        private DatabaseType CurrentDatabase() => Mocker.Resolve<IMainDatabase>().DatabaseType;

        [Test]
        public void should_build_chr_for_postgres_and_char_for_sqlite()
        {
            ActivityFilterSql.LanguageClause(DatabaseType.PostgreSQL, Table, new[] { 5 }).Should().Contain("CHR(13)").And.Contain("CHR(10)").And.NotContain("CHAR(");
            ActivityFilterSql.LanguageClause(DatabaseType.SQLite, Table, new[] { 5 }).Should().Contain("CHAR(13)").And.Contain("CHAR(10)").And.NotContain("CHR(");
        }

        [Test]
        public void should_run_on_the_current_database_for_every_stored_form()
        {
            var middle = Insert("[\n  5,\n  7\n]");
            var last = Insert("[\n  7,\n  5\n]");
            var lastCr = Insert("[\r  7,\r  5\r]");
            var lastCrLf = Insert("[\r\n  7,\r\n  5\r\n]");
            var single = Insert("[5]");
            Insert("[\n  7\n]");
            Insert("[]");

            var clause = ActivityFilterSql.LanguageClause(CurrentDatabase(), Table, new[] { 5 });

            Matching(clause).Should().Equal(new[] { middle, last, lastCr, lastCrLf, single }.OrderBy(x => x));
        }

        [Test]
        public void should_match_any_of_several_languages()
        {
            var five = Insert("[\n  5\n]");
            var six = Insert("[\n  6\n]");
            Insert("[\n  7\n]");

            Matching(ActivityFilterSql.LanguageClause(CurrentDatabase(), Table, new[] { 5, 6 })).Should().Equal(five, six);
        }

        [Test]
        public void should_not_match_a_language_that_only_shares_digits()
        {
            Insert("[\n  15\n]");
            Insert("[\n  51,\n  3\n]");

            Matching(ActivityFilterSql.LanguageClause(CurrentDatabase(), Table, new[] { 5 })).Should().BeEmpty();
        }

        [Test]
        public void should_match_quality_ids()
        {
            var hdtv = Insert("[]", Quality.HDTV720p);
            var dvd = Insert("[]", Quality.DVD);
            Insert("[]", Quality.Bluray1080p);

            Matching(ActivityFilterSql.QualityClause(Table, new[] { Quality.HDTV720p.Id, Quality.DVD.Id })).Should().Equal(hdtv, dvd);
            Matching(ActivityFilterSql.QualityClause(Table, new[] { Quality.DVD.Id })).Should().Equal(dvd);
        }
    }
}
