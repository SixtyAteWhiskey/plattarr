using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(17)]
    public class hygiene_delete_files_option : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("HygieneRules")
                .AddColumn("DeleteFilesOnFailure").AsBoolean().NotNullable().WithDefaultValue(false);

            Alter.Table("HygieneActions")
                .AddColumn("DeleteFilesOnFailure").AsBoolean().NotNullable().WithDefaultValue(false);
        }
    }
}
