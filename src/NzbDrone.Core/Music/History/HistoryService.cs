using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Music.MediaFiles.Events;

namespace NzbDrone.Core.Music.History
{
    public interface IHistoryService
    {
        List<AlbumHistory> GetByArtist(int artistId, AlbumHistoryEventType? eventType);
        List<AlbumHistory> GetByAlbum(int albumId, AlbumHistoryEventType? eventType);
        AlbumHistory MostRecentForAlbum(int albumId);
        List<AlbumHistory> FindByAlbumId(int albumId);
        AlbumHistory MostRecentForDownloadId(string downloadId);
        List<AlbumHistory> FindByDownloadId(string downloadId);
        List<AlbumHistory> Find(string downloadId, AlbumHistoryEventType eventType);
        string FindDownloadId(TrackImportedEvent trackedDownload);
        void UpdateMany(List<AlbumHistory> toUpdate);
    }

    // The reads and updates the metadata refresh needs (it re-points history rows when an artist or
    // album gets a new foreign ID). The event handlers that write history (grab, import, failure,
    // deletion) are the activity step (step 6), which adds them to this class.
    public class HistoryService : IHistoryService
    {
        private readonly IAlbumHistoryRepository _historyRepository;

        private readonly Logger _logger;

        public HistoryService(IAlbumHistoryRepository historyRepository, Logger logger)
        {
            _historyRepository = historyRepository;
            _logger = logger;
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

        public void UpdateMany(List<AlbumHistory> toUpdate)
        {
            _historyRepository.UpdateMany(toUpdate);
        }
    }
}
