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
            var states = _states.FindByIdentities(items.Select(x => (x.DownloadClientId, x.DownloadId)))
                .ToDictionary(x => (x.DownloadClientId, x.DownloadId));
            var rules = _rules.All();

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var assessment = assessments[i];
                HygieneQueueSignalClassifier.Apply(item, assessment, rules, _rules);
                var rule = _rules.Match(rules, assessment.ReasonCode, (int?)assessment.Kind ?? 0, item.DownloadClientId);
                states.TryGetValue((item.DownloadClientId, item.DownloadId), out var state);
                var isSlowOrStalled = assessment.ReasonCode is "download-stalled" or "download-slow";
                var unstableRule = item.DownloadClientId > 0 && !string.IsNullOrWhiteSpace(item.DownloadId) &&
                                   (isSlowOrStalled || state?.UnstableStrikes > 0)
                    ? _rules.Match(rules, "download-unstable", (int?)assessment.Kind ?? 0, item.DownloadClientId)
                    : null;
                var unstableStrikes = unstableRule != null && state?.UnstableRuleId == unstableRule.Id
                    ? state.UnstableStrikes
                    : 0;

                if (unstableRule?.Enabled == true)
                {
                    assessment.UnstableStrikes = unstableStrikes;
                    assessment.UnstableStrikeLimit = unstableRule.StrikeLimit;
                    assessment.UnstableRuleId = unstableRule.Id;
                }

                // A specific stalled/slow rule wins when it reaches its own limit.
                // Otherwise the cumulative rule can authorize the same download.
                if (isSlowOrStalled &&
                    !(rule?.Enabled == true && state?.ReasonCode == assessment.ReasonCode && state.Strikes >= rule.StrikeLimit))
                {
                    if (unstableRule?.Enabled == true && unstableStrikes >= unstableRule.StrikeLimit)
                    {
                        assessment.ReasonCode = "download-unstable";
                        assessment.Explanation += $" Slow or stalled signals have accumulated {unstableStrikes} strikes for this download.";
                        assessment.RecommendedAction = "Review this repeatedly slow or stalled download and its replacement options.";
                        rule = unstableRule;
                    }
                }

                assessment.Strikes = assessment.ReasonCode == "download-unstable" ? unstableStrikes
                    : state?.ReasonCode == assessment.ReasonCode ? state.Strikes : 0;
                assessment.StrikeLimit = rule?.StrikeLimit;
                assessment.RuleId = rule?.Id;
                assessment.RuleEnabled = rule?.Enabled == true;
                assessment.DeleteFilesOnFailure = rule?.DeleteFilesOnFailure == true;
                assessment.SearchForReplacement = rule?.SearchForReplacement == true;
                assessment.Eligible = assessment.RuleEnabled && assessment.Strikes >= rule.StrikeLimit;
                assessment.RuleExplanation = rule == null ? "No monitoring rule is configured for this reason and scope."
                    : !rule.Enabled ? "The matching monitoring rule is disabled."
                    : assessment.Eligible ? "The strike threshold is reached."
                    : $"{assessment.Strikes} of {rule.StrikeLimit} strikes recorded.";

                assessment.WouldAct = false;
                assessment.ActionExplanation = !assessment.Eligible ? "The rule threshold has not been reached."
                    : !_config.HygieneActionsEnabled ? "Automatic actions are disabled by the global Hygiene gate."
                    : assessment.ReasonCode is not ("client-failed" or "import-blocked" or "download-stalled" or "download-slow" or "download-unstable")
                        ? "This reason is monitor-only and requires manual review."
                    : (assessment.ReasonCode is "download-stalled" or "download-slow" or "download-unstable") && item.Protocol != DownloadProtocol.Torrent
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
            var fileAction = assessment.DeleteFilesOnFailure
                ? "delete its source files"
                : "keep its source files";
            var searchAction = assessment.SearchForReplacement
                ? "request a replacement search if failed-download settings allow it"
                : "skip the replacement search";
            assessment.ActionExplanation = $"The scoped rule is eligible; the executor will remove the client job, {fileAction}, blocklist the release, and {searchAction}.";
            return assessment.ActionExplanation;
        }
    }
}
