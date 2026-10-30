using System;

namespace NzbDrone.Core.Hygiene
{
    public static class HygieneStrikeCalculator
    {
        public static HygieneQueueState Advance(HygieneQueueState state, string reasonCode, bool needsAttention, long remainingSize, DateTime now, int intervalMinutes)
        {
            var previousReason = state?.ReasonCode;
            var previousSize = state?.RemainingSize;
            state ??= new HygieneQueueState();

            var strikeable = needsAttention && reasonCode is "client-failed" or "import-blocked" or "download-warning" or "download-stalled" or "download-slow" or "download-metadata";
            var progressed = previousSize.HasValue && remainingSize < previousSize.Value;
            var progressResetsStrikes = reasonCode is not ("download-slow" or "download-metadata");

            if (!strikeable || (progressed && progressResetsStrikes))
            {
                state.Strikes = 0;
                state.LastStrikeAt = null;
            }
            else if (previousReason != reasonCode || !state.LastStrikeAt.HasValue)
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
            state.RemainingSize = remainingSize;
            state.LastSeenAt = now;
            return state;
        }
    }
}
