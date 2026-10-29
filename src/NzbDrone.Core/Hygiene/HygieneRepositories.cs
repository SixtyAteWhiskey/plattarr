using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Hygiene
{
    public interface IHygieneQueueStateRepository : IBasicRepository<HygieneQueueState>
    {
        HygieneQueueState Find(int downloadClientId, string downloadId);
    }

    public class HygieneQueueStateRepository : BasicRepository<HygieneQueueState>, IHygieneQueueStateRepository
    {
        public HygieneQueueStateRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public HygieneQueueState Find(int downloadClientId, string downloadId)
        {
            return Query(x => x.DownloadClientId == downloadClientId && x.DownloadId == downloadId).SingleOrDefault();
        }
    }

    public interface IHygieneRuleRepository : IBasicRepository<HygieneRule>
    {
    }

    public class HygieneRuleRepository : BasicRepository<HygieneRule>, IHygieneRuleRepository
    {
        public HygieneRuleRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }
    }

    public interface IHygieneActionRepository : IBasicRepository<HygieneAction>
    {
        List<HygieneAction> FindByReplacementSearchCommandId(int commandId);
    }

    public class HygieneActionRepository : BasicRepository<HygieneAction>, IHygieneActionRepository
    {
        public HygieneActionRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<HygieneAction> FindByReplacementSearchCommandId(int commandId)
        {
            return Query(x => x.ReplacementSearchCommandId == commandId).ToList();
        }
    }
}
