using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv.History;
using NzbDrone.Core.Tv.MediaFiles;
using NzbDrone.Core.Tv.MediaFiles.EpisodeImport;
using NzbDrone.Core.Tv.Notifications;
using NzbDrone.Core.Tv.Parser;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv.Import
{
    // Contract 5 for TV: the series half of Sonarr's CompletedDownloadService. The core owns the state
    // machine (path validation, tracked download state, the DownloadCompletedEvent); this class decides
    // whether a completed download can be imported automatically, imports it, and judges whether every
    // episode of the release is in.
    public class TvDownloadImporter : IDownloadImporter
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IHistoryService _historyService;
        private readonly IDownloadedEpisodesImportService _downloadedEpisodesImportService;
        private readonly IParsingService _parsingService;
        private readonly ISeriesService _seriesService;
        private readonly ITrackedDownloadAlreadyImported _trackedDownloadAlreadyImported;
        private readonly Logger _logger;

        public TvDownloadImporter(IEventAggregator eventAggregator,
                                  IHistoryService historyService,
                                  IDownloadedEpisodesImportService downloadedEpisodesImportService,
                                  IParsingService parsingService,
                                  ISeriesService seriesService,
                                  ITrackedDownloadAlreadyImported trackedDownloadAlreadyImported,
                                  Logger logger)
        {
            _eventAggregator = eventAggregator;
            _historyService = historyService;
            _downloadedEpisodesImportService = downloadedEpisodesImportService;
            _parsingService = parsingService;
            _seriesService = seriesService;
            _trackedDownloadAlreadyImported = trackedDownloadAlreadyImported;
            _logger = logger;
        }

        public MediaKind Kind => MediaKind.Tv;

        // Sonarr moves a download with one rejected result to ImportBlocked; Radarr leaves it pending.
        public bool BlocksImportAfterRejection => true;

        public DownloadImportCheck Check(TrackedDownload trackedDownload)
        {
            var historyItem = GetGrabbedHistory(trackedDownload).MaxBy(h => h.Date);

            if (historyItem == null && trackedDownload.DownloadItem.Category.IsNullOrWhiteSpace())
            {
                trackedDownload.Warn("Download wasn't grabbed by Plattarr and not in a category, Skipping.");
                return DownloadImportCheck.Skip;
            }

            var series = _parsingService.GetSeries(trackedDownload.DownloadItem.Title);

            if (series == null)
            {
                if (historyItem != null)
                {
                    series = _seriesService.GetSeries(historyItem.ParentId);
                }

                if (series == null)
                {
                    trackedDownload.Warn("Series title mismatch; automatic import is not possible. Manual Import required.");
                    return DownloadImportCheck.Blocked;
                }

                Enum.TryParse(historyItem.Data.GetValueOrDefault(EpisodeHistory.SERIES_MATCH_TYPE, SeriesMatchType.Unknown.ToString()), out SeriesMatchType seriesMatchType);
                Enum.TryParse(historyItem.Data.GetValueOrDefault(EpisodeHistory.RELEASE_SOURCE, ReleaseSourceType.Unknown.ToString()), out ReleaseSourceType releaseSource);

                // Show a warning if the release was matched by ID and the source is not interactive search
                if (seriesMatchType == SeriesMatchType.Id && releaseSource != ReleaseSourceType.InteractiveSearch)
                {
                    trackedDownload.Warn("Found matching series via grab history, but release was matched to series by ID. Automatic import is not possible. Manual Import required.");
                    return DownloadImportCheck.Blocked;
                }
            }

            return DownloadImportCheck.Ready;
        }

        public IReadOnlyList<IImportResult> Import(TrackedDownload trackedDownload, string outputPath)
        {
            return _downloadedEpisodesImportService.ProcessPath(outputPath,
                ImportMode.Auto,
                trackedDownload.GetSeries(),
                trackedDownload.ImportItem)
                .Cast<IImportResult>()
                .ToList();
        }

        public bool IsImported(TrackedDownload trackedDownload, IReadOnlyList<IImportResult> importResults)
        {
            var remoteEpisode = trackedDownload.GetRemoteEpisode();

            var importedEpisodeCount = importResults.OfType<ImportResult>()
                                                    .Where(c => c.Result == ImportResultType.Imported)
                                                    .SelectMany(c => c.ImportDecision.LocalEpisode.Episodes)
                                                    .Count();

            if (importedEpisodeCount >= Math.Max(1, remoteEpisode.Episodes.Count))
            {
                _logger.Debug("All episodes were imported for {0}", trackedDownload.DownloadItem.Title);
                return true;
            }

            // Double check if all episodes were imported by checking the history if at least one
            // file was imported. This will allow the decision engine to reject already imported
            // episode files and still mark the download complete when all files are imported.

            // EDGE CASE: This process relies on EpisodeIds being consistent between executions, if a series is updated
            // and an episode is removed, but later comes back with a different ID then the download is treated as incomplete.
            // Since imports should be relatively fast and these types of data changes are infrequent this should be quite
            // safe, but commenting for future benefit.
            var historyItems = _historyService.FindByDownloadId(trackedDownload.DownloadItem.DownloadId)
                                              .OrderByDescending(h => h.Date)
                                              .ToList();

            if (!_trackedDownloadAlreadyImported.IsImported(trackedDownload, historyItems))
            {
                _logger.Debug("Not all episodes have been imported for the release '{0}'", trackedDownload.DownloadItem.Title);
                return false;
            }

            if (importResults.Any(c => c.Result == ImportResultType.Imported))
            {
                _logger.Debug("All episodes were imported in history for {0}", trackedDownload.DownloadItem.Title);
            }
            else
            {
                // The download is complete, but nothing was just imported, which points at incomplete download history.
                _logger.ForDebugEvent()
                       .Message("No Episodes were just imported, but all episodes were previously imported, possible issue with download history.")
                       .Property("SeriesId", remoteEpisode.Series.Id)
                       .Property("DownloadId", trackedDownload.DownloadItem.DownloadId)
                       .Property("Title", trackedDownload.DownloadItem.Title)
                       .Property("Path", trackedDownload.ImportItem.OutputPath.ToString())
                       .WriteSentryWarn("DownloadHistoryIncomplete")
                       .Log();
            }

            return true;
        }

        public void ImportBlocked(TrackedDownload trackedDownload)
        {
            var grabbedHistories = GetGrabbedHistory(trackedDownload);
            var releaseInfo = grabbedHistories.Count > 0 ? new GrabbedReleaseInfo(grabbedHistories) : null;

            _eventAggregator.PublishEvent(new ManualInteractionRequiredEvent(trackedDownload, new TvNotificationData
            {
                Series = trackedDownload.GetSeries(),
                RemoteEpisode = trackedDownload.GetRemoteEpisode(),
                Release = releaseInfo
            }));
        }

        private List<EpisodeHistory> GetGrabbedHistory(TrackedDownload trackedDownload)
        {
            return _historyService.FindByDownloadId(trackedDownload.DownloadItem.DownloadId)
                                  .Where(h => h.EventType == EpisodeHistoryEventType.Grabbed)
                                  .ToList();
        }
    }
}
