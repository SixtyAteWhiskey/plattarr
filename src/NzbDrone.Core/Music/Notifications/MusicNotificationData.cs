using System.Collections.Generic;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Notifications;

namespace NzbDrone.Core.Music.Notifications
{
    // The Music module's own data on a notification message, as TvNotificationData is for TV. Its
    // builders (webhook, script) read it; which fields are set depends on the event.
    public class MusicNotificationData : IMediaNotificationData
    {
        public MediaKind Kind => MediaKind.Music;

        // Null on a manual-interaction message for a download that is not matched to an artist yet.
        public Artist Artist { get; set; }

        // Grab and manual interaction: the release and the albums it covers.
        public RemoteAlbum RemoteAlbum { get; set; }

        // Download: the album that was imported, its release, the files that came in and the ones they replaced.
        public Album Album { get; set; }
        public AlbumRelease AlbumRelease { get; set; }
        public List<TrackFile> TrackFiles { get; set; } = new();
        public List<TrackFile> OldTrackFiles { get; set; } = new();

        // Rename.
        public List<RenamedTrackFile> RenamedFiles { get; set; } = new();
    }
}
