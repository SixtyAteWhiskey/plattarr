using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(15)]
    public class hygiene_minimum_strikes : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Execute.Sql("UPDATE \"HygieneRules\" SET \"StrikeLimit\" = 3 WHERE \"StrikeLimit\" < 3");
        }
    }
}
