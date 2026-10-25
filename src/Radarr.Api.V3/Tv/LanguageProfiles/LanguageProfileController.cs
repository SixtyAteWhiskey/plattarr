using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Radarr.Http;

namespace Radarr.Api.V3.Tv.LanguageProfiles
{
    [V3ApiController("languageprofile")]
    public class LanguageProfileController : Controller
    {
        [HttpGet]
        [Produces("application/json")]
        public List<LanguageProfileResource> GetLanguageProfiles()
        {
            return new List<LanguageProfileResource>
            {
                new LanguageProfileResource { Id = 1, Name = "English" }
            };
        }
    }

    public class LanguageProfileResource
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
