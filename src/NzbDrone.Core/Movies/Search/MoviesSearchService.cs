using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Movies.History;
using NzbDrone.Core.Queue;

namespace NzbDrone.Core.Movies.Search
{
    public class MovieSearchService : IExecute<MoviesSearchCommand>, IExecute<MissingMoviesSearchCommand>, IExecute<CutoffUnmetMoviesSearchCommand>
    {
        private readonly IMovieService _movieService;
        private readonly IMovieCutoffService _movieCutoffService;
        private readonly ISearchForReleases _releaseSearchService;
        private readonly IProcessDownloadDecisions _processDownloadDecisions;
        private readonly IQueueService _queueService;
        private readonly IHistoryService _history;
        private readonly IConfigService _config;
        private readonly Logger _logger;

        public MovieSearchService(IMovieService movieService,
                                   IMovieCutoffService movieCutoffService,
                                   ISearchForReleases releaseSearchService,
                                   IProcessDownloadDecisions processDownloadDecisions,
                                   IQueueService queueService,
                                   IHistoryService history,
                                   IConfigService config,
                                   Logger logger)
        {
            _movieService = movieService;
            _movieCutoffService = movieCutoffService;
            _releaseSearchService = releaseSearchService;
            _processDownloadDecisions = processDownloadDecisions;
            _queueService = queueService;
            _history = history;
            _config = config;
            _logger = logger;
        }

        public void Execute(MoviesSearchCommand message)
        {
            var userInvokedSearch = message.Trigger == CommandTrigger.Manual;

            var movies = _movieService.GetMovies(message.MovieIds)
                .Where(m => (m.Monitored && m.IsAvailable()) || userInvokedSearch)
                .ToList();

            message.GrabbedCount = SearchForBulkMovies(movies, userInvokedSearch).GetAwaiter().GetResult();
        }

        public void Execute(MissingMoviesSearchCommand message)
        {
            if (message.Trigger == CommandTrigger.Scheduled && !_config.ProactiveMovieSearchEnabled)
            {
                message.GrabbedCount = 0;
                return;
            }

            var pagingSpec = new PagingSpec<Movie>
            {
                Page = 1,
                PageSize = 100000,
                SortDirection = SortDirection.Ascending,
                SortKey = "Id"
            };

            pagingSpec.FilterExpressions.Add(v => v.Monitored == true);

            var movies = _movieService.MoviesWithoutFiles(pagingSpec).Records.ToList();

            var queue = _queueService.GetQueue().Where(q => q.GetMovie() != null).Select(q => q.GetMovie().Id).ToHashSet();
            var missing = movies.Where(e => !queue.Contains(e.Id)).ToList();

            if (message.Trigger == CommandTrigger.Scheduled)
            {
                var now = DateTime.UtcNow;
                var failures = _history.Since(now - MovieSearchSafety.FailureWindow, MovieHistoryEventType.DownloadFailed);

                // Use the same reasons as the read-only preview, then make one provider request.
                missing = MovieProactiveEligibility.Evaluate(movies, queue, failures, now)
                    .Where(item => item.Eligible)
                    .OrderBy(item => item.Movie.LastSearchTime ?? DateTime.MinValue)
                    .ThenBy(item => item.Movie.Id)
                    .Take(1)
                    .Select(item => item.Movie)
                    .ToList();
            }

            message.GrabbedCount = SearchForBulkMovies(missing, message.Trigger == CommandTrigger.Manual).GetAwaiter().GetResult();
        }

        public void Execute(CutoffUnmetMoviesSearchCommand message)
        {
            var pagingSpec = new PagingSpec<Movie>
            {
                Page = 1,
                PageSize = 100000,
                SortDirection = SortDirection.Ascending,
                SortKey = "Id"
            };

            pagingSpec.FilterExpressions.Add(v => v.Monitored == true);

            var movies = _movieCutoffService.MoviesWhereCutoffUnmet(pagingSpec).Records.ToList();

            var queue = _queueService.GetQueue().Where(q => q.GetMovie() != null).Select(q => q.GetMovie().Id);
            var missing = movies.Where(e => !queue.Contains(e.Id)).ToList();

            SearchForBulkMovies(missing, message.Trigger == CommandTrigger.Manual).GetAwaiter().GetResult();
        }

        private async Task<int> SearchForBulkMovies(List<Movie> movies, bool userInvokedSearch)
        {
            _logger.ProgressInfo("Performing search for {0} movies", movies.Count);
            var downloadedCount = 0;

            foreach (var movieId in movies.GroupBy(e => e.Id).OrderBy(g => g.Min(m => m.LastSearchTime ?? DateTime.MinValue)))
            {
                List<DownloadDecision> decisions;

                try
                {
                    decisions = await _releaseSearchService.MovieSearch(movieId.Key, userInvokedSearch, false);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unable to search for movie: [{0}]", movieId.Key);
                    continue;
                }

                var processDecisions = await _processDownloadDecisions.ProcessDecisions(decisions);

                downloadedCount += processDecisions.Grabbed.Count;
            }

            _logger.ProgressInfo("Completed search for {0} movies. {1} reports downloaded.", movies.Count, downloadedCount);
            return downloadedCount;
        }
    }
}
