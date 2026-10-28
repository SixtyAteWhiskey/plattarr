using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers;

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
        private readonly IConfigService _config;

        public HygieneQueueDecisionService(
            IHygieneQueueEvaluator evaluator,
            IHygieneQueueStateRepository states,
            IHygieneRuleService rules,
            IConfigService config)
        {
            _evaluator = evaluator;
            _states = states;
            _rules = rules;
            _config = config;
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
                HygieneQueueSignalClassifier.Apply(item, assessment, rules, _rules);
                var rule = _rules.Match(rules, assessment.ReasonCode, (int?)assessment.Kind ?? 0, item.DownloadClientId);
                states.TryGetValue((item.DownloadClientId, item.DownloadId), out var state);

                assessment.Strikes = state?.ReasonCode == assessment.ReasonCode ? state.Strikes : 0;
                assessment.StrikeLimit = rule?.StrikeLimit;
                assessment.RuleId = rule?.Id;
                assessment.RuleEnabled = rule?.Enabled == true;
                assessment.Eligible = assessment.RuleEnabled && assessment.Strikes >= rule.StrikeLimit;
                assessment.RuleExplanation = rule == null ? "No monitoring rule is configured for this reason and scope."
                    : !rule.Enabled ? "The matching monitoring rule is disabled."
                    : assessment.Eligible ? "The strike threshold is reached."
                    : $"{assessment.Strikes} of {rule.StrikeLimit} strikes recorded.";

                assessment.WouldAct = false;
                assessment.ActionExplanation = !assessment.Eligible ? "The rule threshold has not been reached."
                    : !_config.HygieneActionsEnabled ? "Automatic actions are disabled by the global Hygiene gate."
                    : assessment.ReasonCode is not ("client-failed" or "import-blocked" or "download-stalled" or "download-slow")
                        ? "This reason is monitor-only and requires manual review."
                    : (assessment.ReasonCode == "download-stalled" || assessment.ReasonCode == "download-slow") && item.Protocol != DownloadProtocol.Torrent
                        ? "Automatic stalled and slow cleanup is available only for torrent downloads."
                    : item.DownloadClientId <= 0 || string.IsNullOrWhiteSpace(item.DownloadId)
                        ? "This item is not a tracked download-client item."
                    : assessment.Kind == null
                        ? "The media kind is unknown, so automatic actions are held."
                    : IsPrivateTorrent(item)
                        ? "Automatic actions are held for private or unverified torrent releases."
                    : SetWouldAct(assessment);
            }

            return assessments;
        }

        private static bool IsPrivateTorrent(NzbDrone.Core.Queue.Queue item)
        {
            return item.Protocol == DownloadProtocol.Torrent &&
                   item.RemoteRelease?.Release?.IndexerPrivacy != IndexerPrivacy.Public;
        }

        private static string SetWouldAct(HygieneQueueAssessment assessment)
        {
            assessment.WouldAct = true;
            assessment.ActionExplanation = "The scoped rule is eligible; the executor will remove the client job without deleting its files, blocklist the release, and request the configured replacement search.";
            return assessment.ActionExplanation;
        }
    }
}
