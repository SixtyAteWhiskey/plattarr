using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Configuration;
using Radarr.Http;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Hygiene
{
    public class HygieneConfigResource : RestResource
    {
        public bool ActionsEnabled { get; set; }
    }

    [V3ApiController("hygiene/config")]
    public class HygieneConfigController : Controller
    {
        private readonly IConfigService _config;

        public HygieneConfigController(IConfigService config)
        {
            _config = config;
        }

        [HttpGet]
        public Ok<HygieneConfigResource> Get()
        {
            return TypedResults.Ok(new HygieneConfigResource { Id = 1, ActionsEnabled = _config.HygieneActionsEnabled });
        }

        [HttpPut("{id:int}")]
        public Ok<HygieneConfigResource> Update(int id, [FromBody] HygieneConfigResource resource)
        {
            if (id != 1 || resource == null)
            {
                throw new BadRequestException("A Hygiene configuration resource with id 1 is required.");
            }

            _config.HygieneActionsEnabled = resource.ActionsEnabled;
            return TypedResults.Ok(new HygieneConfigResource { Id = 1, ActionsEnabled = _config.HygieneActionsEnabled });
        }
    }
}
