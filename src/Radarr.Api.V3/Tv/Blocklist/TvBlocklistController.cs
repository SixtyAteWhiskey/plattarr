using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Tv.Blocklisting;
using NzbDrone.Core.Tv.CustomFormats;
using Radarr.Api.V3.Blocklist;
using Radarr.Http;
using Radarr.Http.Extensions;
using Radarr.Http.REST.Attributes;

namespace Radarr.Api.V3.Tv.Blocklist
{
    // Sonarr's blocklist controller. The movie controller owns /blocklist, and a delete there only sees
    // movie rows (each module's repository is scoped to its media kind), so the series blocklist is
    // served beside it at /blocklist/tv with its own delete routes.
    [V3ApiController("blocklist/tv")]
    public class TvBlocklistController : Controller
    {
        private readonly IBlocklistService _blocklistService;
        private readonly ITvCustomFormatCalculationService _formatCalculator;

        public TvBlocklistController(IBlocklistService blocklistService,
                                     ITvCustomFormatCalculationService formatCalculator)
        {
            _blocklistService = blocklistService;
            _formatCalculator = formatCalculator;
        }

        [HttpGet]
        [Produces("application/json")]
        public PagingResource<TvBlocklistResource> GetBlocklist([FromQuery] PagingRequestResource paging, [FromQuery] int[] seriesIds = null, [FromQuery] DownloadProtocol[] protocols = null)
        {
            var pagingResource = new PagingResource<TvBlocklistResource>(paging);
            var pagingSpec = pagingResource.MapToPagingSpec<TvBlocklistResource, NzbDrone.Core.Tv.Blocklisting.Blocklist>(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "date",
                    "indexer",
                    "series.sortTitle",
                    "sourceTitle"
                },
                "date",
                SortDirection.Descending);

            if (seriesIds?.Any() == true)
            {
                pagingSpec.FilterExpressions.Add(b => seriesIds.Contains(b.ParentId));
            }

            if (protocols?.Any() == true)
            {
                pagingSpec.FilterExpressions.Add(b => protocols.Contains(b.Protocol));
            }

            return pagingSpec.ApplyToPage(b => _blocklistService.Paged(pagingSpec), b => b.MapToResource(_formatCalculator));
        }

        [RestDeleteById]
        public Ok DeleteBlocklist(int id)
        {
            _blocklistService.Delete(id);

            return TypedResults.Ok();
        }

        [HttpDelete("bulk")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public Ok<object> Remove([FromBody] BlocklistBulkResource resource)
        {
            _blocklistService.Delete(resource.Ids);

            return TypedResults.Ok<object>(new { });
        }
    }
}
