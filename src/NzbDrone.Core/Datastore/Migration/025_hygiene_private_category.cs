using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(25)]
    public class hygiene_private_category : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("HygieneRules")
                .AddColumn("PrivateTorrentAction").AsString().NotNullable().WithDefaultValue("hold")
                .AddColumn("ChangeCategoryInsteadOfRemoval").AsBoolean().NotNullable().WithDefaultValue(false);
            Alter.Table("HygieneActions")
                .AddColumn("QueueActionMode").AsString().Nullable();
        }
    }
}
