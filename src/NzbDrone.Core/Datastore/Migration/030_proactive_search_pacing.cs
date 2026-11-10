using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(30)]
    public class proactive_search_pacing : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("Movies_ProactiveSearchActivity").AddColumn("Reason").AsString().Nullable();
            Alter.Table("Tv_ProactiveSearchActivity").AddColumn("Reason").AsString().Nullable();
        }
    }
}
