using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(24)]
    public class hygiene_content_actions : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("HygieneActions")
                .AddColumn("ContentActionMode").AsString().Nullable();
        }
    }
}
