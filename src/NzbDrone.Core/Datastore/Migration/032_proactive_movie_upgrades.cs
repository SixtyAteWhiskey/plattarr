using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(32)]
    public class proactive_movie_upgrades : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("Movies_ProactiveUpgradeActivity")
                .WithColumn("StartedAt").AsDateTimeOffset()
                .WithColumn("EndedAt").AsDateTimeOffset()
                .WithColumn("Outcome").AsString()
                .WithColumn("Reason").AsString().Nullable()
                .WithColumn("MovieId").AsInt32().Nullable()
                .WithColumn("MovieTitle").AsString().Nullable()
                .WithColumn("CutoffUnmetMovies").AsInt32().NotNullable()
                .WithColumn("EligibleMovies").AsInt32().NotNullable()
                .WithColumn("GrabbedCount").AsInt32().NotNullable();
        }
    }
}
