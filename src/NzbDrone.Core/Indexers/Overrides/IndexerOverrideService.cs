using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Cache;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.ThingiProvider.Events;

namespace NzbDrone.Core.Indexers.Overrides
{
    public interface IIndexerOverrideService
    {
        // Never null. An indexer without a row gets an empty override (nothing set).
        IndexerOverride Get(int indexerId, MediaKind mediaKind);
        List<IndexerOverride> All();
        IndexerOverride Upsert(IndexerOverride indexerOverride);
        void Delete(int id);
    }

    public class IndexerOverrideService : IIndexerOverrideService, IHandle<ProviderDeletedEvent<IIndexer>>
    {
        private readonly IIndexerOverrideRepository _repository;
        private readonly ICached<List<IndexerOverride>> _cache;

        public IndexerOverrideService(IIndexerOverrideRepository repository, ICacheManager cacheManager)
        {
            _repository = repository;
            _cache = cacheManager.GetRollingCache<List<IndexerOverride>>(GetType(), "overrides", TimeSpan.FromHours(1));
        }

        public IndexerOverride Get(int indexerId, MediaKind mediaKind)
        {
            return All().FirstOrDefault(o => o.IndexerId == indexerId && o.MediaKind == mediaKind)
                ?? new IndexerOverride { IndexerId = indexerId, MediaKind = mediaKind };
        }

        public List<IndexerOverride> All()
        {
            return _cache.Get("all", () => _repository.All().ToList());
        }

        public IndexerOverride Upsert(IndexerOverride indexerOverride)
        {
            var existing = _repository.All().FirstOrDefault(o => o.IndexerId == indexerOverride.IndexerId && o.MediaKind == indexerOverride.MediaKind);

            if (existing != null)
            {
                indexerOverride.Id = existing.Id;
            }

            var result = _repository.Upsert(indexerOverride);
            _cache.Clear();

            return result;
        }

        public void Delete(int id)
        {
            _repository.Delete(id);
            _cache.Clear();
        }

        public void Handle(ProviderDeletedEvent<IIndexer> message)
        {
            _repository.DeleteByIndexerId(message.ProviderId);
            _cache.Clear();
        }
    }
}
