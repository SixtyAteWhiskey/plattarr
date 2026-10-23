using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.CustomFormats;
using NzbDrone.SignalR;
using Radarr.Api.V3.Tv.Episodes;
using Radarr.Http;

namespace Radarr.Api.V3.Tv.Calendar
{
    // Sonarr's calendar. The movie controller owns /calendar, so the episode calendar is served beside
    // it at /calendar/tv.
    [V3ApiController("calendar/tv")]
    public class TvCalendarController : EpisodeControllerWithSignalR
    {
        private readonly ITagService _tagService;

        public TvCalendarController(IBroadcastSignalRMessage signalR,
                                    IEpisodeService episodeService,
                                    ISeriesService seriesService,
                                    IUpgradableSpecification upgradableSpecification,
                                    ITagService tagService,
                                    ITvCustomFormatCalculationService formatCalculator)
            : base(episodeService, seriesService, upgradableSpecification, formatCalculator, signalR)
        {
            _tagService = tagService;
        }

        [NonAction]
        public override Results<Ok<EpisodeResource>, NotFound> GetResourceByIdWithErrorHandler(int id)
        {
            throw new NotImplementedException();
        }

        [HttpGet]
        [Produces("application/json")]
        public List<EpisodeResource> GetCalendar(DateTime? start, DateTime? end, bool unmonitored = false, bool includeSeries = false, bool includeEpisodeFile = false, bool includeEpisodeImages = false, string tags = "")
        {
            var startUse = start ?? DateTime.Today;
            var endUse = end ?? DateTime.Today.AddDays(2);
            var episodes = _episodeService.EpisodesBetweenDates(startUse, endUse, unmonitored, true);
            var allSeries = _seriesService.GetAllSeries();
            var parsedTags = new List<int>();
            var result = new List<Episode>();

            if (tags.IsNotNullOrWhiteSpace())
            {
                parsedTags.AddRange(tags.Split(',').Select(_tagService.GetTag).Select(t => t.Id));
            }

            foreach (var episode in episodes)
            {
                var series = allSeries.SingleOrDefault(s => s.Id == episode.SeriesId);

                if (series == null)
                {
                    continue;
                }

                if (parsedTags.Any() && parsedTags.None(series.Tags.Contains))
                {
                    continue;
                }

                result.Add(episode);
            }

            var resources = MapToResource(result, includeSeries, includeEpisodeFile, includeEpisodeImages);

            return resources.OrderBy(e => e.AirDateUtc).ToList();
        }
    }
}
