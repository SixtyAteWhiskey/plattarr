using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class HygieneActionRetryBackoffFixture : MigrationTest<hygiene_action_retry_backoff>
    {
        [Test]
        public void should_default_existing_and_new_action_retries_to_zero()
        {
            using var connection = WithDapperMigrationTestDb();
            connection.Execute("INSERT INTO \"HygieneActions\" (\"DownloadClientId\", \"DownloadId\", \"MediaKind\", \"ReasonCode\", \"RuleId\", \"Status\", \"Checkpoint\", \"RecoveryPath\", \"CreatedAt\", \"UpdatedAt\") VALUES (4, 'fixture', 2, 'download-stalled', 7, 'failed', 'prepared', 'manual recovery', '2026-10-25', '2026-10-25')");

            var row = connection.QuerySingle("SELECT \"RetryCount\", \"NextAttemptAt\" FROM \"HygieneActions\"");

            ((int)row.RetryCount).Should().Be(0);
            ((object)row.NextAttemptAt).Should().BeNull();
        }
    }
}
