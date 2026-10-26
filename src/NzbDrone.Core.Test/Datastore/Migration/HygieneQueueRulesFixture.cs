using System;
using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class HygieneQueueRulesFixture : MigrationTest<hygiene_queue_rules>
    {
        [Test]
        public void should_create_empty_rule_and_strike_tables()
        {
            var db = WithMigrationTestDb();

            db.Query("SELECT * FROM \"HygieneRules\"").Should().BeEmpty();
            db.Query("SELECT * FROM \"HygieneQueueStates\"").Should().BeEmpty();
        }

        [Test]
        public void should_reject_duplicate_rules_for_the_same_scope()
        {
            using var connection = WithDapperMigrationTestDb();
            var row = new
            {
                ReasonCode = "import-blocked",
                MediaKind = 2,
                DownloadClientId = 4,
                Enabled = false,
                StrikeLimit = 3,
                StrikeIntervalMinutes = 5
            };

            const string sql = "INSERT INTO \"HygieneRules\" (\"ReasonCode\", \"MediaKind\", \"DownloadClientId\", \"Enabled\", \"StrikeLimit\", \"StrikeIntervalMinutes\") VALUES (@ReasonCode, @MediaKind, @DownloadClientId, @Enabled, @StrikeLimit, @StrikeIntervalMinutes)";
            connection.Execute(sql, row);
            var duplicate = () => connection.Execute(sql, row);

            duplicate.Should().Throw<Exception>();
        }
    }
}
