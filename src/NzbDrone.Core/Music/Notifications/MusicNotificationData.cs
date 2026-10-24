using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Notifications;

namespace NzbDrone.Core.Music.Notifications
{
    // The Music module's own data on a notification message, as TvNotificationData is for TV. Step 5
    // sets it on the manual-interaction message only; step 6 adds the other events and the builders
    // (webhook payload, script environment) that read it.
    public class MusicNotificationData : IMediaNotificationData
    {
        public MediaKind Kind => MediaKind.Music;

        // Null on a manual-interaction message for a download that is not matched to an artist yet.
        public Artist Artist { get; set; }

        public RemoteAlbum RemoteAlbum { get; set; }
    }
}
