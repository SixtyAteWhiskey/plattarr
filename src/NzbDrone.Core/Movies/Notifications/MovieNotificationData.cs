using System.Collections.Generic;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Movies.Notifications
{
    // The Movies module's own data on a notification message. Its builders (webhook, script, Trakt) read it;
    // which fields are set depends on the event.
    public class MovieNotificationData : IMediaNotificationData
    {
        public MediaKind Kind => MediaKind.Movies;

        // Null on a manual-interaction message for a download that is not matched to a movie yet.
        public Movie Movie { get; set; }

        public RemoteMovie RemoteMovie { get; set; }
        public QualityModel Quality { get; set; }
        public LocalMovie LocalMovie { get; set; }
        public MovieFile MovieFile { get; set; }
        public List<DeletedMovieFile> OldMovieFiles { get; set; } = new();
        public List<RenamedMovieFile> RenamedFiles { get; set; } = new();
        public GrabbedReleaseInfo Release { get; set; }
    }
}
