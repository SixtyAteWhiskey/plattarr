using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Qualities;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Music.Qualities
{
    public class AudioQualityDefinitionResource : RestResource
    {
        public AudioQuality Quality { get; set; }

        public string Title { get; set; }

        public int Weight { get; set; }

        public double? MinSize { get; set; }
        public double? MaxSize { get; set; }
        public double? PreferredSize { get; set; }
    }

    public static class AudioQualityDefinitionResourceMapper
    {
        public static AudioQualityDefinitionResource ToResource(this AudioQualityDefinition model)
        {
            if (model == null)
            {
                return null;
            }

            return new AudioQualityDefinitionResource
            {
                Id = model.Id,
                Quality = model.Quality,
                Title = model.Title,
                Weight = model.Weight,
                MinSize = model.MinSize,
                MaxSize = model.MaxSize,
                PreferredSize = model.PreferredSize
            };
        }

        public static AudioQualityDefinition ToModel(this AudioQualityDefinitionResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new AudioQualityDefinition
            {
                Id = resource.Id,
                Quality = resource.Quality,
                Title = resource.Title,
                Weight = resource.Weight,
                MinSize = resource.MinSize,
                MaxSize = resource.MaxSize,
                PreferredSize = resource.PreferredSize
            };
        }

        public static List<AudioQualityDefinitionResource> ToResource(this IEnumerable<AudioQualityDefinition> models)
        {
            return models.Select(ToResource).ToList();
        }

        public static List<AudioQualityDefinition> ToModel(this IEnumerable<AudioQualityDefinitionResource> resources)
        {
            return resources.Select(ToModel).ToList();
        }
    }
}
