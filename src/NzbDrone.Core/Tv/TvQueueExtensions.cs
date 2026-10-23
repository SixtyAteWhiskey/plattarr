using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv
{
    // The core queue carries the module's match as a RemoteRelease; TV code reads it as a RemoteEpisode.
    public static class TvQueueExtensions
    {
        public static RemoteEpisode GetRemoteEpisode(this Queue.Queue queue)
        {
            return queue.RemoteRelease as RemoteEpisode;
        }

        public static Series GetSeries(this Queue.Queue queue)
        {
            return queue.GetRemoteEpisode()?.Series;
        }
    }
}
