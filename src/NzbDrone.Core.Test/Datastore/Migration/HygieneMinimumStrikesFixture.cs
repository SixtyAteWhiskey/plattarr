using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class HygieneMinimumStrikesFixture : MigrationTest<hygiene_minimum_strikes>
    {
        [Test]
        public void should_raise_existing_rules_to_the_three_strike_minimum()
        {
            var db = WithMigrationTestDb(migration =>
            {
                migration.Insert.IntoTable("HygieneRules").Row(new
                {
                    ReasonCode = "import-blocked",
                    MediaKind = 0,
                    DownloadClientId = 0,
                    Enabled = false,
                    StrikeLimit = 1,
                    StrikeIntervalMinutes = 5,
                    MinimumDownloadRateKiBps = 0
                });

                migration.Insert.IntoTable("HygieneRules").Row(new
                {
                    ReasonCode = "client-failed",
                    MediaKind = 0,
                    DownloadClientId = 0,
                    Enabled = false,
                    StrikeLimit = 5,
                    StrikeIntervalMinutes = 5,
                    MinimumDownloadRateKiBps = 0
                });
            });

            var limits = db.Query("SELECT \"ReasonCode\", \"StrikeLimit\" FROM \"HygieneRules\"")
                           .ToDictionary(row => (string)row["ReasonCode"], row => Convert.ToInt32(row["StrikeLimit"]));

            limits["import-blocked"].Should().Be(3);
            limits["client-failed"].Should().Be(5);
        }
    }
}
