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
            var items = queue.Where(x => x.DownloadClientId > 0 && !string.IsNullOrWhiteSpace(x.DownloadId)).ToList();
            var assessments = _evaluator.Evaluate(items);
            var states = _states.All().ToDictionary(x => (x.DownloadClientId, x.DownloadId));
            var rules = _rules.All();
            var now = DateTime.UtcNow;

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var assessment = assessments[i];
                HygieneQueueSignalClassifier.Apply(item, assessment, rules, _rules);
                var key = (item.DownloadClientId, item.DownloadId);
                states.TryGetValue(key, out var state);
                var rule = _rules.Match(rules, assessment.ReasonCode, (int?)assessment.Kind ?? 0, item.DownloadClientId);
                var interval = rule?.StrikeIntervalMinutes ?? 5;
                var isNew = state == null;
                var previousStrikes = state?.Strikes;
                var previousReason = state?.ReasonCode;
                var previousSize = state?.RemainingSize;
                var previousKind = state?.MediaKind;
                var previousSeenAt = state?.LastSeenAt;
                state = HygieneStrikeCalculator.Advance(state, assessment.ReasonCode, assessment.NeedsAttention && rule?.Enabled == true, (long)item.SizeLeft, now, interval);
                state.DownloadClientId = item.DownloadClientId;
                state.DownloadId = item.DownloadId;
                state.MediaKind = (int?)assessment.Kind ?? 0;

                if (isNew)
                {
                    _states.Insert(state);
                    states[key] = state;
                }
                else if (state.Strikes != previousStrikes || state.ReasonCode != previousReason ||
                         state.RemainingSize != previousSize || state.MediaKind != previousKind ||
                         now - previousSeenAt >= TimeSpan.FromMinutes(1))
                {
                    _states.Update(state);
                }
            }

            foreach (var state in states.Values.Where(x => x.LastSeenAt < now.AddDays(-30)))
            {
                _states.Delete(state);
            }
        }
    }
}
