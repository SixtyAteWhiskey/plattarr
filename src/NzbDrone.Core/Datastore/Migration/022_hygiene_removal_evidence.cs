using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(22)]
    public class hygiene_removal_evidence : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("HygieneActions")
                .AddColumn("ClientRemovalStatus").AsString().Nullable()
                .AddColumn("SourceDeletionStatus").AsString().Nullable();
        }
    }
}
