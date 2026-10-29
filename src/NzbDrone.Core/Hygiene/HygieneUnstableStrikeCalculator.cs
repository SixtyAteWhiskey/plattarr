using System;

namespace NzbDrone.Core.Hygiene
{
    public static class HygieneUnstableStrikeCalculator
    {
        public static void Advance(HygieneQueueState state, string reasonCode, int ruleId, DateTime now, int intervalMinutes)
        {
            if (ruleId == 0)
            {
                state.UnstableStrikes = 0;
                state.UnstableRuleId = 0;
                state.LastUnstableStrikeAt = null;
                return;
            }

            if (state.UnstableRuleId != ruleId)
            {
                state.UnstableStrikes = 0;
                state.UnstableRuleId = ruleId;
                state.LastUnstableStrikeAt = null;
            }

            // Count at most once per interval across both signals. A healthy interval
            // does not erase earlier strikes for this download-client job.
            if (reasonCode is not ("download-stalled" or "download-slow"))
            {
                return;
            }

            if (!state.LastUnstableStrikeAt.HasValue ||
                now - state.LastUnstableStrikeAt.Value >= TimeSpan.FromMinutes(intervalMinutes))
            {
                state.UnstableStrikes++;
                state.LastUnstableStrikeAt = now;
            }
        }
    }
}
