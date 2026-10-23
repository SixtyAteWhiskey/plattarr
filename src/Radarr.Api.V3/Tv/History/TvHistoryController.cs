using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.CustomFormats;
using NzbDrone.Core.Tv.History;
using Radarr.Api.V3.Tv.Episodes;
using Radarr.Api.V3.Tv.Series;
using Radarr.Http;
using Radarr.Http.Extensions;

namespace Radarr.Api.V3.Tv.History
{
    // Sonarr's history controller. The movie controller owns /history, so the series history is served
    // beside it at /history/tv.
    [V3ApiController("history/tv")]
    public class TvHistoryController : Controller
    {
        private readonly IHistoryService _historyService;
        private readonly ITvCustomFormatCalculationService _formatCalculator;
        private readonly IUpgradableSpecification _upgradableSpecification;
        private readonly IFailedDownloadService _failedDownloadService;
        private readonly ISeriesService _seriesService;
        private readonly IEpisodeService _episodeService;

        public TvHistoryController(IHistoryService historyService,
                                   ITvCustomFormatCalculationService formatCalculator,
                                   IUpgradableSpecification upgradableSpecification,
                                   IFailedDownloadService failedDownloadService,
                                   ISeriesService seriesService,
                                   IEpisodeService episodeService)
        {
            _historyService = historyService;
            _formatCalculator = formatCalculator;
            _upgradableSpecification = upgradableSpecification;
            _failedDownloadService = failedDownloadService;
            _seriesService = seriesService;
            _episodeService = episodeService;
        }

        protected EpisodeHistoryResource MapToResource(EpisodeHistory model, bool includeSeries, bool includeEpisode)
        {
            model.Series ??= _seriesService.GetSeries(model.ParentId);

            var resource = model.ToResource(_formatCalculator);

            if (includeSeries)
            {
                resource.Series = model.Series.ToResource();
            }

            if (includeEpisode)
            {
                model.Episode ??= _episodeService.GetEpisode(model.ItemId);
                resource.Episode = model.Episode.ToResource();
            }

            if (model.Series != null)
            {
                resource.QualityCutoffNotMet = _upgradableSpecification.QualityCutoffNotMet(model.Series.QualityProfile.Value, model.Quality);
            }

            return resource;
        }

        [HttpGet]
        [Produces("application/json")]
        public PagingResource<EpisodeHistoryResource> GetHistory([FromQuery] PagingRequestResource paging, bool includeSeries, bool includeEpisode, [FromQuery(Name = "eventType")] int[] eventTypes, int? episodeId, string downloadId, [FromQuery] int[] seriesIds = null, [FromQuery] int[] languages = null, [FromQuery] int[] quality = null)
        {
            var pagingResource = new PagingResource<EpisodeHistoryResource>(paging);
            var pagingSpec = pagingResource.MapToPagingSpec<EpisodeHistoryResource, EpisodeHistory>(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "date",
                    "series.sortTitle"
                },
                "date",
                SortDirection.Descending);

            if (eventTypes != null && eventTypes.Any())
            {
                pagingSpec.FilterExpressions.Add(v => eventTypes.Contains((int)v.EventType));
            }

            if (episodeId.HasValue)
            {
                pagingSpec.FilterExpressions.Add(h => h.ItemId == episodeId);
            }

            if (downloadId.IsNotNullOrWhiteSpace())
            {
                pagingSpec.FilterExpressions.Add(h => h.DownloadId == downloadId);
            }

            if (seriesIds != null && seriesIds.Any())
            {
                pagingSpec.FilterExpressions.Add(h => seriesIds.Contains(h.ParentId));
            }

            return pagingSpec.ApplyToPage(h => _historyService.Paged(pagingSpec, languages, quality), h => MapToResource(h, includeSeries, includeEpisode));
        }

        [HttpGet("since")]
        [Produces("application/json")]
        public List<EpisodeHistoryResource> GetHistorySince(DateTime date, EpisodeHistoryEventType? eventType = null, bool includeSeries = false, bool includeEpisode = false)
        {
            return _historyService.Since(date, eventType).Select(h => MapToResource(h, includeSeries, includeEpisode)).ToList();
        }

        [HttpGet("series")]
        [Produces("application/json")]
        public List<EpisodeHistoryResource> GetSeriesHistory(int seriesId, int? seasonNumber, EpisodeHistoryEventType? eventType = null, bool includeSeries = false, bool includeEpisode = false)
        {
            var series = _seriesService.GetSeries(seriesId);

            if (seasonNumber.HasValue)
            {
                return _historyService.GetBySeason(seriesId, seasonNumber.Value, eventType).Select(h =>
                {
                    h.Series = series;

                    return MapToResource(h, includeSeries, includeEpisode);
                }).ToList();
            }

            return _historyService.GetBySeries(seriesId, eventType).Select(h =>
            {
                h.Series = series;

                return MapToResource(h, includeSeries, includeEpisode);
            }).ToList();
        }

        [HttpPost("failed/{id}")]
        public object MarkAsFailed([FromRoute] int id)
        {
            _failedDownloadService.MarkAsFailed(MediaKind.Tv, id);
            return new { };
        }
    }
}
