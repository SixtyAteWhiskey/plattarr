using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class HygienePrivateCategoryFixture : MigrationTest<hygiene_private_category>
    {
        [Test]
        public void existing_rules_keep_private_torrents_held_and_category_change_off()
        {
            using var connection = WithDapperMigrationTestDb(migration =>
            {
                migration.Insert.IntoTable("HygieneRules").Row(new
                {
                    ReasonCode = "client-failed",
                    MediaKind = 0,
                    DownloadClientId = 0,
                    Enabled = true,
                    StrikeLimit = 3,
                    StrikeIntervalMinutes = 5
                });
            });

            var row = connection.QuerySingle("SELECT \"PrivateTorrentAction\", \"ChangeCategoryInsteadOfRemoval\" FROM \"HygieneRules\"");
            ((string)row.PrivateTorrentAction).Should().Be("hold");
            ((long)row.ChangeCategoryInsteadOfRemoval).Should().Be(0);
        }
    }
}
