using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Hygiene;
using Radarr.Http;

namespace Radarr.Api.V3.Hygiene
{
    [V3ApiController("hygiene/import-rescue")]
    public class HygieneImportRescueController : Controller
    {
        private readonly IHygieneImportRescueService _rescues;

        public HygieneImportRescueController(IHygieneImportRescueService rescues)
        {
            _rescues = rescues;
        }

        [HttpGet]
        public Ok<List<HygieneImportRescue>> Get([FromQuery] int limit = 100, [FromQuery] int offset = 0)
        {
            return TypedResults.Ok(_rescues.Recent(limit, offset));
        }
    }
}
