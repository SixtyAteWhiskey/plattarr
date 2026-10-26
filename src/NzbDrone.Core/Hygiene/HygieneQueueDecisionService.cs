using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Hygiene
{
    public interface IHygieneQueueDecisionService
    {
        List<HygieneQueueAssessment> Evaluate(IEnumerable<NzbDrone.Core.Queue.Queue> queue);
    }

    // The preview and future action executor consume these same decisions.
    public class HygieneQueueDecisionService : IHygieneQueueDecisionService
    {
        private readonly IHygieneQueueEvaluator _evaluator;
        private readonly IHygieneQueueStateRepository _states;
        private readonly IHygieneRuleService _rules;

        public HygieneQueueDecisionService(IHygieneQueueEvaluator evaluator, IHygieneQueueStateRepository states, IHygieneRuleService rules)
        {
            _evaluator = evaluator;
            _states = states;
            _rules = rules;
        }

        public List<HygieneQueueAssessment> Evaluate(IEnumerable<NzbDrone.Core.Queue.Queue> queue)
        {
            var items = queue.ToList();
            var assessments = _evaluator.Evaluate(items);
            var states = _states.All().ToDictionary(x => (x.DownloadClientId, x.DownloadId));
            var rules = _rules.All();

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var assessment = assessments[i];
                var rule = _rules.Match(rules, assessment.ReasonCode, (int?)assessment.Kind ?? 0, item.DownloadClientId);
                states.TryGetValue((item.DownloadClientId, item.DownloadId), out var state);

                assessment.Strikes = state?.ReasonCode == assessment.ReasonCode ? state.Strikes : 0;
                assessment.StrikeLimit = rule?.StrikeLimit;
                assessment.RuleEnabled = rule?.Enabled == true;
                assessment.Eligible = assessment.RuleEnabled && assessment.Strikes >= rule.StrikeLimit;
                assessment.RuleExplanation = rule == null ? "No monitoring rule is configured for this reason and scope."
                    : !rule.Enabled ? "The matching monitoring rule is disabled."
                    : assessment.Eligible ? "The strike threshold is reached; automatic cleanup is not enabled."
                    : $"{assessment.Strikes} of {rule.StrikeLimit} strikes recorded.";
            }

            return assessments;
        }
    }
}
