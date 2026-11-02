using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class HygieneRemovalEvidenceFixture : MigrationTest<hygiene_removal_evidence>
    {
        [Test]
        public void existing_actions_have_no_inferred_removal_evidence()
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
                    Checkpoint = "client-removed",
                    RecoveryPath = "manual recovery",
                    CreatedAt = "2026-10-28",
                    UpdatedAt = "2026-10-28"
                });
            });

            var row = connection.QuerySingle("SELECT \"ClientRemovalStatus\", \"SourceDeletionStatus\" FROM \"HygieneActions\" WHERE \"DownloadId\" = 'old-download'");
            ((object)row.ClientRemovalStatus is null or System.DBNull).Should().BeTrue();
            ((object)row.SourceDeletionStatus is null or System.DBNull).Should().BeTrue();
        }
    }
}
