using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.Aggregation;
using NzbDrone.Core.Music.CustomFormats;
using NzbDrone.Core.Music.History;
using NzbDrone.Core.Music.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Music.Downloads
{
    // Contract 5 for Music: attributes a tracked download to an artist and its albums. This is the
    // album half of Lidarr's TrackedDownloadService.TrackDownload.
    public class MusicTrackedDownloadMatcher : ITrackedDownloadMatcher
    {
        private readonly IParsingService _parsingService;
        private readonly IHistoryService _historyService;
        private readonly IRemoteAlbumAggregationService _aggregationService;
        private readonly IMusicCustomFormatCalculationService _formatCalculator;
        private readonly Logger _logger;

        public MusicTrackedDownloadMatcher(IParsingService parsingService,
                                           IHistoryService historyService,
                                           IRemoteAlbumAggregationService aggregationService,
                                           IMusicCustomFormatCalculationService formatCalculator,
                                           Logger logger)
        {
            _parsingService = parsingService;
            _historyService = historyService;
            _aggregationService = aggregationService;
            _formatCalculator = formatCalculator;
            _logger = logger;
        }

        public MediaKind Kind => MediaKind.Music;

        // Lidarr's album title parser accepts almost any "name - name" title, including most TV and movie
        // releases. Routing rule 3 needs a yes only for a download that is Music, so a title counts when
        // it parses and names an artist in the library.
        public bool MatchesTitle(string title)
        {
            if (Parser.Parser.ParseAlbumTitle(title) == null)
            {
                return false;
            }

            try
            {
                return _parsingService.GetArtist(title) != null;
            }
            catch (MultipleArtistsFoundException)
            {
                return true;
            }
        }

        public void Match(TrackedDownload trackedDownload, DownloadHistory downloadHistory)
        {
            var downloadItem = trackedDownload.DownloadItem;

            try
            {
                var parsedAlbumInfo = Parser.Parser.ParseAlbumTitle(downloadItem.Title);

                if (parsedAlbumInfo != null)
                {
                    trackedDownload.RemoteRelease = _parsingService.Map(parsedAlbumInfo);
                }

                var historyItems = _historyService.FindByDownloadId(downloadItem.DownloadId)
                    .OrderByDescending(h => h.Date)
                    .ToList();

                if (historyItems.Any())
                {
                    var firstHistoryItem = historyItems.First();
                    var grabbedEvent = historyItems.FirstOrDefault(v => v.EventType == AlbumHistoryEventType.Grabbed);

                    trackedDownload.Indexer = grabbedEvent?.Data?.GetValueOrDefault("indexer");
                    trackedDownload.Added = grabbedEvent?.Date;

                    var remoteAlbum = trackedDownload.GetRemoteAlbum();

                    if (parsedAlbumInfo == null ||
                        remoteAlbum?.Artist == null ||
                        remoteAlbum.Albums.Empty())
                    {
                        // Try parsing the original source title and if that fails, try it with the grabbed artist and album
                        parsedAlbumInfo = Parser.Parser.ParseAlbumTitle(firstHistoryItem.SourceTitle);

                        if (parsedAlbumInfo == null && firstHistoryItem.Artist != null && firstHistoryItem.Album != null)
                        {
                            parsedAlbumInfo = Parser.Parser.ParseAlbumTitleWithSearchCriteria(firstHistoryItem.SourceTitle,
                                firstHistoryItem.Artist,
                                new List<Album> { firstHistoryItem.Album });
                        }

                        if (parsedAlbumInfo != null)
                        {
                            trackedDownload.RemoteRelease = _parsingService.Map(parsedAlbumInfo,
                                firstHistoryItem.ParentId,
                                historyItems.Where(v => v.EventType == AlbumHistoryEventType.Grabbed)
                                    .Select(h => h.ItemId).Distinct());
                        }
                    }

                    remoteAlbum = trackedDownload.GetRemoteAlbum();

                    if (remoteAlbum != null)
                    {
                        remoteAlbum.Release ??= new ReleaseInfo();
                        remoteAlbum.Release.Indexer = trackedDownload.Indexer;
                        remoteAlbum.Release.Title = remoteAlbum.ParsedAlbumInfo?.ReleaseTitle;

                        if (Enum.TryParse(grabbedEvent?.Data?.GetValueOrDefault("indexerFlags"), true, out IndexerFlags flags))
                        {
                            remoteAlbum.Release.AddIndexerFlags(flags);
                        }

                        if (downloadHistory != null)
                        {
                            remoteAlbum.Release.IndexerId = downloadHistory.IndexerId;
                        }
                    }
                }

                var matched = trackedDownload.GetRemoteAlbum();

                if (matched != null)
                {
                    _aggregationService.Augment(matched);

                    // Calculate custom formats
                    matched.CustomFormats = _formatCalculator.ParseCustomFormat(matched, downloadItem.TotalSize);
                }

                // Track it so it can be displayed in the queue even though we can't determine which artist it is for
                if (matched == null)
                {
                    _logger.Trace("No album found for download '{0}'", downloadItem.Title);
                }
            }
            catch (MultipleArtistsFoundException e)
            {
                _logger.Debug(e, "Found multiple artists for " + downloadItem.Title);

                trackedDownload.Warn("Unable to import automatically, found multiple artists: {0}", string.Join(", ", e.Artists));
            }
            catch (Exception e)
            {
                _logger.Debug(e, "Failed to find album for " + downloadItem.Title);

                trackedDownload.Warn("Unable to parse albums from title");
            }
        }

        public void Rematch(TrackedDownload trackedDownload)
        {
            var parsedAlbumInfo = Parser.Parser.ParseAlbumTitle(trackedDownload.DownloadItem.Title);
            var remoteAlbum = parsedAlbumInfo == null ? null : _parsingService.Map(parsedAlbumInfo);

            trackedDownload.RemoteRelease = remoteAlbum;

            _aggregationService.Augment(remoteAlbum);
        }
    }
}
