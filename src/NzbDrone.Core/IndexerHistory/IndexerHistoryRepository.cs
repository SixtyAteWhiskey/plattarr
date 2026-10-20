using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.IndexerHistory
{
    public interface IIndexerHistoryRepository : IBasicRepository<IndexerHistoryItem>
    {
        IndexerHistoryItem MostRecentForDownloadId(string downloadId);
        List<IndexerHistoryItem> FindByDownloadId(string downloadId);
        List<IndexerHistoryItem> FindDownloadHistory(int indexerId);
        List<IndexerHistoryItem> GetByIndexerId(int indexerId, IndexerHistoryEventType? eventType);
        void DeleteForIndexers(List<int> indexerIds);
        IndexerHistoryItem MostRecentForIndexer(int indexerId);
        List<IndexerHistoryItem> Between(DateTime start, DateTime end);
        List<IndexerHistoryItem> Since(DateTime date, IndexerHistoryEventType? eventType);
        void Cleanup(int days);
        int CountSince(int indexerId, DateTime date, List<IndexerHistoryEventType> eventTypes);
        IndexerHistoryItem FindFirstForIndexerSince(int indexerId, DateTime date, List<IndexerHistoryEventType> eventTypes, int limit);
    }

    public class IndexerHistoryRepository : BasicRepository<IndexerHistoryItem>, IIndexerHistoryRepository
    {
        public IndexerHistoryRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public IndexerHistoryItem MostRecentForDownloadId(string downloadId)
        {
            return FindByDownloadId(downloadId).MaxBy(h => h.Date);
        }

        public List<IndexerHistoryItem> FindByDownloadId(string downloadId)
        {
            return Query(x => x.DownloadId == downloadId);
        }

        public List<IndexerHistoryItem> FindDownloadHistory(int indexerId)
        {
            var allowed = new[] { IndexerHistoryEventType.ReleaseGrabbed };

            return Query(h => h.IndexerId == indexerId &&
                         allowed.Contains(h.EventType));
        }

        public List<IndexerHistoryItem> GetByIndexerId(int indexerId, IndexerHistoryEventType? eventType)
        {
            var query = Query(x => x.IndexerId == indexerId);

            if (eventType.HasValue)
            {
                query = query.Where(h => h.EventType == eventType).ToList();
            }

            return query.OrderByDescending(h => h.Date).ToList();
        }

        public void DeleteForIndexers(List<int> indexerIds)
        {
            Delete(c => indexerIds.Contains(c.IndexerId));
        }

        public void Cleanup(int days)
        {
            var cleanDate = DateTime.Now.AddDays(-1 * days);

            Delete(c => c.Date <= cleanDate);
        }

        public IndexerHistoryItem MostRecentForIndexer(int indexerId)
        {
            return Query(x => x.IndexerId == indexerId).MaxBy(h => h.Date);
        }

        public List<IndexerHistoryItem> Between(DateTime start, DateTime end)
        {
            var builder = Builder().Where<IndexerHistoryItem>(x => x.Date >= start && x.Date <= end);

            return Query(builder).OrderBy(h => h.Date).ToList();
        }

        public List<IndexerHistoryItem> Since(DateTime date, IndexerHistoryEventType? eventType)
        {
            var builder = Builder().Where<IndexerHistoryItem>(x => x.Date >= date);

            if (eventType.HasValue)
            {
                builder.Where<IndexerHistoryItem>(h => h.EventType == eventType);
            }

            return Query(builder).OrderBy(h => h.Date).ToList();
        }

        public int CountSince(int indexerId, DateTime date, List<IndexerHistoryEventType> eventTypes)
        {
            var intEvents = eventTypes.Select(t => (int)t).ToList();

            var builder = new SqlBuilder(_database.DatabaseType)
                .SelectCount()
                .Where<IndexerHistoryItem>(x => x.IndexerId == indexerId)
                .Where<IndexerHistoryItem>(x => x.Date >= date)
                .Where<IndexerHistoryItem>(x => intEvents.Contains((int)x.EventType));

            var sql = builder.AddPageCountTemplate(typeof(IndexerHistoryItem));

            using (var conn = _database.OpenConnection())
            {
                return conn.ExecuteScalar<int>(sql.RawSql, sql.Parameters);
            }
        }

        public IndexerHistoryItem FindFirstForIndexerSince(int indexerId, DateTime date, List<IndexerHistoryEventType> eventTypes, int limit)
        {
            var intEvents = eventTypes.Select(t => (int)t).ToList();

            var builder = Builder()
                .Where<IndexerHistoryItem>(x => x.IndexerId == indexerId)
                .Where<IndexerHistoryItem>(x => x.Date >= date)
                .Where<IndexerHistoryItem>(x => intEvents.Contains((int)x.EventType));

            var query = Query(builder);

            if (limit > 0)
            {
                query = query.OrderByDescending(h => h.Date).Take(limit).ToList();
            }

            return query.MinBy(h => h.Date);
        }
    }
}
