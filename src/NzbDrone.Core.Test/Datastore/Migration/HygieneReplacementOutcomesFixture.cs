using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class HygieneReplacementOutcomesFixture : MigrationTest<hygiene_replacement_outcomes>
    {
        [Test]
        public void earlier_actions_keep_search_outcome_unknown()
        {
            using var connection = WithDapperMigrationTestDb(migration =>
            {
                migration.Insert.IntoTable("HygieneActions").Row(new
                {
                    DownloadClientId = 4,
                    DownloadId = "old-download",
                    MediaKind = 2,
                    ReasonCode = "download-stalled",
                    RuleId = 1,
                    Status = "completed",
                    Checkpoint = "replacement-requested",
                    RecoveryPath = "manual recovery",
                    RetryCount = 0,
                    DeleteFilesOnFailure = true,
                    CreatedAt = "2026-10-28",
                    UpdatedAt = "2026-10-28"
                });
            });

            var row = connection.QuerySingle("SELECT \"ReplacementSearchCommandId\", \"ReplacementTargetIds\", \"ReplacementSearchStatus\", \"ReplacementGrabCount\" FROM \"HygieneActions\" WHERE \"DownloadId\" = 'old-download'");
            ((object)row.ReplacementSearchCommandId is null or System.DBNull).Should().BeTrue();
            ((object)row.ReplacementTargetIds is null or System.DBNull).Should().BeTrue();
            ((object)row.ReplacementSearchStatus is null or System.DBNull).Should().BeTrue();
            ((object)row.ReplacementGrabCount is null or System.DBNull).Should().BeTrue();
        }
    }
}
