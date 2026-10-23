using NzbDrone.Core.Modules;

namespace Radarr.Api.V3.Queue
{
    public static class QueueItemExtensions
    {
        // True when a module matched the download to a movie, series or album in the library.
        public static bool IsMatched(this NzbDrone.Core.Queue.Queue queue)
        {
            return queue.RemoteRelease?.Parent != null;
        }

        public static bool IsKind(this NzbDrone.Core.Queue.Queue queue, MediaKind kind)
        {
            return queue.RemoteRelease?.Kind == kind;
        }
    }
}
