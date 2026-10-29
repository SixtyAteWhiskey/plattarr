using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(20)]
    public class hygiene_unstable_strikes : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("HygieneQueueStates")
                .AddColumn("UnstableStrikes").AsInt32().NotNullable().WithDefaultValue(0)
                .AddColumn("UnstableRuleId").AsInt32().NotNullable().WithDefaultValue(0)
                .AddColumn("LastUnstableStrikeAt").AsDateTime().Nullable();
        }
    }
}
