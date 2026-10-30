using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class HygieneWarningCleanupOptInFixture : MigrationTest<hygiene_warning_cleanup_opt_in>
    {
        [Test]
        public void existing_enabled_warning_rules_remain_monitor_only()
        {
            using var connection = WithDapperMigrationTestDb(migration =>
            {
                migration.Insert.IntoTable("HygieneRules").Row(new
                {
                    ReasonCode = "download-warning",
                    MediaKind = 0,
                    DownloadClientId = 0,
                    Enabled = true,
                    StrikeLimit = 3,
                    StrikeIntervalMinutes = 2,
                    MinimumDownloadRateKiBps = 0
                });
            });

            connection.QuerySingle<int>("SELECT \"WarningCleanupEnabled\" FROM \"HygieneRules\"").Should().Be(0);
        }
    }
}
