using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class HygieneUnstableStrikesFixture : MigrationTest<hygiene_unstable_strikes>
    {
        [Test]
        public void existing_queue_state_starts_with_no_cumulative_strikes()
        {
            using var connection = WithDapperMigrationTestDb(migration =>
            {
                migration.Insert.IntoTable("HygieneQueueStates").Row(new
                {
                    DownloadClientId = 4,
                    DownloadId = "existing-download",
                    MediaKind = 2,
                    ReasonCode = "download-slow",
                    Strikes = 2,
                    RemainingSize = 1000,
                    LastSeenAt = "2026-10-29"
                });
            });

            connection.QuerySingle<int>("SELECT \"UnstableStrikes\" FROM \"HygieneQueueStates\"").Should().Be(0);
            connection.QuerySingle<int>("SELECT \"UnstableRuleId\" FROM \"HygieneQueueStates\"").Should().Be(0);
            connection.QuerySingle<int>("SELECT \"Strikes\" FROM \"HygieneQueueStates\"").Should().Be(2);
        }
    }
}
