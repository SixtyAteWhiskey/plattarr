using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    // Music step 7 (docs/module-interface.md, "Music step 7: as built"). Lidarr's notification events
    // for a deleted album, a failed download and a retagged file. The default keeps every existing
    // notification as it is.
    [Migration(11)]
    public class notification_music_events : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("Notifications").AddColumn("OnAlbumDelete").AsBoolean().NotNullable().WithDefaultValue(false);
            Alter.Table("Notifications").AddColumn("OnDownloadFailure").AsBoolean().NotNullable().WithDefaultValue(false);
            Alter.Table("Notifications").AddColumn("OnTrackRetag").AsBoolean().NotNullable().WithDefaultValue(false);
        }
    }
}
