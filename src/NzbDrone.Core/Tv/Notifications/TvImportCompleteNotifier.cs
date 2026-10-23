using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Tv.History;
using NzbDrone.Core.Tv.MediaFiles;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv.Notifications
{
    // Sonarr's "On Import Complete": when the core marks a TV download imported, one message carries the
    // series, every episode and every file the download brought in. The core event holds no files, so they
    // come from the download's import history (one row per episode, written before the event).
    public class TvImportCompleteNotifier : IHandle<DownloadCompletedEvent>
    {
        private readonly INotificationDispatcher _dispatcher;
        private readonly TvActivityRenderer _renderer;
        private readonly IHistoryService _historyService;
        private readonly ISeriesService _seriesService;
        private readonly IEpisodeService _episodeService;
        private readonly IMediaFileService _mediaFileService;
        private readonly Logger _logger;

        public TvImportCompleteNotifier(INotificationDispatcher dispatcher,
                                        TvActivityRenderer renderer,
                                        IHistoryService historyService,
                                        ISeriesService seriesService,
                                        IEpisodeService episodeService,
                                        IMediaFileService mediaFileService,
                                        Logger logger)
        {
            _dispatcher = dispatcher;
            _renderer = renderer;
            _historyService = historyService;
            _seriesService = seriesService;
            _episodeService = episodeService;
            _mediaFileService = mediaFileService;
            _logger = logger;
        }

        public void Handle(DownloadCompletedEvent message)
        {
            var trackedDownload = message.TrackedDownload;

            if (trackedDownload.Kind != MediaKind.Tv || !_dispatcher.AnyEnabled(NotificationEvent.ImportComplete))
            {
                return;
            }

            var downloadId = trackedDownload.DownloadItem?.DownloadId;

            if (downloadId.IsNullOrWhiteSpace())
            {
                return;
            }

            var series = _seriesService.GetSeries(message.ParentId);
            var importedEpisodeIds = _historyService.Find(downloadId, EpisodeHistoryEventType.DownloadFolderImported)
                                                    .Where(h => h.ParentId == series.Id)
                                                    .Select(h => h.ItemId)
                                                    .Distinct()
                                                    .ToList();
            var episodes = _episodeService.GetEpisodes(importedEpisodeIds)
                                          .OrderBy(e => e.SeasonNumber)
                                          .ThenBy(e => e.EpisodeNumber)
                                          .ToList();
            var files = _mediaFileService.GetFiles(episodes.Where(e => e.EpisodeFileId > 0).Select(e => e.EpisodeFileId).Distinct())
                                         .OrderBy(f => f.RelativePath)
                                         .ToList();

            if (files.Empty())
            {
                _logger.Debug("No imported files found in history for download {0}; no import complete notification", downloadId);
                return;
            }

            var remoteEpisode = trackedDownload.GetRemoteEpisode();
            var quality = remoteEpisode?.ParsedEpisodeInfo?.Quality ?? files[0].Quality;
            var grabbed = _historyService.Find(downloadId, EpisodeHistoryEventType.Grabbed);
            var release = grabbed.Any() ? new GrabbedReleaseInfo(grabbed) : null;
            var fullSeason = remoteEpisode?.ParsedEpisodeInfo?.FullSeason == true;

            _dispatcher.ImportComplete(new ImportCompleteMessage
            {
                Message = fullSeason
                    ? TvNotificationHandler.GetFullSeasonMessage(series, episodes[0].SeasonNumber, quality)
                    : TvNotificationHandler.GetMessage(series, episodes, quality),
                Subject = _renderer.Describe(series, episodes),
                Data = new TvNotificationData
                {
                    Series = series,
                    Quality = quality,
                    Episodes = episodes,
                    EpisodeFiles = files,
                    Release = release
                },
                Files = files.Select(f => _renderer.DescribeFile(series, f)).ToList(),
                SourcePath = trackedDownload.DownloadItem.OutputPath.FullPath,
                DestinationPath = files.Select(f => Path.Join(series.Path, f.RelativePath)).ToList().GetLongestCommonPath(),
                DownloadClientInfo = trackedDownload.DownloadItem.DownloadClientInfo,
                DownloadId = downloadId,
                Release = _renderer.DescribeRelease(release),
                ReleaseGroup = remoteEpisode?.ParsedEpisodeInfo?.ReleaseGroup,
                QualityName = quality.Quality.Name
            });
        }
    }
}
