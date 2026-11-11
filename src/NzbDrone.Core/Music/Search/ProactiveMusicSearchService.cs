using System;
using System.Linq;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Music.History;
using NzbDrone.Core.Queue;

namespace NzbDrone.Core.Music.Search
{
    public class ProactiveMusicSearchService : IExecute<ProactiveMusicSearchCommand>
    {
        private readonly IConfigService _config;
        private readonly IAlbumService _albums;
        private readonly IQueueService _queue;
        private readonly IHistoryService _history;
        private readonly ISearchForReleases _search;
        private readonly IProcessDownloadDecisions _decisions;
        private readonly IProactiveMusicSearchActivityRepository _activity;
        private readonly Logger _logger;

        public ProactiveMusicSearchService(IConfigService config,
                                           IAlbumService albums,
                                           IQueueService queue,
                                           IHistoryService history,
                                           ISearchForReleases search,
                                           IProcessDownloadDecisions decisions,
                                           IProactiveMusicSearchActivityRepository activity,
                                           Logger logger)
        {
            _config = config;
            _albums = albums;
            _queue = queue;
            _history = history;
            _search = search;
            _decisions = decisions;
            _activity = activity;
            _logger = logger;
        }

        public void Execute(ProactiveMusicSearchCommand message)
        {
            var run = new ProactiveMusicSearchActivity { StartedAt = DateTime.UtcNow, Outcome = "disabled" };

            try
            {
                if (!_config.ProactiveMusicSearchEnabled)
                {
                    run.Reason = "Scheduled Music search is off";
                    return;
                }

                var paging = new PagingSpec<Album>
                {
                    Page = 1,
                    PageSize = 100000,
                    SortDirection = SortDirection.Ascending,
                    SortKey = "Id"
                };
                var albums = _albums.AlbumsWithoutFiles(paging).Records.ToList();
                var queueItems = _queue.GetQueue();
                var queued = queueItems.Select(item => item.GetRemoteAlbum())
                    .Where(remote => remote?.Albums.Any() == true)
                    .SelectMany(remote => remote.Albums.Select(album => album.Id));
                var failures = _history.Since(run.StartedAt - DownloadRetryPolicy.FailureWindow, AlbumHistoryEventType.DownloadFailed);
                var grabs = _history.Since(run.StartedAt.AddHours(-_config.ProactiveMusicRecentGrabHours), AlbumHistoryEventType.Grabbed);
                var assessments = MusicProactiveEligibility.Evaluate(albums, queued, failures, run.StartedAt,
                    grabs, _config.ProactiveMusicRecentGrabHours);
                run.MissingAlbums = assessments.Count;
                run.EligibleAlbums = assessments.Count(item => item.Eligible);
                var activeDownloads = ProactiveSearchPacing.ActiveDownloadCount(queueItems);

                if (activeDownloads >= _config.ProactiveMusicActiveDownloadLimit)
                {
                    run.Outcome = "paused";
                    run.Reason = $"Active download limit reached ({activeDownloads}/{_config.ProactiveMusicActiveDownloadLimit})";
                    return;
                }

                var candidate = assessments.Where(item => item.Eligible)
                    .OrderBy(item => item.Album.LastSearchTime ?? DateTime.MinValue)
                    .ThenBy(item => item.Album.Id)
                    .FirstOrDefault()?.Album;

                if (candidate == null)
                {
                    run.Outcome = "no-candidate";
                    run.Reason = string.Join(", ", assessments.Where(item => !item.Eligible)
                        .GroupBy(item => item.Reason).OrderBy(group => group.Key)
                        .Select(group => $"{group.Key}: {group.Count()}"));
                    if (run.Reason.Length == 0)
                    {
                        run.Reason = "No missing albums";
                    }

                    return;
                }

                run.AlbumId = candidate.Id;
                run.AlbumTitle = candidate.Title;
                run.Outcome = "searching";
                var decisions = _search.AlbumSearch(candidate.Id, false, false, false).GetAwaiter().GetResult();
                var processed = _decisions.ProcessDecisions(decisions).GetAwaiter().GetResult();
                run.GrabbedCount = processed.Grabbed.Count;
                run.Outcome = run.GrabbedCount > 0 ? "grabbed" : "no-grab";
            }
            catch (Exception ex)
            {
                run.Outcome = "failed";
                _logger.Error(ex, "Scheduled proactive Music search failed");
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
