using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class HygieneReplacementSearchOptionFixture : MigrationTest<hygiene_replacement_search_option>
    {
        [Test]
        public void existing_rules_and_actions_keep_replacement_search_enabled()
        {
            using var connection = WithDapperMigrationTestDb(migration =>
            {
                migration.Insert.IntoTable("HygieneRules").Row(new
                {
                    ReasonCode = "download-stalled",
                    MediaKind = 0,
                    DownloadClientId = 0,
                    Enabled = true,
                    DeleteFilesOnFailure = true,
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
                    DeleteFilesOnFailure = true,
                    Status = "completed",
                    Checkpoint = "replacement-requested",
                    RecoveryPath = "manual recovery",
                    RetryCount = 0,
                    CreatedAt = "2026-10-25",
                    UpdatedAt = "2026-10-25"
                });
            });

            connection.QuerySingle<int>("SELECT \"SearchForReplacement\" FROM \"HygieneRules\"").Should().Be(1);
            connection.QuerySingle<int>("SELECT \"SearchForReplacement\" FROM \"HygieneActions\"").Should().Be(1);
        }
    }
}
