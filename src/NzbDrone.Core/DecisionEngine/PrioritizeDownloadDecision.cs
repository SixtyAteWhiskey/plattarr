using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.DecisionEngine
{
    public interface IPrioritizeDownloadDecision
    {
        List<DownloadDecision> PrioritizeDecisions(List<DownloadDecision> decisions);
    }

    // Splits the decisions by media type and lets each module order its own. A decision whose
    // media type has no prioritizer keeps its place at the end, like a release with no match.
    public class PrioritizeDownloadDecision : IPrioritizeDownloadDecision
    {
        private readonly IEnumerable<IDownloadDecisionPrioritizer> _prioritizers;

        public PrioritizeDownloadDecision(IEnumerable<IDownloadDecisionPrioritizer> prioritizers)
        {
            _prioritizers = prioritizers;
        }

        public List<DownloadDecision> PrioritizeDecisions(List<DownloadDecision> decisions)
        {
            var result = new List<DownloadDecision>();
            var unhandled = new List<DownloadDecision>();

            foreach (var group in decisions.GroupBy(d => d.RemoteRelease.Kind))
            {
                var prioritizer = _prioritizers.FirstOrDefault(p => p.Kind == group.Key);

                if (prioritizer == null)
                {
                    unhandled.AddRange(group);
                    continue;
                }

                result.AddRange(prioritizer.Prioritize(group.ToList()));
            }

            result.AddRange(unhandled);

            return result;
        }
    }
}
