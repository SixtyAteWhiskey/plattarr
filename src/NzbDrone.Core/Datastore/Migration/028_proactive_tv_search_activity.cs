using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(28)]
    public class proactive_tv_search_activity : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("Tv_ProactiveSearchActivity")
                .WithColumn("StartedAt").AsDateTimeOffset()
                .WithColumn("EndedAt").AsDateTimeOffset()
                .WithColumn("Outcome").AsString()
                .WithColumn("SeriesId").AsInt32().Nullable()
                .WithColumn("SeriesTitle").AsString().Nullable()
                .WithColumn("EpisodeId").AsInt32().Nullable()
                .WithColumn("MissingEpisodes").AsInt32().NotNullable()
                .WithColumn("EligibleEpisodes").AsInt32().NotNullable()
                .WithColumn("GrabbedCount").AsInt32().NotNullable();
        }
    }
}
