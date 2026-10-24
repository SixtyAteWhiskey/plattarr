using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Music.Parser.Model;

namespace NzbDrone.Core.Music
{
    // The core tracked download carries the module's match as a RemoteRelease; Music code reads it as a RemoteAlbum.
    public static class MusicTrackedDownloadExtensions
    {
        public static RemoteAlbum GetRemoteAlbum(this TrackedDownload trackedDownload)
        {
            return trackedDownload.RemoteRelease as RemoteAlbum;
        }

        public static Artist GetArtist(this TrackedDownload trackedDownload)
        {
            return trackedDownload.GetRemoteAlbum()?.Artist;
        }
    }
}
