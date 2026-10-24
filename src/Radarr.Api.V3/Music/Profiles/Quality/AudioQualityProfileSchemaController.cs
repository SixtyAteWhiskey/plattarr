using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Music.Profiles;
using Radarr.Http;

namespace Radarr.Api.V3.Music.Profiles.Quality
{
    [V3ApiController("music/qualityprofile/schema")]
    public class AudioQualityProfileSchemaController : Controller
    {
        private readonly IAudioQualityProfileService _profileService;

        public AudioQualityProfileSchemaController(IAudioQualityProfileService profileService)
        {
            _profileService = profileService;
        }

        [HttpGet]
        public AudioQualityProfileResource GetSchema()
        {
            var qualityProfile = _profileService.GetDefaultProfile(string.Empty);

            return qualityProfile.ToResource();
        }
    }
}
