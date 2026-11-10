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

namespace Radarr.Api.V3.ProactiveSearch
{
    public class ProactiveMovieItemResource
    {
        public int MovieId { get; set; }
        public string Title { get; set; }
        public DateTime? LastSearchTime { get; set; }
        public string CandidateType { get; set; }
        public string Reason { get; set; }

        public ProactiveMovieItemResource(MovieProactiveEligibility item)
        {
            MovieId = item.Movie.Id;
            Title = item.Movie.Title ?? string.Empty;
            LastSearchTime = item.Movie.LastSearchTime;
            CandidateType = item.CandidateType;
            Reason = item.Reason;
        }
    }

    public class ProactiveSearchPreviewResource
    {
        public DateTime GeneratedAt { get; set; }
        public int MissingMovies { get; set; }
        public int CutoffUnmetMovies { get; set; }
        public Dictionary<string, int> Reasons { get; set; }
        public List<ProactiveMovieItemResource> NextMovies { get; set; }
        public List<ProactiveMovieItemResource> RecentMovies { get; set; }
        public List<ProactiveMovieItemResource> SkippedMovies { get; set; }
        public int SkippedMoviesTotal { get; set; }
        public int ActiveDownloads { get; set; }
        public string PauseReason { get; set; }
    }

    [V3ApiController("proactive-search/preview")]
    public class ProactiveSearchPreviewController : Controller
    {
        private readonly IMovieService _movies;
        private readonly IMovieCutoffService _cutoff;
        private readonly IQueueService _queue;
        private readonly IHistoryService _history;
        private readonly IConfigService _config;

        public ProactiveSearchPreviewController(IMovieService movies, IMovieCutoffService cutoff, IQueueService queue, IHistoryService history, IConfigService config)
        {
            _movies = movies;
            _cutoff = cutoff;
            _queue = queue;
            _history = history;
            _config = config;
        }

        [HttpGet]
        [Produces("application/json")]
        public Ok<ProactiveSearchPreviewResource> Get([FromQuery] int offset = 0, [FromQuery] int limit = 50, [FromQuery] string title = null)
        {
            var now = DateTime.UtcNow;
            var paging = new PagingSpec<Movie>
            {
                Page = 1,
                PageSize = 100000,
                SortDirection = SortDirection.Ascending,
                SortKey = "Id"
            };
            var movies = _movies.MoviesWithoutFiles(paging).Records;
            var cutoffUnmetMovies = _cutoff.MoviesWhereCutoffUnmet(new PagingSpec<Movie>
            {
                Page = 1,
                PageSize = 100000,
                SortDirection = SortDirection.Ascending,
                SortKey = "Id"
            }).Records;
            var queueItems = _queue.GetQueue();
            var queued = queueItems.Where(item => item.GetMovie() != null).Select(item => item.GetMovie().Id);
            var failures = _history.Since(now - MovieSearchSafety.FailureWindow, MovieHistoryEventType.DownloadFailed);
            var grabs = _history.Since(now.AddHours(-_config.ProactiveMovieRecentGrabHours), MovieHistoryEventType.Grabbed);
            var assessments = MovieProactiveEligibility.Evaluate(movies, cutoffUnmetMovies, queued, failures, now, grabs, _config.ProactiveMovieRecentGrabHours);
            var activeDownloads = ProactiveSearchPacing.ActiveDownloadCount(queueItems);
            var skipped = assessments.Where(item => !item.Eligible &&
                (string.IsNullOrWhiteSpace(title) || item.Movie.Title?.Contains(title, StringComparison.OrdinalIgnoreCase) == true))
                .OrderBy(item => item.Movie.Title).ThenBy(item => item.Movie.Id).ToList();

            return TypedResults.Ok(new ProactiveSearchPreviewResource
            {
                GeneratedAt = now,
                ActiveDownloads = activeDownloads,
                PauseReason = ProactiveSearchPacing.PauseReason(_config.ProactiveMovieSearchEnabled, activeDownloads, _config.ProactiveMovieActiveDownloadLimit),
                MissingMovies = assessments.Count(item => item.CandidateType == "missing"),
                CutoffUnmetMovies = assessments.Count(item => item.CandidateType == "cutoff-unmet"),
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
}
