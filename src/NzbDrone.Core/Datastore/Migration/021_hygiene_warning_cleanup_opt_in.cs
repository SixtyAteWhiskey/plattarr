using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(21)]
    public class hygiene_warning_cleanup_opt_in : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("HygieneRules")
                .AddColumn("WarningCleanupEnabled").AsBoolean().NotNullable().WithDefaultValue(false);
        }
    }
}
