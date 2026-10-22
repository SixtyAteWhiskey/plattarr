using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Movies.Parser.Model;

namespace NzbDrone.Core.Movies
{
    // The core tracked download carries the module's match as a RemoteRelease; movie code reads it as a RemoteMovie.
    public static class MovieTrackedDownloadExtensions
    {
        public static RemoteMovie GetRemoteMovie(this TrackedDownload trackedDownload)
        {
            return trackedDownload.RemoteRelease as RemoteMovie;
        }

        public static Movie GetMovie(this TrackedDownload trackedDownload)
        {
            return trackedDownload.GetRemoteMovie()?.Movie;
        }
    }
}
