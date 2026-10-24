using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Qualities;
using Radarr.Api.V3.Profiles.Quality;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Music.Profiles.Quality
{
    // Music's quality profile (Lidarr's QualityProfileResource). The qualities are the audio
    // catalog's (IDs 201 to 399); the custom format items are the shared ones.
    public class AudioQualityProfileResource : RestResource
    {
        public string Name { get; set; }
        public bool UpgradeAllowed { get; set; }
        public int Cutoff { get; set; }
        public List<AudioQualityProfileQualityItemResource> Items { get; set; }
        public int MinFormatScore { get; set; }
        public int CutoffFormatScore { get; set; }
        public List<ProfileFormatItemResource> FormatItems { get; set; }
    }

    public class AudioQualityProfileQualityItemResource : RestResource
    {
        public AudioQualityProfileQualityItemResource()
        {
            Items = new List<AudioQualityProfileQualityItemResource>();
        }

        public string Name { get; set; }
        public AudioQuality Quality { get; set; }
        public List<AudioQualityProfileQualityItemResource> Items { get; set; }
        public bool Allowed { get; set; }
    }

    public static class AudioQualityProfileResourceMapper
    {
        public static AudioQualityProfileResource ToResource(this AudioQualityProfile model)
        {
            if (model == null)
            {
                return null;
            }

            return new AudioQualityProfileResource
            {
                Id = model.Id,
                Name = model.Name,
                UpgradeAllowed = model.UpgradeAllowed,
                Cutoff = model.Cutoff,
                Items = model.Items.ConvertAll(ToResource),
                MinFormatScore = model.MinFormatScore,
                CutoffFormatScore = model.CutoffFormatScore,
                FormatItems = model.FormatItems.ConvertAll(ProfileResourceMapper.ToResource)
            };
        }

        public static AudioQualityProfileQualityItemResource ToResource(this AudioQualityProfileItem model)
        {
            if (model == null)
            {
                return null;
            }

            return new AudioQualityProfileQualityItemResource
            {
                Id = model.Id,
                Name = model.Name,
                Quality = model.Quality,
                Items = model.Items.ConvertAll(ToResource),
                Allowed = model.Allowed
            };
        }

        public static AudioQualityProfile ToModel(this AudioQualityProfileResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new AudioQualityProfile
            {
                Id = resource.Id,
                Name = resource.Name,
                UpgradeAllowed = resource.UpgradeAllowed,
                Cutoff = resource.Cutoff,
                Items = resource.Items.ConvertAll(ToModel),
                MinFormatScore = resource.MinFormatScore,
                CutoffFormatScore = resource.CutoffFormatScore,
                FormatItems = resource.FormatItems.ConvertAll(ProfileResourceMapper.ToModel)
            };
        }

        public static AudioQualityProfileItem ToModel(this AudioQualityProfileQualityItemResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new AudioQualityProfileItem
            {
                Id = resource.Id,
                Name = resource.Name,
                Quality = resource.Quality != null ? (AudioQuality)resource.Quality.Id : null,
                Items = resource.Items.ConvertAll(ToModel),
                Allowed = resource.Allowed
            };
        }

        public static List<AudioQualityProfileResource> ToResource(this IEnumerable<AudioQualityProfile> models)
        {
            return models.Select(ToResource).ToList();
        }
    }
}
