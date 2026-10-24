using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Music.Pending
{
    public interface IPendingReleaseRepository : IBasicRepository<PendingRelease>
    {
        void DeleteByArtistIds(List<int> artistIds);
        List<PendingRelease> AllByArtistId(int artistId);
        List<PendingRelease> WithoutFallback();
    }

    public class PendingReleaseRepository : MediaKindRepository<PendingRelease>, IPendingReleaseRepository
    {
        public PendingReleaseRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator, MediaKind.Music)
        {
        }

        public void DeleteByArtistIds(List<int> artistIds)
        {
            Delete(r => artistIds.Contains(r.ParentId));
        }

        public List<PendingRelease> AllByArtistId(int artistId)
        {
            return Query(p => p.ParentId == artistId);
        }

        public List<PendingRelease> WithoutFallback()
        {
            var builder = Builder()
                .InnerJoin<PendingRelease, Artist>((p, a) => p.ParentId == a.Id)
                .Where<PendingRelease>(p => p.Reason != PendingReleaseReason.Fallback);

            return Query(builder);
        }
    }
}
