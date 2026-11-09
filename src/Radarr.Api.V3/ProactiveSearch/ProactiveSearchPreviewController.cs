using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Datastore;
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
        public string Reason { get; set; }

        public ProactiveMovieItemResource(MovieProactiveEligibility item)
        {
            MovieId = item.Movie.Id;
            Title = item.Movie.Title ?? string.Empty;
            LastSearchTime = item.Movie.LastSearchTime;
            Reason = item.Reason;
        }
    }

    public class ProactiveSearchPreviewResource
    {
        public DateTime GeneratedAt { get; set; }
        public int MissingMovies { get; set; }
        public Dictionary<string, int> Reasons { get; set; }
        public List<ProactiveMovieItemResource> NextMovies { get; set; }
        public List<ProactiveMovieItemResource> RecentMovies { get; set; }
        public List<ProactiveMovieItemResource> SkippedMovies { get; set; }
        public int SkippedMoviesTotal { get; set; }
    }

    [V3ApiController("proactive-search/preview")]
    public class ProactiveSearchPreviewController : Controller
    {
        private readonly IMovieService _movies;
        private readonly IQueueService _queue;
        private readonly IHistoryService _history;

        public ProactiveSearchPreviewController(IMovieService movies, IQueueService queue, IHistoryService history)
        {
            _movies = movies;
            _queue = queue;
            _history = history;
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
            var queued = _queue.GetQueue().Where(item => item.GetMovie() != null).Select(item => item.GetMovie().Id);
            var failures = _history.Since(now - MovieSearchSafety.FailureWindow, MovieHistoryEventType.DownloadFailed);
            var assessments = MovieProactiveEligibility.Evaluate(movies, queued, failures, now);
            var skipped = assessments.Where(item => !item.Eligible &&
                (string.IsNullOrWhiteSpace(title) || item.Movie.Title?.Contains(title, StringComparison.OrdinalIgnoreCase) == true))
                .OrderBy(item => item.Movie.Title).ThenBy(item => item.Movie.Id).ToList();

            return TypedResults.Ok(new ProactiveSearchPreviewResource
            {
                GeneratedAt = now,
                MissingMovies = assessments.Count,
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
