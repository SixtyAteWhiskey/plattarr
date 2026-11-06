using System;

namespace NzbDrone.Core.Hygiene
{
    public static class HygieneStrikeCalculator
    {
        public static HygieneQueueState Advance(HygieneQueueState state, string reasonCode, bool needsAttention, long remainingSize, DateTime now, int intervalMinutes, bool resetOnProgress = true, long minimumProgressToResetBytes = 0, int ruleId = 0)
        {
            var previousReason = state?.ReasonCode;
            var previousSize = state?.RemainingSize;
            var ruleChanged = state?.ConsecutiveRuleId.HasValue == true && state.ConsecutiveRuleId != ruleId;
            state ??= new HygieneQueueState();

            var strikeable = needsAttention && reasonCode is "client-failed" or "import-blocked" or "download-warning" or "download-stalled" or "download-slow" or "download-metadata";
            var progressResetsStrikes = reasonCode is not "download-metadata" && resetOnProgress;
            if (previousReason != reasonCode || ruleChanged || remainingSize > state.ProgressBaselineRemainingSize)
            {
                state.ProgressBaselineRemainingSize = remainingSize;
            }
            else if (state.ProgressBaselineRemainingSize == null)
            {
                state.ProgressBaselineRemainingSize = previousSize ?? remainingSize;
            }

            var progressed = previousSize.HasValue && remainingSize < previousSize.Value;
            var progressSinceBaseline = state.ProgressBaselineRemainingSize.Value - remainingSize;
            var enoughProgress = progressed && progressSinceBaseline > 0 &&
                                 (minimumProgressToResetBytes == 0 || progressSinceBaseline >= minimumProgressToResetBytes);

            if (!strikeable || (enoughProgress && progressResetsStrikes))
            {
                state.Strikes = 0;
                state.LastStrikeAt = null;
                state.ProgressBaselineRemainingSize = remainingSize;
            }
            else if (previousReason != reasonCode || ruleChanged || !state.LastStrikeAt.HasValue)
            {
                state.Strikes = 1;
                state.LastStrikeAt = now;
            }
            else if (now - state.LastStrikeAt.Value >= TimeSpan.FromMinutes(intervalMinutes))
            {
                state.Strikes++;
                state.LastStrikeAt = now;
            }

            state.ReasonCode = reasonCode;
            state.ConsecutiveRuleId = ruleId;
            state.RemainingSize = remainingSize;
            state.LastSeenAt = now;
            return state;
        }
    }
}
