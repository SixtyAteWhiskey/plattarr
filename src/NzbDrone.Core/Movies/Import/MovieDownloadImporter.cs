using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.History;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.MovieImport;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Movies.Import
{
    // Contract 5 for movies: the movie half of Radarr's CompletedDownloadService, moved unchanged.
    public class MovieDownloadImporter : IDownloadImporter
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IHistoryService _historyService;
        private readonly IDownloadedMovieImportService _downloadedMovieImportService;
        private readonly IParsingService _parsingService;
        private readonly IMovieService _movieService;
        private readonly ITrackedDownloadAlreadyImported _trackedDownloadAlreadyImported;
        private readonly Logger _logger;

        public MovieDownloadImporter(IEventAggregator eventAggregator,
                                     IHistoryService historyService,
                                     IDownloadedMovieImportService downloadedMovieImportService,
                                     IParsingService parsingService,
                                     IMovieService movieService,
                                     ITrackedDownloadAlreadyImported trackedDownloadAlreadyImported,
                                     Logger logger)
        {
            _eventAggregator = eventAggregator;
            _historyService = historyService;
            _downloadedMovieImportService = downloadedMovieImportService;
            _parsingService = parsingService;
            _movieService = movieService;
            _trackedDownloadAlreadyImported = trackedDownloadAlreadyImported;
            _logger = logger;
        }

        public MediaKind Kind => MediaKind.Movies;

        public DownloadImportCheck Check(TrackedDownload trackedDownload)
        {
            var historyItem = GetGrabbedHistory(trackedDownload).MaxBy(h => h.Date);

            if (historyItem == null && trackedDownload.DownloadItem.Category.IsNullOrWhiteSpace())
            {
                trackedDownload.Warn("Download wasn't grabbed by Radarr and not in a category, Skipping.");
                return DownloadImportCheck.Skip;
            }

            var movie = _parsingService.GetMovie(trackedDownload.DownloadItem.Title);

            if (movie == null)
            {
                if (historyItem != null)
                {
                    movie = _movieService.GetMovie(historyItem.ParentId);
                }

                if (movie == null)
                {
                    trackedDownload.Warn("Movie title mismatch, automatic import is not possible. Manual Import required.");
                    return DownloadImportCheck.Blocked;
                }

                Enum.TryParse(historyItem.Data.GetValueOrDefault(MovieHistory.MOVIE_MATCH_TYPE, MovieMatchType.Unknown.ToString()), out MovieMatchType movieMatchType);
                Enum.TryParse(historyItem.Data.GetValueOrDefault(MovieHistory.RELEASE_SOURCE, ReleaseSourceType.Unknown.ToString()), out ReleaseSourceType releaseSource);

                // Show a warning if the release was matched by ID and the source is not interactive search
                if (movieMatchType == MovieMatchType.Id && releaseSource != ReleaseSourceType.InteractiveSearch)
                {
                    trackedDownload.Warn("Found matching movie via grab history, but release was matched to movie by ID. Manual Import required.");
                    return DownloadImportCheck.Blocked;
                }
            }

            return DownloadImportCheck.Ready;
        }

        public IReadOnlyList<IImportResult> Import(TrackedDownload trackedDownload, string outputPath)
        {
            return _downloadedMovieImportService.ProcessPath(outputPath,
                ImportMode.Auto,
                trackedDownload.RemoteMovie.Movie,
                trackedDownload.ImportItem);
        }

        public bool IsImported(TrackedDownload trackedDownload, IReadOnlyList<IImportResult> importResults)
        {
            var atLeastOneMovieImported = importResults.Any(c => c.Result == ImportResultType.Imported);

            if (atLeastOneMovieImported)
            {
                _logger.Debug("All movies were imported for {0}", trackedDownload.DownloadItem.Title);
                return true;
            }

            // Double check if all movies were imported by checking the history if at least one
            // file was imported. This will allow the decision engine to reject already imported
            // movie files and still mark the download complete when all files are imported.
            var historyItems = _historyService.FindByDownloadId(trackedDownload.DownloadItem.DownloadId)
                                              .OrderByDescending(h => h.Date)
                                              .ToList();

            if (!_trackedDownloadAlreadyImported.IsImported(trackedDownload, historyItems))
            {
                _logger.Debug("Not all movies have been imported for {0}", trackedDownload.DownloadItem.Title);
                return false;
            }

            // The download is complete, but nothing was just imported, which points at incomplete download history.
            _logger.ForDebugEvent()
                   .Message("No Movies were just imported, but all movies were previously imported, possible issue with download history.")
                   .Property("MovieId", trackedDownload.RemoteMovie.Movie.Id)
                   .Property("DownloadId", trackedDownload.DownloadItem.DownloadId)
                   .Property("Title", trackedDownload.DownloadItem.Title)
                   .Property("Path", trackedDownload.ImportItem.OutputPath.ToString())
                   .WriteSentryWarn("DownloadHistoryIncomplete")
                   .Log();

            return true;
        }

        public void ImportBlocked(TrackedDownload trackedDownload)
        {
            var grabbedHistories = GetGrabbedHistory(trackedDownload);
            var releaseInfo = grabbedHistories.Count > 0 ? new GrabbedReleaseInfo(grabbedHistories) : null;

            _eventAggregator.PublishEvent(new ManualInteractionRequiredEvent(trackedDownload, releaseInfo));
        }

        private List<MovieHistory> GetGrabbedHistory(TrackedDownload trackedDownload)
        {
            return _historyService.FindByDownloadId(trackedDownload.DownloadItem.DownloadId)
                                  .Where(h => h.EventType == MovieHistoryEventType.Grabbed)
                                  .ToList();
        }
    }
}
