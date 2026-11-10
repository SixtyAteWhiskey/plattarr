using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Tv.Search;
using Radarr.Http;
using Radarr.Http.REST;

namespace Radarr.Api.V3.ProactiveSearch
{
    public class ProactiveTvSearchConfigResource : RestResource
    {
        public bool Enabled { get; set; }
        public int IntervalMinutes { get; set; }
        public int SearchesPerRun { get; set; }
        public int CooldownHours { get; set; }
        public int FailureLimit { get; set; }
        public int FailureWindowDays { get; set; }
    }

    [V3ApiController("proactive-search/tv-config")]
    public class ProactiveTvSearchConfigController : Controller
    {
        private readonly IConfigService _config;

        public ProactiveTvSearchConfigController(IConfigService config)
        {
            _config = config;
        }

        [HttpGet]
        public Ok<ProactiveTvSearchConfigResource> Get()
        {
            return TypedResults.Ok(ToResource());
        }

        [HttpPut("{id:int}")]
        public Ok<ProactiveTvSearchConfigResource> Update(int id, [FromBody] ProactiveTvSearchConfigResource resource)
        {
            if (id != 1 || resource == null)
            {
                throw new BadRequestException("A TV proactive search configuration with id 1 is required.");
            }

            _config.ProactiveTvSearchEnabled = resource.Enabled;
            return TypedResults.Ok(ToResource());
        }

        private ProactiveTvSearchConfigResource ToResource()
        {
            return new ProactiveTvSearchConfigResource
            {
                Id = 1,
                Enabled = _config.ProactiveTvSearchEnabled,
                IntervalMinutes = 60,
                SearchesPerRun = 1,
                CooldownHours = 24,
                FailureLimit = 3,
                FailureWindowDays = 7
            };
        }
    }

    [V3ApiController("proactive-search/tv-activity")]
    public class ProactiveTvSearchActivityController : Controller
    {
        private readonly IProactiveTvSearchActivityRepository _activity;

        public ProactiveTvSearchActivityController(IProactiveTvSearchActivityRepository activity)
        {
            _activity = activity;
        }

        [HttpGet]
        public Ok<List<ProactiveTvSearchActivity>> Get()
        {
            return TypedResults.Ok(_activity.Recent(20));
        }
    }
}
