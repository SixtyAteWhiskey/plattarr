using System;
using System.Linq;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Movies.History;
using NzbDrone.Core.Queue;

namespace NzbDrone.Core.Movies.Search
{
    public class ProactiveMovieUpgradeService : IExecute<ProactiveMovieUpgradeCommand>
    {
        private readonly IConfigService _config;
        private readonly IMovieCutoffService _cutoff;
        private readonly IQueueService _queue;
        private readonly IHistoryService _history;
        private readonly ISearchForReleases _search;
        private readonly IProcessDownloadDecisions _decisions;
        private readonly IProactiveMovieUpgradeActivityRepository _activity;
        private readonly Logger _logger;

        public ProactiveMovieUpgradeService(IConfigService config,
                                            IMovieCutoffService cutoff,
                                            IQueueService queue,
                                            IHistoryService history,
                                            ISearchForReleases search,
                                            IProcessDownloadDecisions decisions,
                                            IProactiveMovieUpgradeActivityRepository activity,
                                            Logger logger)
        {
            _config = config;
            _cutoff = cutoff;
            _queue = queue;
            _history = history;
            _search = search;
            _decisions = decisions;
            _activity = activity;
            _logger = logger;
        }

        public void Execute(ProactiveMovieUpgradeCommand message)
        {
            var run = new ProactiveMovieUpgradeActivity { StartedAt = DateTime.UtcNow, Outcome = "disabled" };

            try
            {
                if (!_config.ProactiveMovieUpgradeEnabled)
                {
                    run.Reason = "Scheduled Movie upgrades are off";
                    return;
                }

                var paging = new PagingSpec<Movie>
                {
                    Page = 1,
                    PageSize = 100000,
                    SortDirection = SortDirection.Ascending,
                    SortKey = "Id"
                };
                var movies = _cutoff.MoviesWhereCutoffUnmet(paging).Records.ToList();
                var queueItems = _queue.GetQueue();
                var queued = queueItems.Where(item => item.GetMovie() != null).Select(item => item.GetMovie().Id);
                var failures = _history.Since(run.StartedAt - MovieSearchSafety.FailureWindow, MovieHistoryEventType.DownloadFailed);
                var grabs = _history.Since(run.StartedAt.AddHours(-_config.ProactiveMovieUpgradeRecentGrabHours), MovieHistoryEventType.Grabbed);
                var assessments = MovieProactiveEligibility.Evaluate(movies,
                                                                      queued,
                                                                      failures,
                                                                      run.StartedAt,
                                                                      grabs,
                                                                      _config.ProactiveMovieUpgradeRecentGrabHours);
                run.CutoffUnmetMovies = assessments.Count;
                run.EligibleMovies = assessments.Count(item => item.Eligible);
                var activeDownloads = ProactiveSearchPacing.ActiveDownloadCount(queueItems);

                if (activeDownloads >= _config.ProactiveMovieUpgradeActiveDownloadLimit)
                {
                    run.Outcome = "paused";
                    run.Reason = $"Active download limit reached ({activeDownloads}/{_config.ProactiveMovieUpgradeActiveDownloadLimit})";
                    return;
                }

                var candidate = assessments.Where(item => item.Eligible)
                    .OrderBy(item => item.Movie.LastSearchTime ?? DateTime.MinValue)
                    .ThenBy(item => item.Movie.Id)
                    .FirstOrDefault()?.Movie;

                if (candidate == null)
                {
                    run.Outcome = "no-candidate";
                    run.Reason = string.Join(", ", assessments.Where(item => !item.Eligible)
                        .GroupBy(item => item.Reason).OrderBy(group => group.Key)
                        .Select(group => $"{group.Key}: {group.Count()}"));
                    if (run.Reason.Length == 0)
                    {
                        run.Reason = "No cutoff-unmet Movies";
                    }

                    return;
                }

                run.MovieId = candidate.Id;
                run.MovieTitle = candidate.Title;
                run.Outcome = "searching";
                var decisions = _search.MovieSearch(candidate.Id, false, false).GetAwaiter().GetResult();
                var processed = _decisions.ProcessDecisions(decisions).GetAwaiter().GetResult();
                run.GrabbedCount = processed.Grabbed.Count;
                run.Outcome = run.GrabbedCount > 0 ? "grabbed" : "no-grab";
            }
            catch (Exception ex)
            {
                run.Outcome = "failed";
                _logger.Error(ex, "Scheduled proactive Movie upgrade failed");
                throw;
            }
            finally
            {
                run.EndedAt = DateTime.UtcNow;
                _activity.Insert(run);
            }
        }
    }
}
