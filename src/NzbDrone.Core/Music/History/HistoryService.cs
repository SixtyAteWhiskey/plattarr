using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.Events;
using NzbDrone.Core.Music.MediaFiles.Events;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Music.History
{
    public interface IHistoryService
    {
        PagingSpec<AlbumHistory> Paged(PagingSpec<AlbumHistory> pagingSpec, int[] qualities);
        AlbumHistory Get(int historyId);
        List<AlbumHistory> GetByArtist(int artistId, AlbumHistoryEventType? eventType);
        List<AlbumHistory> GetByAlbum(int albumId, AlbumHistoryEventType? eventType);
        AlbumHistory MostRecentForAlbum(int albumId);
        List<AlbumHistory> FindByAlbumId(int albumId);
        AlbumHistory MostRecentForDownloadId(string downloadId);
        List<AlbumHistory> FindByDownloadId(string downloadId);
        List<AlbumHistory> Find(string downloadId, AlbumHistoryEventType eventType);
        string FindDownloadId(TrackImportedEvent trackedDownload);
        List<AlbumHistory> Since(DateTime date, AlbumHistoryEventType? eventType);
        void UpdateMany(List<AlbumHistory> toUpdate);
    }

    // Lidarr's EntityHistoryService. A grab, a failure or an ignored download writes one row per album
    // the release covered; an imported, deleted, renamed or retagged file writes one row per track
    // (the track is in Data under "trackId"). Handlers act on the Music kind only: the core
    // publishes the failed and ignored events for every module.
    //
    // Lidarr's AlbumImportIncomplete row is not written: the event that raised it was not ported (the
    // core state machine blocks a partial import for manual import, see step 5).
    public class HistoryService : IHistoryService,
                                  IHandle<ReleaseGrabbedEvent>,
                                  IHandle<TrackImportedEvent>,
                                  IHandle<DownloadFailedEvent>,
                                  IHandle<DownloadCompletedEvent>,
                                  IHandle<TrackFileDeletedEvent>,
                                  IHandle<TrackFileRenamedEvent>,
                                  IHandle<TrackFileRetaggedEvent>,
                                  IHandle<ArtistsDeletedEvent>,
                                  IHandle<DownloadIgnoredEvent>
    {
        private readonly IAlbumHistoryRepository _historyRepository;

        private readonly Logger _logger;

        public HistoryService(IAlbumHistoryRepository historyRepository, Logger logger)
        {
            _historyRepository = historyRepository;
            _logger = logger;
        }

        public PagingSpec<AlbumHistory> Paged(PagingSpec<AlbumHistory> pagingSpec, int[] qualities)
        {
            return _historyRepository.GetPaged(pagingSpec, qualities);
        }

        public AlbumHistory Get(int historyId)
        {
            return _historyRepository.Get(historyId);
        }

        public List<AlbumHistory> GetByArtist(int artistId, AlbumHistoryEventType? eventType)
        {
            return _historyRepository.GetByArtist(artistId, eventType);
        }

        public List<AlbumHistory> GetByAlbum(int albumId, AlbumHistoryEventType? eventType)
        {
            return _historyRepository.GetByAlbum(albumId, eventType);
        }

        public AlbumHistory MostRecentForAlbum(int albumId)
        {
            return _historyRepository.MostRecentForAlbum(albumId);
        }

        public List<AlbumHistory> FindByAlbumId(int albumId)
        {
            return _historyRepository.FindByAlbumId(albumId);
        }

        public AlbumHistory MostRecentForDownloadId(string downloadId)
        {
            return _historyRepository.MostRecentForDownloadId(downloadId);
        }

        public List<AlbumHistory> FindByDownloadId(string downloadId)
        {
            return _historyRepository.FindByDownloadId(downloadId);
        }

        public List<AlbumHistory> Find(string downloadId, AlbumHistoryEventType eventType)
        {
            return FindByDownloadId(downloadId).Where(c => c.EventType == eventType).ToList();
        }

        // Lidarr's EntityHistoryService.FindDownloadId: the download a manually imported file belongs to,
        // found from the artist's grab history. Null unless exactly one grab of each album is still open.
        public string FindDownloadId(TrackImportedEvent trackedDownload)
        {
            _logger.Debug("Trying to find downloadId for {0} from history", trackedDownload.ImportedTrack.Path);

            var albumIds = trackedDownload.TrackInfo.Tracks.Select(c => c.AlbumId).ToList();

            var allHistory = _historyRepository.FindDownloadHistory(trackedDownload.TrackInfo.Artist.Id, trackedDownload.ImportedTrack.Quality);

            // Find download related items for these albums
            var albumsHistory = allHistory.Where(h => albumIds.Contains(h.ItemId)).ToList();

            var processedDownloadId = albumsHistory
                .Where(c => c.EventType != AlbumHistoryEventType.Grabbed && c.DownloadId != null)
                .Select(c => c.DownloadId);

            var stillDownloading = albumsHistory.Where(c => c.EventType == AlbumHistoryEventType.Grabbed && !processedDownloadId.Contains(c.DownloadId)).ToList();

            string downloadId = null;

            if (stillDownloading.Any())
            {
                foreach (var matchingHistory in trackedDownload.TrackInfo.Tracks.Select(e => stillDownloading.Where(c => c.ItemId == e.AlbumId).ToList()))
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
            if (message.Release.Kind != MediaKind.Music)
            {
                return;
            }

            var remoteAlbum = message.GetRemoteAlbum();

            foreach (var album in remoteAlbum.Albums)
            {
                var history = new AlbumHistory
                {
                    MediaKind = MediaKind.Music,
                    EventType = AlbumHistoryEventType.Grabbed,
                    Date = DateTime.UtcNow,
                    Quality = remoteAlbum.ParsedAlbumInfo.Quality,
                    SourceTitle = remoteAlbum.Release.Title,
                    ParentId = remoteAlbum.Artist.Id,
                    ItemId = album.Id,
                    DownloadId = message.DownloadId
                };

                history.Data.Add("Indexer", remoteAlbum.Release.Indexer);
                history.Data.Add("NzbInfoUrl", remoteAlbum.Release.InfoUrl);
                history.Data.Add("ReleaseGroup", remoteAlbum.ParsedAlbumInfo.ReleaseGroup);
                history.Data.Add("Age", remoteAlbum.Release.Age.ToString());
                history.Data.Add("AgeHours", remoteAlbum.Release.AgeHours.ToString());
                history.Data.Add("AgeMinutes", remoteAlbum.Release.AgeMinutes.ToString());
                history.Data.Add("PublishedDate", remoteAlbum.Release.PublishDate.ToUniversalTime().ToString("s") + "Z");
                history.Data.Add("DownloadClient", message.DownloadClient);
                history.Data.Add("DownloadClientName", message.DownloadClientName);

                // Size is nullable on a release; the blocklist parses it back, so a missing size is written as 0.
                history.Data.Add("Size", remoteAlbum.Release.Size.GetValueOrDefault().ToString());
                history.Data.Add("DownloadUrl", remoteAlbum.Release.DownloadUrl);
                history.Data.Add("Guid", remoteAlbum.Release.Guid);
                history.Data.Add("Protocol", ((int)remoteAlbum.Release.DownloadProtocol).ToString());
                history.Data.Add("DownloadForced", (!remoteAlbum.DownloadAllowed).ToString());
                history.Data.Add("CustomFormatScore", remoteAlbum.CustomFormatScore.ToString());
                history.Data.Add("ReleaseSource", remoteAlbum.ReleaseSource.ToString());
                history.Data.Add("IndexerFlags", remoteAlbum.Release.ToIndexerFlags().ToString());
                history.Data.Add("IndexerId", remoteAlbum.Release.IndexerId.ToString());

                if (!remoteAlbum.ParsedAlbumInfo.ReleaseHash.IsNullOrWhiteSpace())
                {
                    history.Data.Add("ReleaseHash", remoteAlbum.ParsedAlbumInfo.ReleaseHash);
                }

                if (remoteAlbum.Release is TorrentInfo torrentRelease)
                {
                    history.Data.Add("TorrentInfoHash", torrentRelease.InfoHash);
                }

                _historyRepository.Insert(history);
            }
        }

        public void Handle(TrackImportedEvent message)
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

            foreach (var track in message.TrackInfo.Tracks)
            {
                var history = new AlbumHistory
                {
                    MediaKind = MediaKind.Music,
                    EventType = AlbumHistoryEventType.TrackFileImported,
                    Date = DateTime.UtcNow,
                    Quality = message.TrackInfo.Quality,
                    SourceTitle = message.ImportedTrack.SceneName ?? Path.GetFileNameWithoutExtension(message.TrackInfo.Path),
                    ParentId = message.TrackInfo.Artist.Id,
                    ItemId = message.TrackInfo.Album.Id,
                    DownloadId = downloadId
                };

                history.Data.Add(AlbumHistory.TRACK_ID, track.Id.ToString());
                history.Data.Add("FileId", message.ImportedTrack.Id.ToString());
                history.Data.Add("DroppedPath", message.TrackInfo.Path);
                history.Data.Add("ImportedPath", message.ImportedTrack.Path);
                history.Data.Add("DownloadClient", message.DownloadClientInfo?.Type);
                history.Data.Add("DownloadClientName", message.DownloadClientInfo?.Name);
                history.Data.Add("ReleaseGroup", message.TrackInfo.ReleaseGroup);
                history.Data.Add("Size", message.TrackInfo.Size.ToString());
                history.Data.Add("IndexerFlags", message.ImportedTrack.IndexerFlags.ToString());

                _historyRepository.Insert(history);
            }
        }

        public void Handle(DownloadFailedEvent message)
        {
            if (message.Kind != MediaKind.Music)
            {
                return;
            }

            var remoteAlbum = message.TrackedDownload?.GetRemoteAlbum();

            foreach (var albumId in message.ItemIds)
            {
                var history = new AlbumHistory
                {
                    MediaKind = MediaKind.Music,
                    EventType = AlbumHistoryEventType.DownloadFailed,
                    Date = DateTime.UtcNow,
                    Quality = AudioQualityModel.FromDisplayModel(message.Quality),
                    SourceTitle = message.SourceTitle,
                    ParentId = message.ParentId,
                    ItemId = albumId,
                    DownloadId = message.DownloadId
                };

                history.Data.Add("DownloadClient", message.DownloadClient);
                history.Data.Add("DownloadClientName", message.TrackedDownload?.DownloadItem?.DownloadClientInfo?.Name);
                history.Data.Add("Message", message.Message);
                history.Data.Add("ReleaseGroup", remoteAlbum?.ParsedAlbumInfo?.ReleaseGroup ?? message.Data.GetValueOrDefault(AlbumHistory.RELEASE_GROUP));
                history.Data.Add("Size", message.TrackedDownload?.DownloadItem?.TotalSize.ToString() ?? message.Data.GetValueOrDefault(AlbumHistory.SIZE));
                history.Data.Add("Indexer", remoteAlbum?.Release?.Indexer ?? message.Data.GetValueOrDefault(AlbumHistory.INDEXER));

                _historyRepository.Insert(history);
            }
        }

        // The core marks a download imported once every album of it is in. Lidarr's DownloadImported row
        // is what TrackedDownloadAlreadyImported reads to recognise a finished download.
        public void Handle(DownloadCompletedEvent message)
        {
            if (message.TrackedDownload.Kind != MediaKind.Music)
            {
                return;
            }

            var remoteAlbum = message.TrackedDownload.GetRemoteAlbum();

            if (remoteAlbum == null)
            {
                return;
            }

            foreach (var album in remoteAlbum.Albums)
            {
                var history = new AlbumHistory
                {
                    MediaKind = MediaKind.Music,
                    EventType = AlbumHistoryEventType.DownloadImported,
                    Date = DateTime.UtcNow,
                    Quality = remoteAlbum.ParsedAlbumInfo?.Quality ?? new AudioQualityModel(),
                    SourceTitle = message.TrackedDownload.DownloadItem.Title,
                    ParentId = remoteAlbum.Artist.Id,
                    ItemId = album.Id,
                    DownloadId = message.TrackedDownload.DownloadItem.DownloadId
                };

                history.Data.Add("ReleaseGroup", remoteAlbum.ParsedAlbumInfo?.ReleaseGroup);

                _historyRepository.Insert(history);
            }
        }

        public void Handle(TrackFileDeletedEvent message)
        {
            if (message.Reason == DeleteMediaFileReason.NoLinkedEpisodes)
            {
                _logger.Debug("Removing track file from DB as part of cleanup routine, not creating history event.");
                return;
            }
            else if (message.Reason == DeleteMediaFileReason.ManualOverride)
            {
                _logger.Debug("Removing track file from DB as part of manual override of existing file, not creating history event.");
                return;
            }

            // The artist is gone when its metadata was removed first; there is nothing to attach a row to.
            var artist = message.TrackFile.Artist?.Value;

            if (artist == null)
            {
                return;
            }

            foreach (var track in message.TrackFile.Tracks.Value)
            {
                var history = new AlbumHistory
                {
                    MediaKind = MediaKind.Music,
                    EventType = AlbumHistoryEventType.TrackFileDeleted,
                    Date = DateTime.UtcNow,
                    Quality = message.TrackFile.Quality,
                    SourceTitle = message.TrackFile.Path,
                    ParentId = artist.Id,
                    ItemId = message.TrackFile.AlbumId
                };

                history.Data.Add(AlbumHistory.TRACK_ID, track.Id.ToString());
                history.Data.Add("Reason", message.Reason.ToString());
                history.Data.Add("ReleaseGroup", message.TrackFile.ReleaseGroup);
                history.Data.Add("Size", message.TrackFile.Size.ToString());
                history.Data.Add("IndexerFlags", message.TrackFile.IndexerFlags.ToString());

                _historyRepository.Insert(history);
            }
        }

        public void Handle(TrackFileRenamedEvent message)
        {
            var sourcePath = message.OriginalPath;
            var path = message.TrackFile.Path;

            foreach (var track in message.TrackFile.Tracks.Value)
            {
                var history = new AlbumHistory
                {
                    MediaKind = MediaKind.Music,
                    EventType = AlbumHistoryEventType.TrackFileRenamed,
                    Date = DateTime.UtcNow,
                    Quality = message.TrackFile.Quality,
                    SourceTitle = message.OriginalPath,
                    ParentId = message.Artist.Id,
                    ItemId = message.TrackFile.AlbumId
                };

                history.Data.Add(AlbumHistory.TRACK_ID, track.Id.ToString());
                history.Data.Add("SourcePath", sourcePath);
                history.Data.Add("Path", path);
                history.Data.Add("ReleaseGroup", message.TrackFile.ReleaseGroup);
                history.Data.Add("Size", message.TrackFile.Size.ToString());
                history.Data.Add("IndexerFlags", message.TrackFile.IndexerFlags.ToString());

                _historyRepository.Insert(history);
            }
        }

        public void Handle(TrackFileRetaggedEvent message)
        {
            var path = message.TrackFile.Path;

            foreach (var track in message.TrackFile.Tracks.Value)
            {
                var history = new AlbumHistory
                {
                    MediaKind = MediaKind.Music,
                    EventType = AlbumHistoryEventType.TrackFileRetagged,
                    Date = DateTime.UtcNow,
                    Quality = message.TrackFile.Quality,
                    SourceTitle = path,
                    ParentId = message.Artist.Id,
                    ItemId = message.TrackFile.AlbumId
                };

                history.Data.Add(AlbumHistory.TRACK_ID, track.Id.ToString());
                history.Data.Add("TagsScrubbed", message.Scrubbed.ToString());
                history.Data.Add("ReleaseGroup", message.TrackFile.ReleaseGroup);
                history.Data.Add("Diff", message.Diff.Select(x => new
                {
                    Field = x.Key,
                    OldValue = x.Value.Item1,
                    NewValue = x.Value.Item2
                }).ToJson());

                _historyRepository.Insert(history);
            }
        }

        public void Handle(ArtistsDeletedEvent message)
        {
            _historyRepository.DeleteForArtists(message.Artists.Select(x => x.Id).ToList());
        }

        public void Handle(DownloadIgnoredEvent message)
        {
            if (message.Kind != MediaKind.Music)
            {
                return;
            }

            var remoteAlbum = message.TrackedDownload?.GetRemoteAlbum();
            var historyToAdd = new List<AlbumHistory>();

            foreach (var albumId in message.ItemIds)
            {
                var history = new AlbumHistory
                {
                    MediaKind = MediaKind.Music,
                    EventType = AlbumHistoryEventType.DownloadIgnored,
                    Date = DateTime.UtcNow,
                    Quality = AudioQualityModel.FromDisplayModel(message.Quality),
                    SourceTitle = message.SourceTitle,
                    ParentId = message.ParentId,
                    ItemId = albumId,
                    DownloadId = message.DownloadId
                };

                history.Data.Add("DownloadClient", message.DownloadClientInfo?.Type);
                history.Data.Add("DownloadClientName", message.DownloadClientInfo?.Name);
                history.Data.Add("Message", message.Message);
                history.Data.Add("ReleaseGroup", remoteAlbum?.ParsedAlbumInfo?.ReleaseGroup);
                history.Data.Add("Size", message.TrackedDownload?.DownloadItem?.TotalSize.ToString());
                history.Data.Add("Indexer", remoteAlbum?.Release?.Indexer);

                historyToAdd.Add(history);
            }

            _historyRepository.InsertMany(historyToAdd);
        }

        public List<AlbumHistory> Since(DateTime date, AlbumHistoryEventType? eventType)
        {
            return _historyRepository.Since(date, eventType);
        }

        public void UpdateMany(List<AlbumHistory> toUpdate)
        {
            _historyRepository.UpdateMany(toUpdate);
        }
    }
}
