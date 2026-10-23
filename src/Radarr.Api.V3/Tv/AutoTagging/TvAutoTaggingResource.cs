using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using NzbDrone.Core.Tv.AutoTagging;
using NzbDrone.Core.Tv.AutoTagging.Specifications;
using Radarr.Http.ClientSchema;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Tv.AutoTagging
{
    public class TvAutoTaggingResource : RestResource
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public override int Id { get; set; }
        public string Name { get; set; }
        public bool RemoveTagsAutomatically { get; set; }
        public HashSet<int> Tags { get; set; }
        public List<TvAutoTaggingSpecificationSchema> Specifications { get; set; }
    }

    public static class TvAutoTaggingResourceMapper
    {
        public static TvAutoTaggingResource ToResource(this AutoTag model)
        {
            return new TvAutoTaggingResource
            {
                Id = model.Id,
                Name = model.Name,
                RemoveTagsAutomatically = model.RemoveTagsAutomatically,
                Tags = model.Tags,
                Specifications = model.Specifications.Select(x => x.ToSchema()).ToList()
            };
        }

        public static List<TvAutoTaggingResource> ToResource(this IEnumerable<AutoTag> models)
        {
            return models.Select(m => m.ToResource()).ToList();
        }

        public static AutoTag ToModel(this TvAutoTaggingResource resource, List<IAutoTaggingSpecification> specifications)
        {
            return new AutoTag
            {
                Id = resource.Id,
                Name = resource.Name,
                RemoveTagsAutomatically = resource.RemoveTagsAutomatically,
                Tags = resource.Tags,
                Specifications = resource.Specifications.Select(x => MapSpecification(x, specifications)).ToList()
            };
        }

        private static IAutoTaggingSpecification MapSpecification(TvAutoTaggingSpecificationSchema resource, List<IAutoTaggingSpecification> specifications)
        {
            var matchingSpec =
                specifications.SingleOrDefault(x => x.GetType().Name == resource.Implementation);

            if (matchingSpec is null)
            {
                throw new ArgumentException(
                    $"{resource.Implementation} is not a valid specification implementation");
            }

            var type = matchingSpec.GetType();

            // Finding the exact current specification isn't possible given the dynamic nature of them and the possibility that multiple
            // of the same type exist within the same format. Passing in null is safe as long as there never exists a specification that
            // relies on additional privacy.
            var spec = (IAutoTaggingSpecification)SchemaBuilder.ReadFromSchema(resource.Fields, type, null);
            spec.Name = resource.Name;
            spec.Negate = resource.Negate;
            spec.Required = resource.Required;
            return spec;
        }
    }
}
