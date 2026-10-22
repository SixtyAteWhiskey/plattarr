using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.Aggregation;
using NzbDrone.Core.Movies.History;
using NzbDrone.Core.Movies.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Movies.Downloads
{
    // Contract 5 for movies: attributes a tracked download to a movie in the library.
    public class MovieTrackedDownloadMatcher : ITrackedDownloadMatcher
    {
        private readonly IParsingService _parsingService;
        private readonly IHistoryService _historyService;
        private readonly IRemoteMovieAggregationService _aggregationService;
        private readonly ICustomFormatCalculationService _formatCalculator;
        private readonly Logger _logger;

        public MovieTrackedDownloadMatcher(IParsingService parsingService,
                                           IHistoryService historyService,
                                           IRemoteMovieAggregationService aggregationService,
                                           ICustomFormatCalculationService formatCalculator,
                                           Logger logger)
        {
            _parsingService = parsingService;
            _historyService = historyService;
            _aggregationService = aggregationService;
            _formatCalculator = formatCalculator;
            _logger = logger;
        }

        public MediaKind Kind => MediaKind.Movies;

        public bool MatchesTitle(string title)
        {
            return Parser.Parser.ParseMovieTitle(title) != null;
        }

        public void Match(TrackedDownload trackedDownload, DownloadHistory downloadHistory)
        {
            var downloadItem = trackedDownload.DownloadItem;

            try
            {
                var parsedMovieInfo = Parser.Parser.ParseMovieTitle(downloadItem.Title);

                if (parsedMovieInfo != null)
                {
                    trackedDownload.RemoteRelease = downloadHistory is { EventType: DownloadHistoryEventType.DownloadImported }
                        ? _parsingService.Map(parsedMovieInfo, downloadHistory.ParentId)
                        : _parsingService.Map(parsedMovieInfo, "", 0, null);
                }

                var historyItems = _historyService.FindByDownloadId(downloadItem.DownloadId)
                    .OrderByDescending(h => h.Date)
                    .ToList();

                if (historyItems.Any())
                {
                    var firstHistoryItem = historyItems.First();
                    var grabbedEvent = historyItems.FirstOrDefault(v => v.EventType == MovieHistoryEventType.Grabbed);

                    trackedDownload.Indexer = grabbedEvent?.Data?.GetValueOrDefault("indexer");
                    trackedDownload.Added = grabbedEvent?.Date;

                    if (parsedMovieInfo == null ||
                        trackedDownload.GetRemoteMovie()?.Movie == null)
                    {
                        parsedMovieInfo = Parser.Parser.ParseMovieTitle(firstHistoryItem.SourceTitle);

                        if (parsedMovieInfo != null)
                        {
                            trackedDownload.RemoteRelease = _parsingService.Map(parsedMovieInfo,
                                firstHistoryItem.ParentId);
                        }
                    }

                    var remoteMovie = trackedDownload.GetRemoteMovie();

                    if (remoteMovie != null)
                    {
                        remoteMovie.Release ??= new ReleaseInfo();
                        remoteMovie.Release.Indexer = trackedDownload.Indexer;
                        remoteMovie.Release.Title = remoteMovie.ParsedMovieInfo?.ReleaseTitle;

                        if (Enum.TryParse(grabbedEvent?.Data?.GetValueOrDefault("indexerFlags"), true, out IndexerFlags flags))
                        {
                            remoteMovie.Release.AddIndexerFlags(flags);
                        }

                        if (downloadHistory != null)
                        {
                            remoteMovie.Release.IndexerId = downloadHistory.IndexerId;
                        }
                    }
                }

                var matched = trackedDownload.GetRemoteMovie();

                if (matched != null)
                {
                    _aggregationService.Augment(matched);

                    // Calculate custom formats
                    matched.CustomFormats = _formatCalculator.ParseCustomFormat(matched, downloadItem.TotalSize);
                }

                // Track it so it can be displayed in the queue even though we can't determine which movie it is for
                if (matched == null)
                {
                    _logger.Trace("No Movie found for download '{0}'", downloadItem.Title);
                }
            }
            catch (MultipleMoviesFoundException e)
            {
                _logger.Debug(e, "Found multiple movies for " + downloadItem.Title);

                trackedDownload.Warn("Unable to import automatically, found multiple movies: {0}", string.Join(", ", e.Movies));
            }
            catch (Exception e)
            {
                _logger.Debug(e, "Failed to find movie for " + downloadItem.Title);

                trackedDownload.Warn("Unable to parse movie from title");
            }
        }

        public void Rematch(TrackedDownload trackedDownload)
        {
            var parsedMovieInfo = Parser.Parser.ParseMovieTitle(trackedDownload.DownloadItem.Title);
            var remoteMovie = parsedMovieInfo == null ? null : _parsingService.Map(parsedMovieInfo, "", 0, null);

            trackedDownload.RemoteRelease = remoteMovie;

            _aggregationService.Augment(remoteMovie);
        }
    }
}
