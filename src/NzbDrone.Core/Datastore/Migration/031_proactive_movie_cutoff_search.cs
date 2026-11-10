using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(31)]
    public class proactive_movie_cutoff_search : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("Movies_ProactiveSearchActivity").AddColumn("CandidateType").AsString().Nullable();
            Alter.Table("Movies_ProactiveSearchActivity").AddColumn("CutoffUnmetMovies").AsInt32().NotNullable().WithDefaultValue(0);
        }
    }
}
