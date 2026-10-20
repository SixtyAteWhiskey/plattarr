using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Cache;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.ThingiProvider.Events;

namespace NzbDrone.Core.Indexers
{
    // The indexer settings the download pipeline reads on every grab and import, cached.
    // Radarr kept the same cache over its own indexer layer.
    public interface ICachedIndexerSettingsProvider
    {
        CachedIndexerSettings GetSettings(int indexerId);
    }

    public class CachedIndexerSettingsProvider : ICachedIndexerSettingsProvider, IHandle<ProviderUpdatedEvent<IIndexer>>, IHandle<ProviderDeletedEvent<IIndexer>>
    {
        private readonly IIndexerFactory _indexerFactory;
        private readonly ICached<CachedIndexerSettings> _cache;

        public CachedIndexerSettingsProvider(IIndexerFactory indexerFactory, ICacheManager cacheManager)
        {
            _indexerFactory = indexerFactory;
            _cache = cacheManager.GetRollingCache<CachedIndexerSettings>(GetType(), "settingsByIndexer", TimeSpan.FromHours(1));
        }

        public CachedIndexerSettings GetSettings(int indexerId)
        {
            if (indexerId == 0)
            {
                return null;
            }

            return _cache.Get(indexerId.ToString(), () => FetchIndexerSettings(indexerId));
        }

        private CachedIndexerSettings FetchIndexerSettings(int indexerId)
        {
            var indexer = _indexerFactory.Find(indexerId);

            if (indexer?.Settings is not IIndexerSettings indexerSettings)
            {
                return null;
            }

            return new CachedIndexerSettings
            {
                FailDownloads = (indexerSettings.BaseSettings?.FailDownloads ?? Array.Empty<int>()).Select(f => (FailDownloads)f).ToHashSet(),
                TorrentBaseSettings = (indexer.Settings as ITorrentIndexerSettings)?.TorrentBaseSettings
            };
        }

        public void Handle(ProviderUpdatedEvent<IIndexer> message)
        {
            _cache.Clear();
        }

        public void Handle(ProviderDeletedEvent<IIndexer> message)
        {
            _cache.Clear();
        }
    }

    public class CachedIndexerSettings
    {
        public HashSet<FailDownloads> FailDownloads { get; set; }

        // Null for Usenet indexers.
        public IndexerTorrentBaseSettings TorrentBaseSettings { get; set; }
    }
}
