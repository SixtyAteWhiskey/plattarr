using System.Collections.Generic;
using NzbDrone.Core.Tv.AutoTagging.Specifications;
using Radarr.Http.ClientSchema;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Tv.AutoTagging
{
    public class TvAutoTaggingSpecificationSchema : RestResource
    {
        public string Name { get; set; }
        public string Implementation { get; set; }
        public string ImplementationName { get; set; }
        public bool Negate { get; set; }
        public bool Required { get; set; }
        public List<Field> Fields { get; set; }
    }

    public static class TvAutoTaggingSpecificationSchemaMapper
    {
        public static TvAutoTaggingSpecificationSchema ToSchema(this IAutoTaggingSpecification model)
        {
            return new TvAutoTaggingSpecificationSchema
            {
                Name = model.Name,
                Implementation = model.GetType().Name,
                ImplementationName = model.ImplementationName,
                Negate = model.Negate,
                Required = model.Required,
                Fields = SchemaBuilder.ToSchema(model)
            };
        }
    }
}
