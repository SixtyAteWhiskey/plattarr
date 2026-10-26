using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Hygiene;
using Radarr.Http;

namespace Radarr.Api.V3.Hygiene
{
    [V3ApiController("hygiene/action")]
    public class HygieneActionController : Controller
    {
        private readonly IHygieneQueueActionService _actions;

        public HygieneActionController(IHygieneQueueActionService actions)
        {
            _actions = actions;
        }

        [HttpGet]
        public Ok<List<HygieneAction>> Get([FromQuery] int limit = 100)
        {
            return TypedResults.Ok(_actions.Recent(limit));
        }
    }
}
