using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv
{
    // The core tracked download carries the module's match as a RemoteRelease; TV code reads it as a RemoteEpisode.
    public static class TvTrackedDownloadExtensions
    {
        public static RemoteEpisode GetRemoteEpisode(this TrackedDownload trackedDownload)
        {
            return trackedDownload.RemoteRelease as RemoteEpisode;
        }

        public static Series GetSeries(this TrackedDownload trackedDownload)
        {
            return trackedDownload.GetRemoteEpisode()?.Series;
        }
    }
}
