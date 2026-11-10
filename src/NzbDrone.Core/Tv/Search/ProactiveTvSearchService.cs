using System;
using System.Linq;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Tv.History;

namespace NzbDrone.Core.Tv.Search
{
    public class ProactiveTvSearchService : IExecute<ProactiveTvSearchCommand>
    {
        private readonly IConfigService _config;
        private readonly IEpisodeService _episodes;
        private readonly ISeriesService _series;
        private readonly IQueueService _queue;
        private readonly IHistoryService _history;
        private readonly ISearchForReleases _search;
        private readonly IProcessDownloadDecisions _decisions;
        private readonly IProactiveTvSearchActivityRepository _activity;
        private readonly Logger _logger;

        public ProactiveTvSearchService(IConfigService config,
                                        IEpisodeService episodes,
                                        ISeriesService series,
                                        IQueueService queue,
                                        IHistoryService history,
                                        ISearchForReleases search,
                                        IProcessDownloadDecisions decisions,
                                        IProactiveTvSearchActivityRepository activity,
                                        Logger logger)
        {
            _config = config;
            _episodes = episodes;
            _series = series;
            _queue = queue;
            _history = history;
            _search = search;
            _decisions = decisions;
            _activity = activity;
            _logger = logger;
        }

        public void Execute(ProactiveTvSearchCommand message)
        {
            var run = new ProactiveTvSearchActivity { StartedAt = DateTime.UtcNow, Outcome = "disabled" };

            try
            {
                if (!_config.ProactiveTvSearchEnabled)
                {
                    run.Reason = "Scheduled TV search is off";
                    return;
                }

                var paging = new PagingSpec<Episode>
                {
                    Page = 1,
                    PageSize = 1000000,
                    SortDirection = SortDirection.Ascending,
                    SortKey = "Id"
                };
                var episodes = _episodes.EpisodesWithoutFiles(paging, true).Records.ToList();
                var seriesById = _series.GetAllSeries().ToDictionary(item => item.Id);
                var queueItems = _queue.GetQueue();
                var queued = queueItems.Select(item => item.GetRemoteEpisode())
                    .Where(remote => remote?.Episodes.Any() == true)
                    .SelectMany(remote => remote.Episodes.Select(episode => episode.Id));
                var failures = _history.Since(run.StartedAt - DownloadRetryPolicy.FailureWindow,
                    EpisodeHistoryEventType.DownloadFailed);
                var limited = TvProactiveFailurePolicy.LimitedSeries(failures, run.StartedAt);
                var grabs = _history.Since(run.StartedAt.AddHours(-_config.ProactiveTvRecentGrabHours), EpisodeHistoryEventType.Grabbed);
                var assessments = TvProactiveSeriesEligibility.Evaluate(episodes, seriesById, queued, limited, run.StartedAt, grabs, _config.ProactiveTvRecentGrabHours);
                run.MissingEpisodes = assessments.Sum(item => item.Episodes.Count);
                run.EligibleEpisodes = assessments.Sum(item => item.EligibleEpisodeCount);
                var activeDownloads = ProactiveSearchPacing.ActiveDownloadCount(queueItems);

                if (activeDownloads >= _config.ProactiveTvActiveDownloadLimit)
                {
                    run.Outcome = "paused";
                    run.Reason = $"Active download limit reached ({activeDownloads}/{_config.ProactiveTvActiveDownloadLimit})";
                    return;
                }

                var candidate = assessments
                    .SelectMany(item => item.Episodes.Where(episode => episode.Reason == "eligible")
                        .Select(episode => new { item.Series, episode.Episode }))
                    .OrderBy(item => item.Episode.LastSearchTime ?? DateTime.MinValue)
                    .ThenBy(item => item.Series.Id)
                    .ThenBy(item => item.Episode.Id)
                    .FirstOrDefault();

                if (candidate == null)
                {
                    run.Outcome = "no-candidate";
                    run.Reason = string.Join(", ", assessments.SelectMany(item => item.Episodes)
                        .Where(item => item.Reason != "eligible")
                        .GroupBy(item => item.Reason).OrderBy(group => group.Key)
                        .Select(group => $"{group.Key}: {group.Count()}"));
                    if (run.Reason.Length == 0)
                    {
                        run.Reason = "No aired missing episodes";
                    }

                    return;
                }

                run.SeriesId = candidate.Series.Id;
                run.SeriesTitle = candidate.Series.Title;
                run.EpisodeId = candidate.Episode.Id;
                run.Outcome = "searching";

                // One episode search is one bounded search unit. Bulk and season search can
                // make several provider requests and are reserved for manual commands.
                var decisions = _search.BoundedEpisodeSearch(candidate.Episode).GetAwaiter().GetResult();
                var processed = _decisions.ProcessDecisions(decisions).GetAwaiter().GetResult();
                run.GrabbedCount = processed.Grabbed.Count;
                run.Outcome = run.GrabbedCount > 0 ? "grabbed" : "no-grab";
            }
            catch (Exception ex)
            {
                run.Outcome = "failed";
                _logger.Error(ex, "Scheduled proactive TV search failed");
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
