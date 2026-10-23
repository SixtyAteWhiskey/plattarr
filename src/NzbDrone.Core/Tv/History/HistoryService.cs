using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv.Events;
using NzbDrone.Core.Tv.MediaFiles.Events;

namespace NzbDrone.Core.Tv.History
{
    public interface IHistoryService
    {
        PagingSpec<EpisodeHistory> Paged(PagingSpec<EpisodeHistory> pagingSpec, int[] languages, int[] qualities);
        EpisodeHistory MostRecentForEpisode(int episodeId);
        List<EpisodeHistory> FindByEpisodeId(int episodeId);
        EpisodeHistory MostRecentForDownloadId(string downloadId);
        EpisodeHistory Get(int historyId);
        List<EpisodeHistory> GetBySeries(int seriesId, EpisodeHistoryEventType? eventType);
        List<EpisodeHistory> GetBySeason(int seriesId, int seasonNumber, EpisodeHistoryEventType? eventType);
        List<EpisodeHistory> GetByEpisode(int episodeId, EpisodeHistoryEventType? eventType);
        List<EpisodeHistory> Find(string downloadId, EpisodeHistoryEventType eventType);
        List<EpisodeHistory> FindByDownloadId(string downloadId);
        string FindDownloadId(EpisodeImportedEvent trackedDownload);
        List<EpisodeHistory> Since(DateTime date, EpisodeHistoryEventType? eventType);
    }

