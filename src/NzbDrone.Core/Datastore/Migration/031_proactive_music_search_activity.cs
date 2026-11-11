using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(31)]
    public class proactive_music_search_activity : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("Music_ProactiveSearchActivity")
                .WithColumn("StartedAt").AsDateTimeOffset()
                .WithColumn("EndedAt").AsDateTimeOffset()
                .WithColumn("Outcome").AsString()
                .WithColumn("Reason").AsString().Nullable()
                .WithColumn("AlbumId").AsInt32().Nullable()
                .WithColumn("AlbumTitle").AsString().Nullable()
                .WithColumn("MissingAlbums").AsInt32().NotNullable()
                .WithColumn("EligibleAlbums").AsInt32().NotNullable()
                .WithColumn("GrabbedCount").AsInt32().NotNullable();
        }
    }
}
