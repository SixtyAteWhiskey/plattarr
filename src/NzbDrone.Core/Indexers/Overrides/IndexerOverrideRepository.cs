using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Indexers.Overrides
{
    public interface IIndexerOverrideRepository : IBasicRepository<IndexerOverride>
    {
        void DeleteByIndexerId(int indexerId);
    }

    public class IndexerOverrideRepository : BasicRepository<IndexerOverride>, IIndexerOverrideRepository
    {
        public IndexerOverrideRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public void DeleteByIndexerId(int indexerId)
        {
            Delete(o => o.IndexerId == indexerId);
        }
    }
}
