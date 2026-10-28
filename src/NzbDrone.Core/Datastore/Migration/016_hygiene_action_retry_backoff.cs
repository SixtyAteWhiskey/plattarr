using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(16)]
    public class hygiene_action_retry_backoff : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("HygieneActions")
                .AddColumn("RetryCount").AsInt32().NotNullable().WithDefaultValue(0)
                .AddColumn("NextAttemptAt").AsDateTime().Nullable();
        }
    }
}
