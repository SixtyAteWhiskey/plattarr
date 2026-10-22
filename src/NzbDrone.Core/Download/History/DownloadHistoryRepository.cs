using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Download.History
{
    public interface IDownloadHistoryRepository : IBasicRepository<DownloadHistory>
    {
        List<DownloadHistory> FindByDownloadId(string downloadId);
        void DeleteByParentIds(MediaKind kind, List<int> parentIds);
    }

    // Download history is a core table: one row per download event for every media type. A
    // download id belongs to one download, whatever its media type, so lookups by download id
    // see all rows. Ids of movies, series and artists overlap, so a delete names its kind.
    public class DownloadHistoryRepository : BasicRepository<DownloadHistory>, IDownloadHistoryRepository
    {
        public DownloadHistoryRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<DownloadHistory> FindByDownloadId(string downloadId)
        {
            return Query(x => x.DownloadId == downloadId).OrderByDescending(h => h.Date).ToList();
        }

        public void DeleteByParentIds(MediaKind kind, List<int> parentIds)
        {
            Delete(r => r.MediaKind == kind && parentIds.Contains(r.ParentId));
        }

        protected override void BeforeInsert(DownloadHistory model)
        {
            if (!Enum.IsDefined(model.MediaKind))
            {
                throw new InvalidOperationException($"Can't insert a DownloadHistory with MediaKind '{model.MediaKind}'");
            }
        }
    }
}
