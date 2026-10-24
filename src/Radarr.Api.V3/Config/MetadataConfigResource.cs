using NzbDrone.Core.Configuration;
using NzbDrone.Core.Movies.MetadataSource.SkyHook.Resource;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Config
{
    public class MetadataConfigResource : RestResource
    {
        public TMDbCountryCode CertificationCountry { get; set; }

        // Music: base URL of the artist and album metadata service; empty uses the Lidarr project's.
        public string MusicMetadataSource { get; set; }
    }

    public static class MetadataConfigResourceMapper
    {
        public static MetadataConfigResource ToResource(IConfigService model)
        {
            return new MetadataConfigResource
            {
                CertificationCountry = model.CertificationCountry,
                MusicMetadataSource = model.MusicMetadataSource,
            };
        }
    }
}
