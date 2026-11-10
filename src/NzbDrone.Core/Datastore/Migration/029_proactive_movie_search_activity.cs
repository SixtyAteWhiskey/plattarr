using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(29)]
    public class proactive_movie_search_activity : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("Movies_ProactiveSearchActivity")
                .WithColumn("StartedAt").AsDateTimeOffset()
                .WithColumn("EndedAt").AsDateTimeOffset()
                .WithColumn("Outcome").AsString()
                .WithColumn("MovieId").AsInt32().Nullable()
                .WithColumn("MovieTitle").AsString().Nullable()
                .WithColumn("MissingMovies").AsInt32().NotNullable()
                .WithColumn("EligibleMovies").AsInt32().NotNullable()
                .WithColumn("GrabbedCount").AsInt32().NotNullable()
                .WithColumn("ErrorMessage").AsString().Nullable();
        }
    }
}