    // Sonarr's history service. One row per episode: a grab or an import of a multi-episode release
    // writes a row for each episode it covered. Handlers act on the TV kind only; the core publishes
    // the failed and ignored events for every module.
    public class HistoryService : IHistoryService,
                                  IHandle<ReleaseGrabbedEvent>,
                                  IHandle<EpisodeImportedEvent>,
                                  IHandle<DownloadFailedEvent>,
                                  IHandle<EpisodeFileDeletedEvent>,
                                  IHandle<EpisodeFileRenamedEvent>,
                                  IHandle<SeriesDeletedEvent>,
                                  IHandle<DownloadIgnoredEvent>
    {
        private readonly IHistoryRepository _historyRepository;
        private readonly Logger _logger;

        public HistoryService(IHistoryRepository historyRepository, Logger logger)
        {
            _historyRepository = historyRepository;
            _logger = logger;
        }

        public PagingSpec<EpisodeHistory> Paged(PagingSpec<EpisodeHistory> pagingSpec, int[] languages, int[] qualities)
        {
            return _historyRepository.GetPaged(pagingSpec, languages, qualities);
        }

        public EpisodeHistory MostRecentForEpisode(int episodeId)
        {
            return _historyRepository.MostRecentForEpisode(episodeId);
        }

        public List<EpisodeHistory> FindByEpisodeId(int episodeId)
        {
            return _historyRepository.FindByEpisodeId(episodeId);
        }

        public EpisodeHistory MostRecentForDownloadId(string downloadId)
        {
            return _historyRepository.MostRecentForDownloadId(downloadId);
        }

        public EpisodeHistory Get(int historyId)
        {
            return _historyRepository.Get(historyId);
        }

        public List<EpisodeHistory> GetBySeries(int seriesId, EpisodeHistoryEventType? eventType)
        {
            return _historyRepository.GetBySeries(seriesId, eventType);
        }

        public List<EpisodeHistory> GetBySeason(int seriesId, int seasonNumber, EpisodeHistoryEventType? eventType)
        {
            return _historyRepository.GetBySeason(seriesId, seasonNumber, eventType);
        }

        public List<EpisodeHistory> GetByEpisode(int episodeId, EpisodeHistoryEventType? eventType)
        {
            return _historyRepository.GetByEpisode(episodeId, eventType);
        }

        public List<EpisodeHistory> Find(string downloadId, EpisodeHistoryEventType eventType)
        {
            return _historyRepository.FindByDownloadId(downloadId).Where(c => c.EventType == eventType).ToList();
        }

        public List<EpisodeHistory> FindByDownloadId(string downloadId)
        {
            return _historyRepository.FindByDownloadId(downloadId);
        }

        public string FindDownloadId(EpisodeImportedEvent trackedDownload)
        {
            _logger.Debug("Trying to find downloadId for {0} from history", trackedDownload.ImportedEpisode.Path);

            var episodeIds = trackedDownload.EpisodeInfo.Episodes.Select(c => c.Id).ToList();
            var allHistory = _historyRepository.FindDownloadHistory(trackedDownload.EpisodeInfo.Series.Id, trackedDownload.ImportedEpisode.Quality);

            // Find download related items for these episodes
            var episodesHistory = allHistory.Where(h => episodeIds.Contains(h.ItemId)).ToList();

            var processedDownloadId = episodesHistory
                .Where(c => c.EventType != EpisodeHistoryEventType.Grabbed && c.DownloadId != null)
                .Select(c => c.DownloadId);

            var stillDownloading = episodesHistory.Where(c => c.EventType == EpisodeHistoryEventType.Grabbed && !processedDownloadId.Contains(c.DownloadId)).ToList();

            string downloadId = null;

            if (stillDownloading.Any())
            {
                foreach (var matchingHistory in trackedDownload.EpisodeInfo.Episodes.Select(e => stillDownloading.Where(c => c.ItemId == e.Id).ToList()))
                {
                    if (matchingHistory.Count != 1)
                    {
                        return null;
                    }

                    var newDownloadId = matchingHistory.Single().DownloadId;

                    if (downloadId == null || downloadId == newDownloadId)
                    {
                        downloadId = newDownloadId;
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            return downloadId;
        }

        public void Handle(ReleaseGrabbedEvent message)
        {
            if (message.Release.Kind != MediaKind.Tv)
            {
                return;
            }

            var remoteEpisode = message.GetRemoteEpisode();

            foreach (var episode in remoteEpisode.Episodes)
            {
                var history = new EpisodeHistory
                {
                    MediaKind = MediaKind.Tv,
                    EventType = EpisodeHistoryEventType.Grabbed,
                    Date = DateTime.UtcNow,
                    Quality = remoteEpisode.ParsedEpisodeInfo.Quality,
                    SourceTitle = remoteEpisode.Release.Title,
                    ParentId = episode.SeriesId,
                    ItemId = episode.Id,
                    DownloadId = message.DownloadId,
                    Languages = remoteEpisode.Languages,
                };

                history.Data.Add("Indexer", remoteEpisode.Release.Indexer);
                history.Data.Add("NzbInfoUrl", remoteEpisode.Release.InfoUrl);
                history.Data.Add("ReleaseGroup", remoteEpisode.ParsedEpisodeInfo.ReleaseGroup);
                history.Data.Add("Age", remoteEpisode.Release.Age.ToString());
                history.Data.Add("AgeHours", remoteEpisode.Release.AgeHours.ToString());
                history.Data.Add("AgeMinutes", remoteEpisode.Release.AgeMinutes.ToString());
                history.Data.Add("PublishedDate", remoteEpisode.Release.PublishDate.ToUniversalTime().ToString("s") + "Z");
                history.Data.Add("DownloadClient", message.DownloadClient);
                history.Data.Add("DownloadClientName", message.DownloadClientName);
                history.Data.Add("Size", remoteEpisode.Release.Size.GetValueOrDefault().ToString());
                history.Data.Add("DownloadUrl", remoteEpisode.Release.DownloadUrl);
                history.Data.Add("Guid", remoteEpisode.Release.Guid);
                history.Data.Add("TvdbId", remoteEpisode.Release.TvdbId.ToString());
                history.Data.Add("TvRageId", remoteEpisode.Release.TvRageId.ToString());
                history.Data.Add("ImdbId", remoteEpisode.Release.ImdbId.ToString());
                history.Data.Add("Protocol", ((int)remoteEpisode.Release.DownloadProtocol).ToString());
                history.Data.Add("CustomFormatScore", remoteEpisode.CustomFormatScore.ToString());
                history.Data.Add("SeriesMatchType", remoteEpisode.SeriesMatchType.ToString());
                history.Data.Add("ReleaseSource", remoteEpisode.ReleaseSource.ToString());
                history.Data.Add("IndexerFlags", remoteEpisode.Release.ToIndexerFlags().ToString());
                history.Data.Add("IndexerId", remoteEpisode.Release.IndexerId.ToString());
                history.Data.Add("ReleaseType", remoteEpisode.ParsedEpisodeInfo.ReleaseType.ToString());

                if (!remoteEpisode.ParsedEpisodeInfo.ReleaseHash.IsNullOrWhiteSpace())
                {
                    history.Data.Add("ReleaseHash", remoteEpisode.ParsedEpisodeInfo.ReleaseHash);
                }

                if (remoteEpisode.Release is TorrentInfo torrentRelease)
                {
                    history.Data.Add("TorrentInfoHash", torrentRelease.InfoHash);
                }

                _historyRepository.Insert(history);
            }
        }

        public void Handle(EpisodeImportedEvent message)
        {
            if (!message.NewDownload)
            {
                return;
            }

            var downloadId = message.DownloadId;

            if (downloadId.IsNullOrWhiteSpace())
            {
                downloadId = FindDownloadId(message);
            }

            foreach (var episode in message.EpisodeInfo.Episodes)
            {
                var history = new EpisodeHistory
                {
                    MediaKind = MediaKind.Tv,
                    EventType = EpisodeHistoryEventType.DownloadFolderImported,
                    Date = DateTime.UtcNow,
                    Quality = message.EpisodeInfo.Quality,
                    SourceTitle = message.ImportedEpisode.SceneName ?? Path.GetFileNameWithoutExtension(message.EpisodeInfo.Path),
                    ParentId = message.ImportedEpisode.SeriesId,
                    ItemId = episode.Id,
                    DownloadId = downloadId,
                    Languages = message.EpisodeInfo.Languages
                };

                history.Data.Add("FileId", message.ImportedEpisode.Id.ToString());
                history.Data.Add("DroppedPath", message.EpisodeInfo.Path);
                history.Data.Add("ImportedPath", Path.Combine(message.EpisodeInfo.Series.Path, message.ImportedEpisode.RelativePath));
                history.Data.Add("DownloadClient", message.DownloadClientInfo?.Type);
                history.Data.Add("DownloadClientName", message.DownloadClientInfo?.Name);
                history.Data.Add("ReleaseGroup", message.EpisodeInfo.ReleaseGroup);
                history.Data.Add("CustomFormatScore", message.EpisodeInfo.CustomFormatScore.ToString());
                history.Data.Add("Size", message.EpisodeInfo.Size.ToString());
                history.Data.Add("IndexerFlags", message.ImportedEpisode.IndexerFlags.ToString());
                history.Data.Add("ReleaseType", message.ImportedEpisode.ReleaseType.ToString());

                _historyRepository.Insert(history);
            }
        }

        public void Handle(DownloadFailedEvent message)
        {
            if (message.Kind != MediaKind.Tv)
            {
                return;
            }

            foreach (var episodeId in message.ItemIds)
            {
                var history = new EpisodeHistory
                {
                    MediaKind = MediaKind.Tv,
                    EventType = EpisodeHistoryEventType.DownloadFailed,
                    Date = DateTime.UtcNow,
                    Quality = message.Quality,
                    SourceTitle = message.SourceTitle,
                    ParentId = message.ParentId,
                    ItemId = episodeId,
                    DownloadId = message.DownloadId,
                    Languages = message.Languages
                };

                history.Data.Add("DownloadClient", message.DownloadClient);
                history.Data.Add("DownloadClientName", message.TrackedDownload?.DownloadItem.DownloadClientInfo.Name);
                history.Data.Add("Message", message.Message);
                history.Data.Add("ReleaseGroup", message.TrackedDownload?.GetRemoteEpisode()?.ParsedEpisodeInfo?.ReleaseGroup ?? message.Data.GetValueOrDefault(EpisodeHistory.RELEASE_GROUP));
                history.Data.Add("Size", message.TrackedDownload?.DownloadItem.TotalSize.ToString() ?? message.Data.GetValueOrDefault(EpisodeHistory.SIZE));
                history.Data.Add("Indexer", message.TrackedDownload?.GetRemoteEpisode()?.Release?.Indexer ?? message.Data.GetValueOrDefault(EpisodeHistory.INDEXER));

                _historyRepository.Insert(history);
            }
        }

        public void Handle(EpisodeFileDeletedEvent message)
        {
            if (message.Reason == DeleteMediaFileReason.NoLinkedEpisodes)
            {
                _logger.Debug("Removing episode file from DB as part of cleanup routine, not creating history event.");
                return;
            }
            else if (message.Reason == DeleteMediaFileReason.ManualOverride)
            {
                _logger.Debug("Removing episode file from DB as part of manual override of existing file, not creating history event.");
                return;
            }

            foreach (var episode in message.EpisodeFile.Episodes.Value)
            {
                var history = new EpisodeHistory
                {
                    MediaKind = MediaKind.Tv,
                    EventType = EpisodeHistoryEventType.EpisodeFileDeleted,
                    Date = DateTime.UtcNow,
                    Quality = message.EpisodeFile.Quality,
                    SourceTitle = message.EpisodeFile.Path,
                    ParentId = message.EpisodeFile.SeriesId,
                    ItemId = episode.Id,
                    Languages = message.EpisodeFile.Languages
                };

                history.Data.Add("Reason", message.Reason.ToString());
                history.Data.Add("ReleaseGroup", message.EpisodeFile.ReleaseGroup);
                history.Data.Add("Size", message.EpisodeFile.Size.ToString());
                history.Data.Add("IndexerFlags", message.EpisodeFile.IndexerFlags.ToString());
                history.Data.Add("ReleaseType", message.EpisodeFile.ReleaseType.ToString());

                _historyRepository.Insert(history);
            }
        }

        public void Handle(EpisodeFileRenamedEvent message)
        {
            var sourcePath = message.OriginalPath;
            var sourceRelativePath = message.Series.Path.GetRelativePath(message.OriginalPath);
            var path = Path.Combine(message.Series.Path, message.EpisodeFile.RelativePath);
            var relativePath = message.EpisodeFile.RelativePath;

            foreach (var episode in message.EpisodeFile.Episodes.Value)
            {
                var history = new EpisodeHistory
                {
                    MediaKind = MediaKind.Tv,
                    EventType = EpisodeHistoryEventType.EpisodeFileRenamed,
                    Date = DateTime.UtcNow,
                    Quality = message.EpisodeFile.Quality,
                    SourceTitle = message.OriginalPath,
                    ParentId = message.EpisodeFile.SeriesId,
                    ItemId = episode.Id,
                    Languages = message.EpisodeFile.Languages
                };

                history.Data.Add("SourcePath", sourcePath);
                history.Data.Add("SourceRelativePath", sourceRelativePath);
                history.Data.Add("Path", path);
                history.Data.Add("RelativePath", relativePath);
                history.Data.Add("ReleaseGroup", message.EpisodeFile.ReleaseGroup);
                history.Data.Add("Size", message.EpisodeFile.Size.ToString());
                history.Data.Add("IndexerFlags", message.EpisodeFile.IndexerFlags.ToString());
                history.Data.Add("ReleaseType", message.EpisodeFile.ReleaseType.ToString());

                _historyRepository.Insert(history);
            }
        }

        public void Handle(DownloadIgnoredEvent message)
        {
            if (message.Kind != MediaKind.Tv)
            {
                return;
            }

            var historyToAdd = new List<EpisodeHistory>();

            foreach (var episodeId in message.ItemIds)
            {
                var history = new EpisodeHistory
                {
                    MediaKind = MediaKind.Tv,
                    EventType = EpisodeHistoryEventType.DownloadIgnored,
                    Date = DateTime.UtcNow,
                    Quality = message.Quality,
                    SourceTitle = message.SourceTitle,
                    ParentId = message.ParentId,
                    ItemId = episodeId,
                    DownloadId = message.DownloadId,
                    Languages = message.Languages
                };

                history.Data.Add("DownloadClient", message.DownloadClientInfo.Type);
                history.Data.Add("DownloadClientName", message.DownloadClientInfo.Name);
                history.Data.Add("Message", message.Message);
                history.Data.Add("ReleaseGroup", message.TrackedDownload?.GetRemoteEpisode()?.ParsedEpisodeInfo?.ReleaseGroup);
                history.Data.Add("Size", message.TrackedDownload?.DownloadItem.TotalSize.ToString());
                history.Data.Add("Indexer", message.TrackedDownload?.GetRemoteEpisode()?.Release?.Indexer);
                history.Data.Add("ReleaseType", message.TrackedDownload?.GetRemoteEpisode()?.ParsedEpisodeInfo?.ReleaseType.ToString());

                historyToAdd.Add(history);
            }

            _historyRepository.InsertMany(historyToAdd);
        }

        public void Handle(SeriesDeletedEvent message)
        {
            _historyRepository.DeleteForSeries(message.Series.Select(m => m.Id).ToList());
        }

        public List<EpisodeHistory> Since(DateTime date, EpisodeHistoryEventType? eventType)
        {
            return _historyRepository.Since(date, eventType);
        }
    }
}
