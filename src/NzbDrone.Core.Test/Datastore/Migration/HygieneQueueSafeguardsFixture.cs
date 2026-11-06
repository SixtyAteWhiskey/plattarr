using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class HygieneQueueSafeguardsFixture : MigrationTest<hygiene_queue_safeguards>
    {
        [Test]
        public void existing_rules_keep_their_behavior_after_upgrade()
        {
            using var connection = WithDapperMigrationTestDb(migration =>
            {
                migration.Insert.IntoTable("HygieneRules").Row(new
                {
                    ReasonCode = "download-slow",
                    MediaKind = 0,
                    DownloadClientId = 0,
                    Enabled = true,
                    StrikeLimit = 3,
                    StrikeIntervalMinutes = 5,
                    MinimumDownloadRateKiBps = 100
                });
            });

            var row = connection.QuerySingle("SELECT * FROM \"HygieneRules\"");
            ((string)row.FailedImportPatternMode).Should().Be("all");
            ((int)row.MinimumCompletionPercent).Should().Be(0);
            ((int)row.MaximumCompletionPercent).Should().Be(100);
            ((long)row.MinimumTorrentSizeBytes).Should().Be(0);
            ((long)row.MaximumTorrentSizeBytes).Should().Be(0);
            ((int)row.MaximumDownloadHours).Should().Be(0);
            connection.QuerySingle<int>("SELECT \"IgnoreWhileAlternateSpeedActive\" FROM \"HygieneRules\"").Should().Be(1);
        }
    }
}
