using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.History;
using NzbDrone.Core.Movies.Search;
using NzbDrone.Core.Queue;
using Radarr.Http;
using Radarr.Http.REST;

namespace Radarr.Api.V3.ProactiveSearch
{
    public class ProactiveMovieUpgradePreviewResource
    {
        public DateTime GeneratedAt { get; set; }
        public int CutoffUnmetMovies { get; set; }
        public Dictionary<string, int> Reasons { get; set; }
        public List<ProactiveMovieItemResource> NextMovies { get; set; }
        public List<ProactiveMovieItemResource> RecentMovies { get; set; }
        public List<ProactiveMovieItemResource> SkippedMovies { get; set; }
        public int SkippedMoviesTotal { get; set; }
        public int ActiveDownloads { get; set; }
        public string PauseReason { get; set; }
    }

    [V3ApiController("proactive-search/movie-upgrade-preview")]
    public class ProactiveMovieUpgradePreviewController : Controller
    {
        private readonly IMovieCutoffService _cutoff;
        private readonly IQueueService _queue;
        private readonly IHistoryService _history;
        private readonly IConfigService _config;

        public ProactiveMovieUpgradePreviewController(IMovieCutoffService cutoff, IQueueService queue, IHistoryService history, IConfigService config)
        {
            _cutoff = cutoff;
            _queue = queue;
            _history = history;
            _config = config;
        }

        [HttpGet]
        public Ok<ProactiveMovieUpgradePreviewResource> Get([FromQuery] int offset = 0, [FromQuery] int limit = 50, [FromQuery] string title = null)
        {
            var now = DateTime.UtcNow;
            var paging = new PagingSpec<Movie>
            {
                Page = 1,
                PageSize = 100000,
                SortDirection = SortDirection.Ascending,
                SortKey = "Id"
            };
            var movies = _cutoff.MoviesWhereCutoffUnmet(paging).Records;
            var queueItems = _queue.GetQueue();
            var queued = queueItems.Where(item => item.GetMovie() != null).Select(item => item.GetMovie().Id);
            var failures = _history.Since(now - MovieSearchSafety.FailureWindow, MovieHistoryEventType.DownloadFailed);
            var grabs = _history.Since(now.AddHours(-_config.ProactiveMovieUpgradeRecentGrabHours), MovieHistoryEventType.Grabbed);
            var assessments = MovieProactiveEligibility.Evaluate(movies, queued, failures, now, grabs, _config.ProactiveMovieUpgradeRecentGrabHours);
            var activeDownloads = ProactiveSearchPacing.ActiveDownloadCount(queueItems);
            var skipped = assessments.Where(item => !item.Eligible &&
                (string.IsNullOrWhiteSpace(title) || item.Movie.Title?.Contains(title, StringComparison.OrdinalIgnoreCase) == true))
                .OrderBy(item => item.Movie.Title).ThenBy(item => item.Movie.Id).ToList();

            return TypedResults.Ok(new ProactiveMovieUpgradePreviewResource
            {
                GeneratedAt = now,
                CutoffUnmetMovies = assessments.Count,
                ActiveDownloads = activeDownloads,
                PauseReason = ProactiveSearchPacing.PauseReason(_config.ProactiveMovieUpgradeEnabled, activeDownloads, _config.ProactiveMovieUpgradeActiveDownloadLimit),
                Reasons = assessments.GroupBy(item => item.Reason).ToDictionary(group => group.Key, group => group.Count()),
                NextMovies = assessments.Where(item => item.Eligible)
                    .OrderBy(item => item.Movie.LastSearchTime ?? DateTime.MinValue).ThenBy(item => item.Movie.Id)
                    .Take(10).Select(item => new ProactiveMovieItemResource(item)).ToList(),
                RecentMovies = assessments.Where(item => item.Movie.LastSearchTime.HasValue)
                    .OrderByDescending(item => item.Movie.LastSearchTime).Take(10)
                    .Select(item => new ProactiveMovieItemResource(item)).ToList(),
                SkippedMoviesTotal = skipped.Count,
                SkippedMovies = skipped.Skip(Math.Max(0, offset)).Take(Math.Clamp(limit, 1, 100))
                    .Select(item => new ProactiveMovieItemResource(item)).ToList()
            });
        }
    }

