using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Music.History
{
    public interface IAlbumHistoryRepository : IBasicRepository<AlbumHistory>
    {
        AlbumHistory MostRecentForAlbum(int albumId);
        List<AlbumHistory> FindByAlbumId(int albumId);
        AlbumHistory MostRecentForDownloadId(string downloadId);
        List<AlbumHistory> FindByDownloadId(string downloadId);
        List<AlbumHistory> GetByArtist(int artistId, AlbumHistoryEventType? eventType);
        List<AlbumHistory> GetByAlbum(int albumId, AlbumHistoryEventType? eventType);
        List<AlbumHistory> FindDownloadHistory(int artistId, AudioQualityModel quality);
        void DeleteForArtists(List<int> artistIds);
        List<AlbumHistory> Since(DateTime date, AlbumHistoryEventType? eventType);
        PagingSpec<AlbumHistory> GetPaged(PagingSpec<AlbumHistory> pagingSpec, int[] qualities);
    }

    public class AlbumHistoryRepository : MediaKindRepository<AlbumHistory>, IAlbumHistoryRepository
    {
        public AlbumHistoryRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator, MediaKind.Music)
        {
        }

        public AlbumHistory MostRecentForAlbum(int albumId)
        {
            return Query(h => h.ItemId == albumId).MaxBy(h => h.Date);
        }

        public List<AlbumHistory> FindByAlbumId(int albumId)
        {
            return Query(h => h.ItemId == albumId)
                .OrderByDescending(h => h.Date)
                .ToList();
        }

        public AlbumHistory MostRecentForDownloadId(string downloadId)
        {
            return Query(h => h.DownloadId == downloadId).MaxBy(h => h.Date);
        }

        public List<AlbumHistory> FindByDownloadId(string downloadId)
        {
            return Query(h => h.DownloadId == downloadId);
        }

        public List<AlbumHistory> GetByArtist(int artistId, AlbumHistoryEventType? eventType)
        {
            var builder = Builder().Join<AlbumHistory, Artist>((h, a) => h.ParentId == a.Id)
                                   .Join<AlbumHistory, Album>((h, a) => h.ItemId == a.Id)
                                   .Where<AlbumHistory>(h => h.ParentId == artistId);

            if (eventType.HasValue)
            {
                builder.Where<AlbumHistory>(h => h.EventType == eventType);
            }

            return PagedQuery(builder).OrderByDescending(h => h.Date).ToList();
        }

        public List<AlbumHistory> GetByAlbum(int albumId, AlbumHistoryEventType? eventType)
        {
            var builder = Builder()
                .Join<AlbumHistory, Artist>((h, a) => h.ParentId == a.Id)
                .Join<AlbumHistory, Album>((h, a) => h.ItemId == a.Id)
                .Where<AlbumHistory>(h => h.ItemId == albumId);

            if (eventType.HasValue)
            {
                builder.Where<AlbumHistory>(h => h.EventType == eventType);
            }

            return PagedQuery(builder).OrderByDescending(h => h.Date).ToList();
        }

        public List<AlbumHistory> FindDownloadHistory(int artistId, AudioQualityModel quality)
        {
            return Query(h =>
                h.ParentId == artistId &&
                h.Quality == quality &&
                (h.EventType == AlbumHistoryEventType.Grabbed ||
                 h.EventType == AlbumHistoryEventType.DownloadFailed ||
                 h.EventType == AlbumHistoryEventType.DownloadImported));
        }

        public void DeleteForArtists(List<int> artistIds)
        {
            Delete(h => artistIds.Contains(h.ParentId));
        }

        public List<AlbumHistory> Since(DateTime date, AlbumHistoryEventType? eventType)
        {
            var builder = Builder()
                .Join<AlbumHistory, Artist>((h, a) => h.ParentId == a.Id)
                .Join<AlbumHistory, Album>((h, a) => h.ItemId == a.Id)
                .Where<AlbumHistory>(h => h.Date >= date);

            if (eventType.HasValue)
            {
                builder.Where<AlbumHistory>(h => h.EventType == eventType);
            }

            return PagedQuery(builder).OrderBy(h => h.Date).ToList();
        }

        public PagingSpec<AlbumHistory> GetPaged(PagingSpec<AlbumHistory> pagingSpec, int[] qualities)
        {
            pagingSpec.Records = GetPagedRecords(PagedBuilder(qualities), pagingSpec, PagedQuery);

            var countTemplate = $"SELECT COUNT(*) FROM (SELECT /**select**/ FROM \"{TableMapping.Mapper.TableNameMapping(typeof(AlbumHistory))}\" /**join**/ /**innerjoin**/ /**leftjoin**/ /**where**/ /**groupby**/ /**having**/) AS \"Inner\"";
            pagingSpec.TotalRecords = GetPagedRecordCount(PagedBuilder(qualities).Select(typeof(AlbumHistory)), pagingSpec, countTemplate);

            return pagingSpec;
        }

        // The quality filter reads the stored audio quality (QualityModel's JSON has the same "quality": id
        // shape for both catalogs), and the joins give the sort keys artists.* and albums.*.
        private SqlBuilder PagedBuilder(int[] qualities)
        {
            var builder = Builder()
                .Join<AlbumHistory, Artist>((h, a) => h.ParentId == a.Id)
                .Join<AlbumHistory, Album>((h, a) => h.ItemId == a.Id);

            if (qualities is { Length: > 0 })
            {
                builder.Where($"({ActivityFilterSql.QualityClause(TableMapping.Mapper.TableNameMapping(typeof(AlbumHistory)), qualities)})");
            }

            return builder;
        }

        protected override IEnumerable<AlbumHistory> PagedQuery(SqlBuilder builder) =>
            _database.QueryJoined<AlbumHistory, Artist, Album>(builder, (history, artist, album) =>
            {
                history.Artist = artist;
                history.Album = album;
                return history;
            });
    }
}
