using NzbDrone.Core.Tv.Extras.Metadata;

namespace Radarr.Api.V3.Tv.Metadata
{
    public class TvMetadataResource : ProviderResource<TvMetadataResource>
    {
        public bool Enable { get; set; }
    }

    public class TvMetadataResourceMapper : ProviderResourceMapper<TvMetadataResource, MetadataDefinition>
    {
        public override TvMetadataResource ToResource(MetadataDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            var resource = base.ToResource(definition);

            resource.Enable = definition.Enable;

            return resource;
        }

        public override MetadataDefinition ToModel(TvMetadataResource resource, MetadataDefinition existingDefinition)
        {
            if (resource == null)
            {
                return null;
            }

            var definition = base.ToModel(resource, existingDefinition);

            definition.Enable = resource.Enable;

            return definition;
        }
    }
}
