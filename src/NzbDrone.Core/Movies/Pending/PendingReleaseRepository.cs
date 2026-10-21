using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Movies.Pending
{
    public interface IPendingReleaseRepository : IBasicRepository<PendingRelease>
    {
        void DeleteByMovieIds(List<int> movieIds);
        List<PendingRelease> AllByMovieId(int movieId);
        List<PendingRelease> WithoutFallback();
    }

    public class PendingReleaseRepository : MediaKindRepository<PendingRelease>, IPendingReleaseRepository
    {
        public PendingReleaseRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator, MediaKind.Movies)
        {
        }

        public void DeleteByMovieIds(List<int> movieIds)
        {
            Delete(x => movieIds.Contains(x.ParentId));
        }

        public List<PendingRelease> AllByMovieId(int movieId)
        {
            return Query(x => x.ParentId == movieId);
        }

        public List<PendingRelease> WithoutFallback()
        {
            var builder = Builder()
                .InnerJoin<PendingRelease, Movie>((p, m) => p.ParentId == m.Id)
                .Where<PendingRelease>(p => p.Reason != PendingReleaseReason.Fallback);

            return Query(builder);
        }
    }
}
