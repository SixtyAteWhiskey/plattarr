using System;
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

            if (item.DownloadClientDownloadingMetadata)
            {
                Set(
                    assessment,
                    "download-metadata",
                    "The download client is fetching torrent metadata; it may need attention if this state persists.",
                    "Check tracker and peer connectivity if metadata remains unavailable.");
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

            var slowRate = rule?.MinimumDownloadRateKiBps > 0 && rate is >= 0 &&
                           rate.Value < rule.MinimumDownloadRateKiBps * 1024L;
            var overtime = rule?.MaximumDownloadHours > 0 && item.Added.HasValue &&
                           DateTime.UtcNow - item.Added.Value >= TimeSpan.FromHours(rule.MaximumDownloadHours);

            if (rule == null || item.Status != QueueStatus.Downloading || item.SizeLeft <= 0 ||
                (!slowRate && !overtime))
            {
                return;
            }

            Set(
                assessment,
                "download-slow",
                slowRate
                    ? $"The reported download rate is {rate.Value / 1024.0:F1} KiB/s, below this rule's {rule.MinimumDownloadRateKiBps} KiB/s threshold."
                    : $"This download has exceeded this rule's {rule.MaximumDownloadHours}-hour limit.",
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
