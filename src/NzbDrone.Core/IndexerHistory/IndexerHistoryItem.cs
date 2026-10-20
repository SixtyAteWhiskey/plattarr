using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.IndexerHistory
{
    public class IndexerHistoryItem : ModelBase
    {
        public IndexerHistoryItem()
        {
            Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public int IndexerId { get; set; }
        public DateTime Date { get; set; }
        public bool Successful { get; set; }
        public IndexerHistoryEventType EventType { get; set; }
        public Dictionary<string, string> Data { get; set; }

        public string DownloadId { get; set; }
    }

    public enum IndexerHistoryEventType
    {
        Unknown = 0,
        ReleaseGrabbed = 1,
        IndexerQuery = 2,
        IndexerRss = 3,
        IndexerAuth = 4,
        IndexerInfo = 5
    }
}
