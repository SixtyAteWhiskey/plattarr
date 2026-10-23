using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Tv.Pending
{
    public interface IPendingReleaseRepository : IBasicRepository<PendingRelease>
    {
        void DeleteBySeriesIds(List<int> seriesIds);
        List<PendingRelease> AllBySeriesId(int seriesId);
        List<PendingRelease> WithoutFallback();
    }

    public class PendingReleaseRepository : MediaKindRepository<PendingRelease>, IPendingReleaseRepository
    {
        public PendingReleaseRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator, MediaKind.Tv)
        {
        }

        public void DeleteBySeriesIds(List<int> seriesIds)
        {
            Delete(r => seriesIds.Contains(r.ParentId));
        }

        public List<PendingRelease> AllBySeriesId(int seriesId)
        {
            return Query(p => p.ParentId == seriesId);
        }

        public List<PendingRelease> WithoutFallback()
        {
            var builder = Builder()
                .InnerJoin<PendingRelease, Series>((p, s) => p.ParentId == s.Id)
                .Where<PendingRelease>(p => p.Reason != PendingReleaseReason.Fallback);

            return Query(builder);
        }
    }
}
