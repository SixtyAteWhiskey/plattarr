using NzbDrone.Core.IndexerProxies;
using NzbDrone.SignalR;
using Radarr.Http;

namespace Radarr.Api.V3.IndexerProxies
{
    // From Prowlarr.Api.V1 (26a605e). Indexer proxies (FlareSolverr, HTTP, SOCKS) apply to
    // indexers that share a tag with them.
    [V3ApiController]
    public class IndexerProxyController : ProviderControllerBase<IndexerProxyResource, IndexerProxyBulkResource, IIndexerProxy, IndexerProxyDefinition>
    {
        public static readonly IndexerProxyResourceMapper ResourceMapper = new();
        public static readonly IndexerProxyBulkResourceMapper BulkResourceMapper = new();

        public IndexerProxyController(IBroadcastSignalRMessage signalRBroadcaster, IndexerProxyFactory indexerProxyFactory)
            : base(signalRBroadcaster, indexerProxyFactory, "indexerProxy", ResourceMapper, BulkResourceMapper)
        {
        }
    }
}
