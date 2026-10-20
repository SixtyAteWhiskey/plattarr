using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.IndexerProxies;

namespace NzbDrone.Core.Indexers
{
    public interface IIndexerMediaTagService
    {
        // The indexer's tags that restrict which media it serves.
        HashSet<int> GetMediaTags(IndexerDefinition indexer);

        // True when the indexer has no media tags, or shares one with the media item.
        bool Allows(IndexerDefinition indexer, IEnumerable<int> mediaTags);
    }

    // Indexer tags have two meanings after the merge. In Radarr, Sonarr, and Lidarr a tag
    // on an indexer restricts it to media with the same tag. In Prowlarr a tag on an
    // indexer selects an indexer proxy (FlareSolverr, HTTP, SOCKS). Treating a proxy tag
    // as a media restriction would stop the indexer from searching anything that isn't
    // tagged the same way, so a tag that any indexer proxy uses never restricts media.
    public class IndexerMediaTagService : IIndexerMediaTagService
    {
        private readonly IIndexerProxyFactory _indexerProxyFactory;

        public IndexerMediaTagService(IIndexerProxyFactory indexerProxyFactory)
        {
            _indexerProxyFactory = indexerProxyFactory;
        }

        public HashSet<int> GetMediaTags(IndexerDefinition indexer)
        {
            var tags = new HashSet<int>(indexer.Tags);

            if (tags.Count == 0)
            {
                return tags;
            }

            var proxyTags = _indexerProxyFactory.All().SelectMany(p => p.Tags);
            tags.ExceptWith(proxyTags);

            return tags;
        }

        public bool Allows(IndexerDefinition indexer, IEnumerable<int> mediaTags)
        {
            var indexerTags = GetMediaTags(indexer);

            return indexerTags.Count == 0 || indexerTags.Overlaps(mediaTags);
        }
    }
}
