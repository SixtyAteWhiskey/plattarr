using System.Collections.Generic;
using NzbDrone.Core.Queue;
using QueueModel = NzbDrone.Core.Queue.Queue;

namespace NzbDrone.Core.Hygiene
{
    public static class HygieneQueueSignalClassifier
    {
        public static void Apply(
            QueueModel item,
            HygieneQueueAssessment assessment,
            IEnumerable<HygieneRule> rules,
            IHygieneRuleService ruleService)
        {
            if (assessment.ReasonCode is "ignored" or "client-failed" or "import-blocked" or "client-unavailable")
            {
                return;
            }

            if (item.DownloadClientStalled)
            {
                Set(
                    assessment,
                    "download-stalled",
                    "The download client reports this download in its stalled state.",
                    "Check seed availability and client connectivity; resume or replace the release if it remains stalled.");
                return;
            }

            if (assessment.ReasonCode != "healthy")
            {
                return;
            }

            var rule = ruleService.Match(rules, "download-slow", (int?)assessment.Kind ?? 0, item.DownloadClientId);
            var rate = item.DownloadRateBytesPerSecond;

            if (rule == null || rule.MinimumDownloadRateKiBps <= 0 ||
                item.Status != QueueStatus.Downloading || item.SizeLeft <= 0 ||
                !rate.HasValue || rate.Value < 0 || rate.Value >= rule.MinimumDownloadRateKiBps * 1024L)
            {
                return;
            }

            Set(
                assessment,
                "download-slow",
                $"The reported download rate is {rate.Value / 1024.0:F1} KiB/s, below this rule's {rule.MinimumDownloadRateKiBps} KiB/s threshold.",
                "Check seed availability, client connectivity, and the configured speed threshold.");
        }

        private static void Set(HygieneQueueAssessment assessment, string reasonCode, string explanation, string recommendedAction)
        {
            assessment.ReasonCode = reasonCode;
            assessment.Explanation = explanation;
            assessment.RecommendedAction = recommendedAction;
            assessment.NeedsAttention = true;
        }
    }
}
