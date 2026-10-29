using System.Collections.Generic;
using System.Linq;
using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Hygiene
{
    public interface IHygieneQueueStateRepository : IBasicRepository<HygieneQueueState>
    {
        HygieneQueueState Find(int downloadClientId, string downloadId);
        IEnumerable<HygieneQueueState> FindByIdentities(IEnumerable<(int DownloadClientId, string DownloadId)> identities);
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

        public IEnumerable<HygieneQueueState> FindByIdentities(IEnumerable<(int DownloadClientId, string DownloadId)> identities)
        {
            var states = new List<HygieneQueueState>();
            var distinct = identities.Distinct().ToList();

            // Keep the parameter count below older SQLite limits as queue size grows.
            foreach (var batch in distinct.Chunk(200))
            {
                var parameters = new DynamicParameters();
                var clauses = new List<string>();

                for (var i = 0; i < batch.Length; i++)
                {
                    var (downloadClientId, downloadId) = batch[i];
                    parameters.Add($"client{i}", downloadClientId);

                    if (downloadId == null)
                    {
                        clauses.Add($"(\"DownloadClientId\" = @client{i} AND \"DownloadId\" IS NULL)");
                    }
                    else
                    {
                        parameters.Add($"download{i}", downloadId);
                        clauses.Add($"(\"DownloadClientId\" = @client{i} AND \"DownloadId\" = @download{i})");
                    }
                }

                states.AddRange(Query(Builder().Where("(" + string.Join(" OR ", clauses) + ")", parameters)));
            }

            return states;
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
        HygieneAction FindLatest(int downloadClientId, string downloadId, string reasonCode);
        List<HygieneAction> FindIncomplete();
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

        public HygieneAction FindLatest(int downloadClientId, string downloadId, string reasonCode)
        {
            return Query(x => x.DownloadClientId == downloadClientId && x.DownloadId == downloadId && x.ReasonCode == reasonCode)
                .OrderByDescending(x => x.Id)
                .FirstOrDefault();
        }

        public List<HygieneAction> FindIncomplete()
        {
            return Query(x => x.Status != "completed");
        }
    }
}
