using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.History;
using NzbDrone.Core.Tv.Search;
using Radarr.Http;

namespace Radarr.Api.V3.ProactiveSearch
{
    public class ProactiveTvSeriesResource
    {
        public int SeriesId { get; set; }
        public string Title { get; set; }
        public int MissingEpisodes { get; set; }
        public int EligibleEpisodes { get; set; }
        public DateTime? LastSearchTime { get; set; }
        public Dictionary<string, int> Reasons { get; set; }

        public ProactiveTvSeriesResource(TvProactiveSeriesEligibility item)
        {
            SeriesId = item.Series.Id;
            Title = item.Series.Title ?? string.Empty;
            MissingEpisodes = item.Episodes.Count;
            EligibleEpisodes = item.EligibleEpisodeCount;
            LastSearchTime = item.LastSearchTime;
            Reasons = item.Reasons;
        }
    }

    public class ProactiveTvPreviewResource
    {
        public DateTime GeneratedAt { get; set; }
        public int MissingSeries { get; set; }
        public int MissingEpisodes { get; set; }
        public int EligibleSeries { get; set; }
        public List<ProactiveTvSeriesResource> CandidateSeries { get; set; }
        public List<ProactiveTvSeriesResource> RecentSeries { get; set; }
        public List<ProactiveTvSeriesResource> SkippedSeries { get; set; }
        public int SkippedSeriesTotal { get; set; }
        public int ActiveDownloads { get; set; }
        public string PauseReason { get; set; }
    }

    [V3ApiController("proactive-search/tv-preview")]
    public class ProactiveTvPreviewController : Controller
    {
        private readonly IEpisodeService _episodes;
        private readonly ISeriesService _series;
        private readonly IQueueService _queue;
        private readonly IHistoryService _history;
        private readonly IConfigService _config;

        public ProactiveTvPreviewController(IEpisodeService episodes,
                                            ISeriesService series,
                                            IQueueService queue,
                                            IHistoryService history,
                                            IConfigService config)
        {
            _episodes = episodes;
            _series = series;
            _queue = queue;
            _history = history;
            _config = config;
        }

        [HttpGet]
        [Produces("application/json")]
        public Ok<ProactiveTvPreviewResource> Get([FromQuery] int offset = 0, [FromQuery] int limit = 50, [FromQuery] string title = null)
        {
            var now = DateTime.UtcNow;
            var paging = new PagingSpec<Episode>
            {
                Page = 1,
                PageSize = 1000000,
                SortDirection = SortDirection.Ascending,
                SortKey = "Id"
            };

            // The missing-episode repository already limits these rows to aired episodes without files.
            var episodes = _episodes.EpisodesWithoutFiles(paging, true).Records.ToList();
            var seriesById = _series.GetAllSeries().ToDictionary(item => item.Id);
            var queueItems = _queue.GetQueue();
            var queued = queueItems.Select(item => item.GetRemoteEpisode())
                .Where(remote => remote?.Episodes.Any() == true)
                .SelectMany(remote => remote.Episodes.Select(episode => episode.Id));
            var failures = _history.Since(now - DownloadRetryPolicy.FailureWindow, EpisodeHistoryEventType.DownloadFailed);
            var failedSeriesIds = TvProactiveFailurePolicy.LimitedSeries(failures, now);
            var grabs = _history.Since(now.AddHours(-_config.ProactiveTvRecentGrabHours), EpisodeHistoryEventType.Grabbed);
            var assessments = TvProactiveSeriesEligibility.Evaluate(episodes, seriesById, queued, failedSeriesIds, now, grabs, _config.ProactiveTvRecentGrabHours);
            var activeDownloads = ProactiveSearchPacing.ActiveDownloadCount(queueItems);
            var skipped = assessments.Where(item => !item.Eligible &&
                (string.IsNullOrWhiteSpace(title) || item.Series.Title?.Contains(title, StringComparison.OrdinalIgnoreCase) == true))
                .OrderBy(item => item.Series.Title).ThenBy(item => item.Series.Id).ToList();

            return TypedResults.Ok(new ProactiveTvPreviewResource
            {
                GeneratedAt = now,
                ActiveDownloads = activeDownloads,
                PauseReason = ProactiveSearchPacing.PauseReason(_config.ProactiveTvSearchEnabled, activeDownloads, _config.ProactiveTvActiveDownloadLimit),
                MissingSeries = assessments.Count,
                MissingEpisodes = assessments.Sum(item => item.Episodes.Count),
                EligibleSeries = assessments.Count(item => item.Eligible),
                CandidateSeries = assessments.Where(item => item.Eligible)
                    .OrderBy(item => item.OldestEligibleSearchTime).ThenBy(item => item.Series.Id)
                    .Take(10).Select(item => new ProactiveTvSeriesResource(item)).ToList(),
                RecentSeries = assessments.Where(item => item.LastSearchTime.HasValue)
                    .OrderByDescending(item => item.LastSearchTime).Take(10)
                    .Select(item => new ProactiveTvSeriesResource(item)).ToList(),
                SkippedSeriesTotal = skipped.Count,
                SkippedSeries = skipped.Skip(Math.Max(0, offset)).Take(Math.Clamp(limit, 1, 100))
                    .Select(item => new ProactiveTvSeriesResource(item)).ToList()
            });
        }
    }
}
