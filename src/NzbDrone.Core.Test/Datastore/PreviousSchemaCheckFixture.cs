using System;
using System.Data.SQLite;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Datastore.Migration.Framework;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore
{
    [TestFixture]
    public class PreviousSchemaCheckFixture : CoreTest<MigrationController>
    {
        private string _connectionString;

        [SetUp]
        public void Setup()
        {
            Mocker.SetConstant<ILoggerProvider>(Mocker.Resolve<NLogLoggerProvider>());

            _connectionString = new SQLiteConnectionStringBuilder { DataSource = GetTempFilePath() + ".db" }.ConnectionString;
        }

        [TearDown]
        public void TearDown()
        {
            SQLiteConnection.ClearAllPools();
        }

        private void GivenVersionInfo(long version, string description)
        {
            using var conn = new SQLiteConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS \"VersionInfo\" (\"Version\" INTEGER NOT NULL, \"AppliedOn\" DATETIME, \"Description\" TEXT);" +
                              $"INSERT INTO \"VersionInfo\" VALUES ({version}, '2026-01-01', '{description}');";
            cmd.ExecuteNonQuery();
        }

        private void Migrate()
        {
            Subject.Migrate(_connectionString, new MigrationContext(MigrationType.Main), DatabaseType.SQLite);
        }

        [Test]
        public void should_refuse_an_upstream_database()
        {
            // Radarr's and pre-step-8 Plattarr's first row, as stored on plattarr-dev.
            GivenVersionInfo(1, "InitialSetup");
            GivenVersionInfo(244, "add_indexer_overrides");

            Action act = Migrate;

            act.Should().Throw<Exception>()
               .Where(e => e is PreviousSchemaException || e.InnerException is PreviousSchemaException)
               .And.ToString().Should().Contain("schema version 244");
        }

        [TestCase("InitialSetup")]
        [TestCase("initial_setup")]
        [TestCase("SomethingElse")]
        public void should_refuse_any_version_1_that_is_not_plattarrs(string description)
        {
            GivenVersionInfo(1, description);

            Action act = Migrate;

            act.Should().Throw<Exception>()
               .Where(e => e is PreviousSchemaException || e.InnerException is PreviousSchemaException);
        }

        [Test]
        public void should_migrate_an_empty_database()
        {
            Action act = Migrate;

            act.Should().NotThrow();
        }

        [Test]
        public void should_accept_its_own_database_on_restart()
        {
            Migrate();

            Action act = Migrate;

            act.Should().NotThrow();
        }
    }
}
