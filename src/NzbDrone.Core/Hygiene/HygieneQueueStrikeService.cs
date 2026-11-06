using System;
using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Hygiene
{
    public interface IHygieneQueueStrikeService
    {
        void Observe(IEnumerable<NzbDrone.Core.Queue.Queue> queue);
    }

    public class HygieneQueueStrikeService : IHygieneQueueStrikeService
    {
        private readonly IHygieneQueueStateRepository _states;
        private readonly IHygieneRuleService _rules;
        private readonly IHygieneQueueEvaluator _evaluator;

        public HygieneQueueStrikeService(IHygieneQueueStateRepository states, IHygieneRuleService rules, IHygieneQueueEvaluator evaluator)
        {
            _states = states;
            _rules = rules;
            _evaluator = evaluator;
        }

        public void Observe(IEnumerable<NzbDrone.Core.Queue.Queue> queue)
        {
            var items = queue.Where(x => x.DownloadClientId > 0 && !string.IsNullOrWhiteSpace(x.DownloadId) &&
                string.IsNullOrWhiteSpace(x.HygieneImportRescueStatus)).ToList();
            var assessments = _evaluator.Evaluate(items);
            var states = _states.FindByIdentities(items.Select(x => (x.DownloadClientId, x.DownloadId)))
                .ToDictionary(x => (x.DownloadClientId, x.DownloadId));
            var rules = _rules.All();
            var now = DateTime.UtcNow;

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var assessment = assessments[i];
                HygieneQueueSignalClassifier.Apply(item, assessment, rules, _rules);
                var key = (item.DownloadClientId, item.DownloadId);
                states.TryGetValue(key, out var state);
                var rule = HygieneRuleSafeguards.MatchForItem(rules, _rules, assessment.ReasonCode, (int?)assessment.Kind ?? 0, item);
                if (HygieneRuleSafeguards.HoldReason(item, rule, assessment.ReasonCode) != null)
                {
                    assessment.ReasonCode = "ignored";
                    assessment.NeedsAttention = false;
                }

                var unstableRule = HygieneRuleSafeguards.MatchForItem(rules, _rules, "download-unstable", (int?)assessment.Kind ?? 0, item);
                if (HygieneRuleSafeguards.HoldReason(item, unstableRule, "download-unstable") != null)
                {
                    unstableRule = null;
                }

                var interval = rule?.StrikeIntervalMinutes ?? 5;
                var isNew = state == null;
                var previousStrikes = state?.Strikes;
                var previousReason = state?.ReasonCode;
                var previousRuleId = state?.ConsecutiveRuleId;
                var previousSize = state?.RemainingSize;
                var previousProgressBaseline = state?.ProgressBaselineRemainingSize;
                var previousKind = state?.MediaKind;
                var previousSeenAt = state?.LastSeenAt;
                var previousUnstableStrikes = state?.UnstableStrikes;
                var previousUnstableRuleId = state?.UnstableRuleId;
                var previousUnstableStrikeAt = state?.LastUnstableStrikeAt;
                state = HygieneStrikeCalculator.Advance(
                    state,
                    assessment.ReasonCode,
                    assessment.NeedsAttention && rule?.Enabled == true,
                    (long)item.SizeLeft,
                    now,
                    interval,
                    rule?.ReasonCode == "download-slow" ? rule.ResetStrikesOnProgress : rule?.ResetStrikesOnProgress != false,
                    rule?.MinimumProgressToResetBytes ?? 0,
                    rule?.Enabled == true ? rule.Id : 0);
                HygieneUnstableStrikeCalculator.Advance(state, assessment.ReasonCode, unstableRule?.Enabled == true ? unstableRule.Id : 0, now, unstableRule?.StrikeIntervalMinutes ?? 5);
                state.DownloadClientId = item.DownloadClientId;
                state.DownloadId = item.DownloadId;
                state.MediaKind = (int?)assessment.Kind ?? 0;

                if (isNew)
                {
                    _states.Insert(state);
                    states[key] = state;
                }
                else if (state.Strikes != previousStrikes || state.UnstableStrikes != previousUnstableStrikes ||
                         state.UnstableRuleId != previousUnstableRuleId ||
                         state.LastUnstableStrikeAt != previousUnstableStrikeAt || state.ReasonCode != previousReason ||
                         state.ConsecutiveRuleId != previousRuleId ||
                         state.RemainingSize != previousSize || state.ProgressBaselineRemainingSize != previousProgressBaseline || state.MediaKind != previousKind ||
                         now - previousSeenAt >= TimeSpan.FromMinutes(1))
                {
                    _states.Update(state);
                }
            }

            _states.DeleteExpired(now.AddDays(-30));
        }
    }
}
