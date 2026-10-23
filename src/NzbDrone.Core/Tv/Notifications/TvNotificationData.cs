using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv.Notifications
{
    // The TV module's own data on a notification message, as MovieNotificationData is for movies.
    // Step 5 fills what the manual-interaction message needs; the builders and the other events
    // (grab, import, rename, delete) arrive with TV step 6 and add their fields here.
    public class TvNotificationData : IMediaNotificationData
    {
        public MediaKind Kind => MediaKind.Tv;

        // Null on a manual-interaction message for a download that is not matched to a series yet.
        public Series Series { get; set; }

        public RemoteEpisode RemoteEpisode { get; set; }
        public GrabbedReleaseInfo Release { get; set; }
    }
}
