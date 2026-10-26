using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class HygieneActionJournalFixture : MigrationTest<hygiene_action_journal>
    {
        [Test]
        public void should_create_empty_action_journal()
        {
            var db = WithMigrationTestDb();

            db.Query("SELECT * FROM \"HygieneActions\"").Should().BeEmpty();
        }

        [Test]
        public void should_store_action_checkpoints_and_recovery_details()
        {
            using var connection = WithDapperMigrationTestDb();
            connection.Execute("INSERT INTO \"HygieneActions\" (\"DownloadClientId\", \"DownloadId\", \"MediaKind\", \"ReasonCode\", \"RuleId\", \"Status\", \"Checkpoint\", \"RecoveryPath\", \"CreatedAt\", \"UpdatedAt\") VALUES (4, 'fixture', 2, 'import-blocked', 7, 'processing', 'client-removed', 'manual recovery', '2026-10-25', '2026-10-25')");

            var row = connection.QuerySingle("SELECT \"MediaKind\", \"Status\", \"Checkpoint\", \"RecoveryPath\" FROM \"HygieneActions\"");

            ((int)row.MediaKind).Should().Be(2);
            ((string)row.Status).Should().Be("processing");
            ((string)row.Checkpoint).Should().Be("client-removed");
            ((string)row.RecoveryPath).Should().Be("manual recovery");
        }
    }
}
