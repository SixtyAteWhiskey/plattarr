using NzbDrone.Core.Movies.Indexers.Legacy;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class FixFutureIndexerStatusTimes : FixFutureProviderStatusTimes<IndexerStatus>, IHousekeepingTask
    {
        public FixFutureIndexerStatusTimes(IIndexerStatusRepository indexerStatusRepository)
            : base(indexerStatusRepository)
        {
        }
    }
}
