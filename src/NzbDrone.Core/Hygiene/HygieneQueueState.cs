using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Hygiene
{
    public class HygieneQueueState : ModelBase
    {
        public int DownloadClientId { get; set; }
        public string DownloadId { get; set; }
        public int MediaKind { get; set; }
        public string ReasonCode { get; set; }
        public int Strikes { get; set; }
        public long RemainingSize { get; set; }
        public DateTime LastSeenAt { get; set; }
        public DateTime? LastStrikeAt { get; set; }
    }
}
