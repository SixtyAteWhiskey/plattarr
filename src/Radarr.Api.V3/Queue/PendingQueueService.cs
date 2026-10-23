using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Modules;
using MoviePending = NzbDrone.Core.Movies.Pending;
using TvPending = NzbDrone.Core.Tv.Pending;

namespace Radarr.Api.V3.Queue
{
    // The pending queue is kept per module (each module stores its own parsed release). The queue
    // API shows them together, so this joins them and sends a removal to the module that owns the item.
    public interface IPendingQueueService
    {
        List<NzbDrone.Core.Queue.Queue> GetPendingQueue();
        NzbDrone.Core.Queue.Queue FindPendingQueueItem(int queueId);
        void RemovePendingQueueItem(NzbDrone.Core.Queue.Queue item);
    }

    public class PendingQueueService : IPendingQueueService
    {
        private readonly MoviePending.IPendingReleaseService _moviePending;
        private readonly TvPending.IPendingReleaseService _tvPending;

        public PendingQueueService(MoviePending.IPendingReleaseService moviePending, TvPending.IPendingReleaseService tvPending)
        {
            _moviePending = moviePending;
            _tvPending = tvPending;
        }

        public List<NzbDrone.Core.Queue.Queue> GetPendingQueue()
        {
            return _moviePending.GetPendingQueue().Concat(_tvPending.GetPendingQueue()).ToList();
        }

        public NzbDrone.Core.Queue.Queue FindPendingQueueItem(int queueId)
        {
            return _moviePending.FindPendingQueueItem(queueId) ?? _tvPending.FindPendingQueueItem(queueId);
        }

        public void RemovePendingQueueItem(NzbDrone.Core.Queue.Queue item)
        {
            if (item.RemoteRelease?.Kind == MediaKind.Tv)
            {
                _tvPending.RemovePendingQueueItems(item.Id);
            }
            else
            {
                _moviePending.RemovePendingQueueItems(item.Id);
            }
        }
    }
}
