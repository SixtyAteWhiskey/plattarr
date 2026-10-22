using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Tv.History
{
    public interface IHistoryRepository : IBasicRepository<EpisodeHistory>
    {
        EpisodeHistory MostRecentForEpisode(int episodeId);
        List<EpisodeHistory> FindByEpisodeId(int episodeId);
        EpisodeHistory MostRecentForDownloadId(string downloadId);
        List<EpisodeHistory> FindByDownloadId(string downloadId);
        List<EpisodeHistory> GetBySeries(int seriesId, EpisodeHistoryEventType? eventType);
        List<EpisodeHistory> GetBySeason(int seriesId, int seasonNumber, EpisodeHistoryEventType? eventType);
        List<EpisodeHistory> GetByEpisode(int episodeId, EpisodeHistoryEventType? eventType);
        List<EpisodeHistory> FindDownloadHistory(int seriesId, QualityModel quality);
        void DeleteForSeries(List<int> seriesIds);
        List<EpisodeHistory> Since(DateTime date, EpisodeHistoryEventType? eventType);
        PagingSpec<EpisodeHistory> GetPaged(PagingSpec<EpisodeHistory> pagingSpec, int[] languages, int[] qualities);
    }

    public class HistoryRepository : MediaKindRepository<EpisodeHistory>, IHistoryRepository
    {
        public HistoryRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator, MediaKind.Tv)
        {
        }

        public EpisodeHistory MostRecentForEpisode(int episodeId)
        {
            return Query(h => h.ItemId == episodeId).MaxBy(h => h.Date);
        }

        public List<EpisodeHistory> FindByEpisodeId(int episodeId)
        {
            return Query(h => h.ItemId == episodeId)
                .OrderByDescending(h => h.Date)
                .ToList();
        }

        public EpisodeHistory MostRecentForDownloadId(string downloadId)
        {
            return Query(h => h.DownloadId == downloadId).MaxBy(h => h.Date);
        }

        public List<EpisodeHistory> FindByDownloadId(string downloadId)
        {
            return Query(h => h.DownloadId == downloadId);
        }

        public List<EpisodeHistory> GetBySeries(int seriesId, EpisodeHistoryEventType? eventType)
        {
            var builder = Builder().Join<EpisodeHistory, Series>((h, s) => h.ParentId == s.Id)
                                   .Join<EpisodeHistory, Episode>((h, e) => h.ItemId == e.Id)
                                   .Where<EpisodeHistory>(h => h.ParentId == seriesId);

            if (eventType.HasValue)
            {
                builder.Where<EpisodeHistory>(h => h.EventType == eventType);
            }

            return PagedQuery(builder).OrderByDescending(h => h.Date).ToList();
        }

        public List<EpisodeHistory> GetBySeason(int seriesId, int seasonNumber, EpisodeHistoryEventType? eventType)
        {
            var builder = Builder()
                .Join<EpisodeHistory, Episode>((h, e) => h.ItemId == e.Id)
                .Join<EpisodeHistory, Series>((h, s) => h.ParentId == s.Id)
                .Where<EpisodeHistory>(h => h.ParentId == seriesId)
                .Where<Episode>(e => e.SeasonNumber == seasonNumber);

            if (eventType.HasValue)
            {
                builder.Where<EpisodeHistory>(h => h.EventType == eventType);
            }

            return PagedQuery(builder).OrderByDescending(h => h.Date).ToList();
        }

        public List<EpisodeHistory> GetByEpisode(int episodeId, EpisodeHistoryEventType? eventType)
        {
            var builder = Builder()
                .Join<EpisodeHistory, Series>((h, s) => h.ParentId == s.Id)
                .Join<EpisodeHistory, Episode>((h, e) => h.ItemId == e.Id)
                .Where<EpisodeHistory>(h => h.ItemId == episodeId);

            if (eventType.HasValue)
            {
                builder.Where<EpisodeHistory>(h => h.EventType == eventType);
            }

            return PagedQuery(builder).OrderByDescending(h => h.Date).ToList();
        }

        public List<EpisodeHistory> FindDownloadHistory(int seriesId, QualityModel quality)
        {
            return Query(h =>
                h.ParentId == seriesId &&
                h.Quality == quality &&
                (h.EventType == EpisodeHistoryEventType.Grabbed ||
                 h.EventType == EpisodeHistoryEventType.DownloadFailed ||
                 h.EventType == EpisodeHistoryEventType.DownloadFolderImported));
        }

        public void DeleteForSeries(List<int> seriesIds)
        {
            Delete(h => seriesIds.Contains(h.ParentId));
        }

        public List<EpisodeHistory> Since(DateTime date, EpisodeHistoryEventType? eventType)
        {
            var builder = Builder()
                .Join<EpisodeHistory, Series>((h, s) => h.ParentId == s.Id)
                .Join<EpisodeHistory, Episode>((h, e) => h.ItemId == e.Id)
                .Where<EpisodeHistory>(h => h.Date >= date);

            if (eventType.HasValue)
            {
                builder.Where<EpisodeHistory>(h => h.EventType == eventType);
            }

            return PagedQuery(builder).OrderBy(h => h.Date).ToList();
        }

        public PagingSpec<EpisodeHistory> GetPaged(PagingSpec<EpisodeHistory> pagingSpec, int[] languages, int[] qualities)
        {
            pagingSpec.Records = GetPagedRecords(PagedBuilder(languages, qualities), pagingSpec, PagedQuery);

            var countTemplate = $"SELECT COUNT(*) FROM (SELECT /**select**/ FROM \"{TableMapping.Mapper.TableNameMapping(typeof(EpisodeHistory))}\" /**join**/ /**innerjoin**/ /**leftjoin**/ /**where**/ /**groupby**/ /**having**/) AS \"Inner\"";
            pagingSpec.TotalRecords = GetPagedRecordCount(PagedBuilder(languages, qualities).Select(typeof(EpisodeHistory)), pagingSpec, countTemplate);

            return pagingSpec;
        }

        protected override IEnumerable<EpisodeHistory> PagedQuery(SqlBuilder builder) =>
            _database.QueryJoined<EpisodeHistory, Series, Episode>(builder, (history, series, episode) =>
            {
                history.Series = series;
                history.Episode = episode;
                return history;
            });

        private SqlBuilder PagedBuilder(int[] languages, int[] qualities)
        {
            var builder = Builder()
                .Join<EpisodeHistory, Series>((h, s) => h.ParentId == s.Id)
                .Join<EpisodeHistory, Episode>((h, e) => h.ItemId == e.Id);

            if (languages is { Length: > 0 })
            {
                builder.Where($"({ActivityFilterSql.LanguageClause(_database.DatabaseType, TableMapping.Mapper.TableNameMapping(typeof(EpisodeHistory)), languages)})");
            }

            if (qualities is { Length: > 0 })
            {
                builder.Where($"({ActivityFilterSql.QualityClause(TableMapping.Mapper.TableNameMapping(typeof(EpisodeHistory)), qualities)})");
            }

            return builder;
        }
    }
}
