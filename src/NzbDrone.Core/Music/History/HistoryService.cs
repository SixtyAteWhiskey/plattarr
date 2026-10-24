using System.Collections.Generic;

namespace NzbDrone.Core.Music.History
{
    public interface IHistoryService
    {
        List<AlbumHistory> GetByArtist(int artistId, AlbumHistoryEventType? eventType);
        List<AlbumHistory> GetByAlbum(int albumId, AlbumHistoryEventType? eventType);
        AlbumHistory MostRecentForAlbum(int albumId);
        List<AlbumHistory> FindByAlbumId(int albumId);
        void UpdateMany(List<AlbumHistory> toUpdate);
    }

    // The reads and updates the metadata refresh needs (it re-points history rows when an artist or
    // album gets a new foreign ID). The event handlers that write history (grab, import, failure,
    // deletion) are the activity step (step 6), which adds them to this class.
    public class HistoryService : IHistoryService
    {
        private readonly IAlbumHistoryRepository _historyRepository;

        public HistoryService(IAlbumHistoryRepository historyRepository)
        {
            _historyRepository = historyRepository;
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

        public void UpdateMany(List<AlbumHistory> toUpdate)
        {
            _historyRepository.UpdateMany(toUpdate);
        }
    }
}
