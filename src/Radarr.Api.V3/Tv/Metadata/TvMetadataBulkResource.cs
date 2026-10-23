using NzbDrone.Core.Tv.Extras.Metadata;

namespace Radarr.Api.V3.Tv.Metadata
{
    public class TvMetadataBulkResource : ProviderBulkResource<TvMetadataBulkResource>
    {
    }

    public class TvMetadataBulkResourceMapper : ProviderBulkResourceMapper<TvMetadataBulkResource, MetadataDefinition>
    {
    }
}
