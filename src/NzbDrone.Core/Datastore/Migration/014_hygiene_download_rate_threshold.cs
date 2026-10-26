using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(14)]
    public class hygiene_download_rate_threshold : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("HygieneRules")
                .AddColumn("MinimumDownloadRateKiBps").AsInt32().NotNullable().WithDefaultValue(0);
        }
    }
}
