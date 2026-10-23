using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Tv.History
{
    // The reads the decision engine, the search and the API make. The event handlers that write
    // history (grab, import, failure, delete, rename, ignore) arrive with TV step 6.
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
        List<EpisodeHistory> Since(DateTime date, EpisodeHistoryEventType? eventType);
    }

    public class HistoryService : IHistoryService
    {
        private readonly IHistoryRepository _historyRepository;

        public HistoryService(IHistoryRepository historyRepository)
        {
            _historyRepository = historyRepository;
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

        public List<EpisodeHistory> Since(DateTime date, EpisodeHistoryEventType? eventType)
        {
            return _historyRepository.Since(date, eventType);
        }
    }
}
