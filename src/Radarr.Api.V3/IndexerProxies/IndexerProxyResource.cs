using NzbDrone.Core.IndexerProxies;

namespace Radarr.Api.V3.IndexerProxies
{
    public class IndexerProxyResource : ProviderResource<IndexerProxyResource>
    {
    }

    public class IndexerProxyResourceMapper : ProviderResourceMapper<IndexerProxyResource, IndexerProxyDefinition>
    {
    }

    public class IndexerProxyBulkResource : ProviderBulkResource<IndexerProxyBulkResource>
    {
    }

    public class IndexerProxyBulkResourceMapper : ProviderBulkResourceMapper<IndexerProxyBulkResource, IndexerProxyDefinition>
    {
    }
}
