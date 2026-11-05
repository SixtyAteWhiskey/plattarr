using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class HygieneContentActionsFixture : MigrationTest<hygiene_content_actions>
    {
        [Test]
        public void existing_actions_are_not_content_actions()
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
                    CreatedAt = "2026-11-05",
                    UpdatedAt = "2026-11-05"
                });
            });

            var row = connection.QuerySingle("SELECT \"ContentActionMode\" FROM \"HygieneActions\" WHERE \"DownloadId\" = 'old-download'");
            ((object)row.ContentActionMode is null or System.DBNull).Should().BeTrue();
        }
    }
}
