using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv.Aggregation;
using NzbDrone.Core.Tv.CustomFormats;
using NzbDrone.Core.Tv.History;
using NzbDrone.Core.Tv.Parser;

namespace NzbDrone.Core.Tv.Downloads
{
    // Contract 5 for TV: attributes a tracked download to a series and its episodes. This is the
    // series half of Sonarr's TrackedDownloadService.TrackDownload.
    public class TvTrackedDownloadMatcher : ITrackedDownloadMatcher
    {
        private readonly IParsingService _parsingService;
        private readonly IHistoryService _historyService;
        private readonly ISeriesService _seriesService;
        private readonly IRemoteEpisodeAggregationService _aggregationService;
        private readonly ITvCustomFormatCalculationService _formatCalculator;
        private readonly Logger _logger;

        public TvTrackedDownloadMatcher(IParsingService parsingService,
                                        IHistoryService historyService,
                                        ISeriesService seriesService,
                                        IRemoteEpisodeAggregationService aggregationService,
                                        ITvCustomFormatCalculationService formatCalculator,
                                        Logger logger)
        {
            _parsingService = parsingService;
            _historyService = historyService;
            _seriesService = seriesService;
            _aggregationService = aggregationService;
            _formatCalculator = formatCalculator;
            _logger = logger;
        }

        public MediaKind Kind => MediaKind.Tv;

        public bool MatchesTitle(string title)
        {
            return Parser.Parser.ParseTitle(title) != null;
        }

        public void Match(TrackedDownload trackedDownload, DownloadHistory downloadHistory)
        {
            var downloadItem = trackedDownload.DownloadItem;

            try
            {
                var parsedEpisodeInfo = Parser.Parser.ParseTitle(downloadItem.Title);

                if (parsedEpisodeInfo != null)
                {
                    trackedDownload.RemoteRelease = downloadHistory is { EventType: DownloadHistoryEventType.DownloadImported }
                        ? _parsingService.Map(parsedEpisodeInfo, _seriesService.GetSeries(downloadHistory.ParentId))
                        : _parsingService.Map(parsedEpisodeInfo, 0, 0, null);
                }

                var historyItems = _historyService.FindByDownloadId(downloadItem.DownloadId)
                    .OrderByDescending(h => h.Date)
                    .ToList();

                if (historyItems.Any())
                {
                    var firstHistoryItem = historyItems.First();
                    var grabbedEvent = historyItems.FirstOrDefault(v => v.EventType == EpisodeHistoryEventType.Grabbed);

                    trackedDownload.Indexer = grabbedEvent?.Data?.GetValueOrDefault("indexer");
                    trackedDownload.Added = grabbedEvent?.Date;

                    var remoteEpisode = trackedDownload.GetRemoteEpisode();

                    if (parsedEpisodeInfo == null ||
                        remoteEpisode?.Series == null ||
                        remoteEpisode.Episodes.Empty())
                    {
                        // Try parsing the original source title and if that fails, try parsing it as a special
                        parsedEpisodeInfo = Parser.Parser.ParseTitle(firstHistoryItem.SourceTitle) ??
                                            _parsingService.ParseSpecialEpisodeTitle(parsedEpisodeInfo, firstHistoryItem.SourceTitle, 0, 0, null);

                        if (parsedEpisodeInfo != null)
                        {
                            trackedDownload.RemoteRelease = _parsingService.Map(parsedEpisodeInfo,
                                firstHistoryItem.ParentId,
                                historyItems.Where(v => v.EventType == EpisodeHistoryEventType.Grabbed)
                                    .Select(h => h.ItemId).Distinct());
                        }
                    }

                    remoteEpisode = trackedDownload.GetRemoteEpisode();

                    if (remoteEpisode != null)
                    {
                        remoteEpisode.Release ??= new ReleaseInfo();
                        remoteEpisode.Release.Indexer = trackedDownload.Indexer;
                        remoteEpisode.Release.Title = remoteEpisode.ParsedEpisodeInfo?.ReleaseTitle;

                        if (Enum.TryParse(grabbedEvent?.Data?.GetValueOrDefault("indexerFlags"), true, out IndexerFlags flags))
                        {
                            remoteEpisode.Release.AddIndexerFlags(flags);
                        }

                        if (downloadHistory != null)
                        {
                            remoteEpisode.Release.IndexerId = downloadHistory.IndexerId;
                        }
                    }
                }

                var matched = trackedDownload.GetRemoteEpisode();

                if (matched != null)
                {
                    _aggregationService.Augment(matched);

                    // Calculate custom formats
                    matched.CustomFormats = _formatCalculator.ParseCustomFormat(matched, downloadItem.TotalSize);
                }

                // Track it so it can be displayed in the queue even though we can't determine which series it is for
                if (matched == null)
                {
                    _logger.Trace("No Episode found for download '{0}'", downloadItem.Title);
                }
            }
            catch (MultipleSeriesFoundException e)
            {
                _logger.Debug(e, "Found multiple series for " + downloadItem.Title);

                trackedDownload.Warn("Unable to import automatically, found multiple series: {0}", string.Join(", ", e.Series));
            }
            catch (Exception e)
            {
                _logger.Debug(e, "Failed to find episode for " + downloadItem.Title);

                trackedDownload.Warn("Unable to parse episodes from title");
            }
        }

        public void Rematch(TrackedDownload trackedDownload)
        {
            var parsedEpisodeInfo = Parser.Parser.ParseTitle(trackedDownload.DownloadItem.Title);
            var remoteEpisode = parsedEpisodeInfo == null ? null : _parsingService.Map(parsedEpisodeInfo, 0, 0, null);

            trackedDownload.RemoteRelease = remoteEpisode;

            _aggregationService.Augment(remoteEpisode);
        }
    }
}
