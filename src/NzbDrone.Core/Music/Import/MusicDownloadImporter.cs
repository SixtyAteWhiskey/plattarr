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
using NzbDrone.Core.Music.History;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.MediaFiles.TrackImport;
using NzbDrone.Core.Music.Notifications;
using NzbDrone.Core.Music.Parser;

namespace NzbDrone.Core.Music.Import
{
    // Contract 5 for Music: the artist half of Lidarr's CompletedDownloadService. The core owns the
    // state machine (path validation, tracked download state, the DownloadCompletedEvent); this class
    // decides whether a completed download can be imported automatically, imports it, and judges
    // whether every album of the release is in. Lidarr's separate ImportFailed state and its
    // AlbumImportIncompleteEvent are not ported: the core blocks a partial import for manual import.
    public class MusicDownloadImporter : IDownloadImporter
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IHistoryService _historyService;
        private readonly IDownloadedTracksImportService _downloadedTracksImportService;
        private readonly IParsingService _parsingService;
        private readonly IArtistService _artistService;
        private readonly ITrackedDownloadAlreadyImported _trackedDownloadAlreadyImported;
        private readonly Logger _logger;

        public MusicDownloadImporter(IEventAggregator eventAggregator,
                                     IHistoryService historyService,
                                     IDownloadedTracksImportService downloadedTracksImportService,
                                     IParsingService parsingService,
                                     IArtistService artistService,
                                     ITrackedDownloadAlreadyImported trackedDownloadAlreadyImported,
                                     Logger logger)
        {
            _eventAggregator = eventAggregator;
            _historyService = historyService;
            _downloadedTracksImportService = downloadedTracksImportService;
            _parsingService = parsingService;
            _artistService = artistService;
            _trackedDownloadAlreadyImported = trackedDownloadAlreadyImported;
            _logger = logger;
        }

        public MediaKind Kind => MediaKind.Music;

        // Lidarr, like Sonarr, moves a download with one rejected result to ImportBlocked.
        public bool BlocksImportAfterRejection => true;

        public DownloadImportCheck Check(TrackedDownload trackedDownload)
        {
            var historyItem = _historyService.MostRecentForDownloadId(trackedDownload.DownloadItem.DownloadId);

            if (historyItem == null && trackedDownload.DownloadItem.Category.IsNullOrWhiteSpace())
            {
                trackedDownload.Warn("Download wasn't grabbed by Plattarr and not in a category, Skipping.");
                return DownloadImportCheck.Skip;
            }

            var artist = _parsingService.GetArtist(trackedDownload.DownloadItem.Title);

            if (artist == null && historyItem != null)
            {
                artist = _artistService.GetArtist(historyItem.ParentId);
            }

            if (artist == null)
            {
                trackedDownload.Warn("Artist name mismatch, automatic import is not possible. Manual Import required.");
                return DownloadImportCheck.Blocked;
            }

            return DownloadImportCheck.Ready;
        }

        public IReadOnlyList<IImportResult> Import(TrackedDownload trackedDownload, string outputPath)
        {
            return _downloadedTracksImportService.ProcessPath(outputPath,
                ImportMode.Auto,
                trackedDownload.GetArtist(),
                trackedDownload.ImportItem)
                .Cast<IImportResult>()
                .ToList();
        }

        public bool IsImported(TrackedDownload trackedDownload, IReadOnlyList<IImportResult> importResults)
        {
            var remoteAlbum = trackedDownload.GetRemoteAlbum();
            var imported = importResults.OfType<ImportResult>().Where(c => c.Result == ImportResultType.Imported).ToList();

            var allTracksImported =
                (importResults.Any() && importResults.All(c => c.Result == ImportResultType.Imported)) ||
                imported.SelectMany(c => c.ImportDecision.Item.Tracks).Count() >=
                Math.Max(1, remoteAlbum.Albums.Sum(x => x.AlbumReleases.Value.Where(y => y.Monitored).Sum(z => z.TrackCount)));

            if (allTracksImported)
            {
                _logger.Debug("All albums were imported for {0}", trackedDownload.DownloadItem.Title);
                return true;
            }

            // Double check by the history if at least one file was imported. This lets the decision
            // engine reject already imported files and still mark the download complete when every album is in.

            // EDGE CASE: this relies on album IDs staying the same between executions. If an album is
            // removed in a refresh and comes back with another ID, the download is treated as incomplete.
            var historyItems = _historyService.FindByDownloadId(trackedDownload.DownloadItem.DownloadId)
                                              .OrderByDescending(h => h.Date)
                                              .ToList();

            if (!_trackedDownloadAlreadyImported.IsImported(trackedDownload, historyItems))
            {
                _logger.Debug("Not all albums have been imported for the release '{0}'", trackedDownload.DownloadItem.Title);
                return false;
            }

            if (imported.Any())
            {
                _logger.Debug("All albums were imported in history for {0}", trackedDownload.DownloadItem.Title);
            }
            else
            {
                // The download is complete, but nothing was just imported, which points at incomplete download history.
                _logger.ForDebugEvent()
                       .Message("No albums were just imported, but all albums were previously imported, possible issue with download history.")
                       .Property("ArtistId", remoteAlbum.Artist.Id)
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
            _eventAggregator.PublishEvent(new ManualInteractionRequiredEvent(trackedDownload, new MusicNotificationData
            {
                Artist = trackedDownload.GetArtist(),
                RemoteAlbum = trackedDownload.GetRemoteAlbum()
            }));
        }
    }
}
