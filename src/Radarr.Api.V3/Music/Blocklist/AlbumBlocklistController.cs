using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Music.Blocklisting;
using NzbDrone.Core.Music.CustomFormats;
using Radarr.Api.V3.Blocklist;
using Radarr.Http;
using Radarr.Http.Extensions;
using Radarr.Http.REST.Attributes;

namespace Radarr.Api.V3.Music.Blocklist
{
    // Lidarr's blocklist controller. The movie controller owns /blocklist, and a delete there only sees
    // movie rows (each module's repository is scoped to its media kind), so the Music blocklist is
    // served beside it at /blocklist/music with its own delete routes.
    [V3ApiController("blocklist/music")]
    public class AlbumBlocklistController : Controller
    {
        private readonly IBlocklistService _blocklistService;
        private readonly IMusicCustomFormatCalculationService _formatCalculator;

        public AlbumBlocklistController(IBlocklistService blocklistService,
                                        IMusicCustomFormatCalculationService formatCalculator)
        {
            _blocklistService = blocklistService;
            _formatCalculator = formatCalculator;
        }

        [HttpGet]
        [Produces("application/json")]
        public PagingResource<AlbumBlocklistResource> GetBlocklist([FromQuery] PagingRequestResource paging, [FromQuery] int[] artistIds = null, [FromQuery] DownloadProtocol[] protocols = null)
        {
            var pagingResource = new PagingResource<AlbumBlocklistResource>(paging);
            var pagingSpec = pagingResource.MapToPagingSpec<AlbumBlocklistResource, NzbDrone.Core.Music.Blocklisting.Blocklist>(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "date",
                    "indexer",
                    "artists.sortName",
                    "sourceTitle"
                },
                "date",
                SortDirection.Descending);

            if (artistIds?.Any() == true)
            {
                pagingSpec.FilterExpressions.Add(b => artistIds.Contains(b.ParentId));
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
