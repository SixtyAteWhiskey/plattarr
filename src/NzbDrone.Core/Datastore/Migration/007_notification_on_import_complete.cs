using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    // TV port, step 7 (docs/module-interface.md, "TV step 7, part 2: as built"). Sonarr's "On Import
    // Complete" notification event: one message per download after its last file is imported. The
    // default keeps every existing notification as it is.
    [Migration(7)]
    public class notification_on_import_complete : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("Notifications").AddColumn("OnImportComplete").AsBoolean().NotNullable().WithDefaultValue(false);
        }
    }
}
