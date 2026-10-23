using System.Collections.Generic;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv.MediaFiles;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv.Notifications
{
    // The TV module's own data on a notification message, as MovieNotificationData is for movies. Its
    // builders (webhook, script) read it; which fields are set depends on the event.
    public class TvNotificationData : IMediaNotificationData
    {
        public MediaKind Kind => MediaKind.Tv;

        // Null on a manual-interaction message for a download that is not matched to a series yet.
        public Series Series { get; set; }

        public RemoteEpisode RemoteEpisode { get; set; }
        public QualityModel Quality { get; set; }
        public LocalEpisode LocalEpisode { get; set; }
        public EpisodeFile EpisodeFile { get; set; }
        public List<DeletedEpisodeFile> OldEpisodeFiles { get; set; } = new();
        public List<RenamedEpisodeFile> RenamedFiles { get; set; } = new();
        public GrabbedReleaseInfo Release { get; set; }
    }
}
