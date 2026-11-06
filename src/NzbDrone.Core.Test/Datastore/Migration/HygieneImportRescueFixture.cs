using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class HygieneImportRescueFixture : MigrationTest<hygiene_import_rescue>
    {
        [Test]
        public void existing_rules_do_not_force_import()
        {
            using var connection = WithDapperMigrationTestDb(migration =>
            {
                migration.Insert.IntoTable("HygieneRules").Row(new
                {
                    ReasonCode = "import-blocked",
                    MediaKind = 0,
                    DownloadClientId = 0,
                    Enabled = true,
                    StrikeLimit = 3,
                    StrikeIntervalMinutes = 5
                });
            });

            var rule = connection.QuerySingle("SELECT \"ForceImportEnabled\", \"ForceImportMaxTries\" FROM \"HygieneRules\"");
            ((long)rule.ForceImportEnabled).Should().Be(0);
            ((long)rule.ForceImportMaxTries).Should().Be(3);
            connection.Query("SELECT * FROM \"HygieneImportRescues\"").Should().BeEmpty();
        }
    }
}
