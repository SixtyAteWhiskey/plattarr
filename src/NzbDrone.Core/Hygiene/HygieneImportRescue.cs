using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Hygiene
{
    public class HygieneImportRescue : ModelBase
    {
        public int DownloadClientId { get; set; }
        public string DownloadId { get; set; }
        public int MediaKind { get; set; }
        public int RuleId { get; set; }
        public string TargetIds { get; set; }
        public string CandidateFingerprint { get; set; }
        public string Status { get; set; }
        public int Attempts { get; set; }
        public int? CommandId { get; set; }
        public DateTime? RequestedAt { get; set; }
        public DateTime? NextAttemptAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public interface IHygieneImportRescueRepository : IBasicRepository<HygieneImportRescue>
    {
        HygieneImportRescue FindLatest(int clientId, string downloadId);
        List<HygieneImportRescue> FindPending();
        List<HygieneImportRescue> FindRecent(int limit, int offset);
    }

    public class HygieneImportRescueRepository : BasicRepository<HygieneImportRescue>, IHygieneImportRescueRepository
    {
        public HygieneImportRescueRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public HygieneImportRescue FindLatest(int clientId, string downloadId)
        {
            return Query(x => x.DownloadClientId == clientId && x.DownloadId == downloadId)
                .OrderByDescending(x => x.Id).FirstOrDefault();
        }

        public List<HygieneImportRescue> FindPending()
        {
            return Query(x => x.Status == "requested");
        }

        public List<HygieneImportRescue> FindRecent(int limit, int offset)
        {
            return Query(Builder().OrderBy("\"Id\" DESC LIMIT @limit OFFSET @offset", new { limit, offset }));
        }
    }
}
