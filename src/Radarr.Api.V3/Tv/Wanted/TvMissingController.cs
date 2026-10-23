using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.CustomFormats;
using NzbDrone.SignalR;
using Radarr.Api.V3.Tv.Episodes;
using Radarr.Http;
using Radarr.Http.Extensions;

namespace Radarr.Api.V3.Tv.Wanted
{
    // Sonarr's wanted/missing. The movie controller owns /wanted/missing, so the missing episodes are
    // served beside it at /wanted/missing/tv.
    [V3ApiController("wanted/missing/tv")]
    public class TvMissingController : EpisodeControllerWithSignalR
    {
        public TvMissingController(IEpisodeService episodeService,
                                   ISeriesService seriesService,
                                   IUpgradableSpecification upgradableSpecification,
                                   ITvCustomFormatCalculationService formatCalculator,
                                   IBroadcastSignalRMessage signalRBroadcaster)
            : base(episodeService, seriesService, upgradableSpecification, formatCalculator, signalRBroadcaster)
        {
        }

        [NonAction]
        public override Results<Ok<EpisodeResource>, NotFound> GetResourceByIdWithErrorHandler(int id)
        {
            throw new NotImplementedException();
        }

        [HttpGet]
        [Produces("application/json")]
        public PagingResource<EpisodeResource> GetMissingEpisodes([FromQuery] PagingRequestResource paging, bool includeSeries = false, bool includeImages = false, bool monitored = true)
        {
            var pagingResource = new PagingResource<EpisodeResource>(paging);
            var pagingSpec = pagingResource.MapToPagingSpec<EpisodeResource, Episode>(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "episodes.airDateUtc",
                    "episodes.lastSearchTime",
                    "series.sortTitle"
                },
                "episodes.airDateUtc",
                SortDirection.Ascending);

            if (monitored)
            {
                pagingSpec.FilterExpressions.Add(v => v.Monitored == true && v.Series.Monitored == true);
            }
            else
            {
                pagingSpec.FilterExpressions.Add(v => v.Monitored == false || v.Series.Monitored == false);
            }

            return pagingSpec.ApplyToPage(spec => _episodeService.EpisodesWithoutFiles(spec, true), v => MapToResource(v, includeSeries, false, includeImages));
        }
    }
}
