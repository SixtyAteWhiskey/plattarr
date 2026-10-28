using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class HygieneDeleteFilesOptionFixture : MigrationTest<hygiene_delete_files_option>
    {
        [Test]
        public void existing_rules_and_actions_keep_source_files_by_default()
        {
            using var connection = WithDapperMigrationTestDb(migration =>
            {
                migration.Insert.IntoTable("HygieneRules").Row(new
                {
                    ReasonCode = "download-stalled",
                    MediaKind = 0,
                    DownloadClientId = 0,
                    Enabled = false,
                    StrikeLimit = 3,
                    StrikeIntervalMinutes = 5,
                    MinimumDownloadRateKiBps = 0
                });

                migration.Insert.IntoTable("HygieneActions").Row(new
                {
                    DownloadClientId = 4,
                    DownloadId = "existing-action",
                    MediaKind = 2,
                    ReasonCode = "download-stalled",
                    RuleId = 1,
                    Status = "completed",
                    Checkpoint = "replacement-requested",
                    RecoveryPath = "manual recovery",
                    RetryCount = 0,
                    CreatedAt = "2026-10-25",
                    UpdatedAt = "2026-10-25"
                });
            });

            connection.QuerySingle<int>("SELECT \"DeleteFilesOnFailure\" FROM \"HygieneRules\"").Should().Be(0);
            connection.QuerySingle<int>("SELECT \"DeleteFilesOnFailure\" FROM \"HygieneActions\"").Should().Be(0);
        }
    }
}