    public class ProactiveMovieUpgradeConfigResource : RestResource
    {
        public bool Enabled { get; set; }
        public int IntervalMinutes { get; set; }
        public int SearchesPerRun { get; set; }
        public int CooldownHours { get; set; }
        public int FailureLimit { get; set; }
        public int FailureWindowDays { get; set; }
        public int ActiveDownloadLimit { get; set; }
        public int RecentGrabHours { get; set; }
    }

    public class ProactiveMovieUpgradeConfigUpdateResource
    {
        public bool Enabled { get; set; }
        public int? IntervalMinutes { get; set; }
        public int? ActiveDownloadLimit { get; set; }
        public int? RecentGrabHours { get; set; }
    }

    [V3ApiController("proactive-search/movie-upgrade-config")]
    public class ProactiveMovieUpgradeConfigController : Controller
    {
        private readonly IConfigService _config;

        public ProactiveMovieUpgradeConfigController(IConfigService config)
        {
            _config = config;
        }

        [HttpGet]
        public Ok<ProactiveMovieUpgradeConfigResource> Get()
        {
            return TypedResults.Ok(ToResource());
        }

        [HttpPut("{id:int}")]
        public Ok<ProactiveMovieUpgradeConfigResource> Update(int id, [FromBody] ProactiveMovieUpgradeConfigUpdateResource resource)
        {
            if (id != 1 || resource == null)
            {
                throw new BadRequestException("A Movie upgrade configuration with id 1 is required.");
            }

            var interval = resource.IntervalMinutes ?? _config.ProactiveMovieUpgradeIntervalMinutes;
            var limit = resource.ActiveDownloadLimit ?? _config.ProactiveMovieUpgradeActiveDownloadLimit;
            var window = resource.RecentGrabHours ?? _config.ProactiveMovieUpgradeRecentGrabHours;
            if (!ProactiveSearchPacing.Valid(interval, limit, window))
            {
                throw new BadRequestException("Interval must be 5–1440 minutes, active download limit 1–100, and recent grab window 1–168 hours.");
            }

            _config.ProactiveMovieUpgradeEnabled = resource.Enabled;
            _config.ProactiveMovieUpgradeIntervalMinutes = interval;
            _config.ProactiveMovieUpgradeActiveDownloadLimit = limit;
            _config.ProactiveMovieUpgradeRecentGrabHours = window;
            return TypedResults.Ok(ToResource());
        }

        private ProactiveMovieUpgradeConfigResource ToResource()
        {
            return new ProactiveMovieUpgradeConfigResource
            {
                Id = 1,
                Enabled = _config.ProactiveMovieUpgradeEnabled,
                IntervalMinutes = _config.ProactiveMovieUpgradeIntervalMinutes,
                SearchesPerRun = 1,
                CooldownHours = 24,
                FailureLimit = MovieSearchSafety.FailureLimit,
                FailureWindowDays = (int)MovieSearchSafety.FailureWindow.TotalDays,
                ActiveDownloadLimit = _config.ProactiveMovieUpgradeActiveDownloadLimit,
                RecentGrabHours = _config.ProactiveMovieUpgradeRecentGrabHours
            };
        }
    }

    [V3ApiController("proactive-search/movie-upgrade-activity")]
    public class ProactiveMovieUpgradeActivityController : Controller
    {
        private readonly IProactiveMovieUpgradeActivityRepository _activity;

        public ProactiveMovieUpgradeActivityController(IProactiveMovieUpgradeActivityRepository activity)
        {
            _activity = activity;
        }

        [HttpGet]
        public Ok<List<ProactiveMovieUpgradeActivity>> Get()
        {
            return TypedResults.Ok(_activity.Recent(20));
        }
    }
}
