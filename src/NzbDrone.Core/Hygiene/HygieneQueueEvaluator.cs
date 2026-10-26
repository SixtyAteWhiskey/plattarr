using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Queue;

namespace NzbDrone.Core.Hygiene
{
    public class HygieneQueueAssessment
    {
        public int QueueId { get; set; }
        public string Title { get; set; }
        public MediaKind? Kind { get; set; }
        public string DownloadClient { get; set; }
        public string ReasonCode { get; set; }
        public string Explanation { get; set; }
        public string RecommendedAction { get; set; }
        public bool NeedsAttention { get; set; }
        public bool WouldAct { get; set; }
    }

    public interface IHygieneQueueEvaluator
    {
        List<HygieneQueueAssessment> Evaluate(IEnumerable<NzbDrone.Core.Queue.Queue> queue);
    }

    // A snapshot evaluator. Automatic actions must be configured and implemented separately;
    // preview never changes a download, its category, or a media file.
    public class HygieneQueueEvaluator : IHygieneQueueEvaluator
    {
        public List<HygieneQueueAssessment> Evaluate(IEnumerable<NzbDrone.Core.Queue.Queue> queue)
        {
            return queue.Select(Evaluate).ToList();
        }

        private static HygieneQueueAssessment Evaluate(NzbDrone.Core.Queue.Queue item)
        {
            var assessment = new HygieneQueueAssessment
            {
                QueueId = item.Id,
                Title = item.Title,
                Kind = item.RemoteRelease?.Kind,
                DownloadClient = item.DownloadClient,
                WouldAct = false
            };

            if (item.TrackedDownloadState == TrackedDownloadState.Ignored)
            {
                Set(assessment, "ignored", "This download is ignored by Plattarr.", "Review it in the download client if needed.", false);
            }
            else if (item.Status == QueueStatus.Failed || item.TrackedDownloadStatus == TrackedDownloadStatus.Error)
            {
                Set(assessment, "client-failed", "The download client reported a failed download.", "Inspect the client error, then remove and blocklist the release if it cannot recover.", true);
            }
            else if (item.TrackedDownloadState == TrackedDownloadState.ImportBlocked)
            {
                Set(assessment, "import-blocked", "The completed download could not be imported automatically.", "Review the queue import messages and use Manual Import if the files are valid.", true);
            }
            else if (item.Status == QueueStatus.DownloadClientUnavailable)
            {
                Set(assessment, "client-unavailable", "The download client is unavailable.", "Check the client's connection and category settings.", true);
            }
            else if (item.Status == QueueStatus.Warning || item.TrackedDownloadStatus == TrackedDownloadStatus.Warning)
            {
                Set(assessment, "download-warning", "Plattarr or the download client reported a warning.", "Review the queue details and download client before taking action.", true);
            }
            else if (item.Status == QueueStatus.Delay)
            {
                Set(assessment, "pending", "This release is waiting in the download queue.", "No action is needed unless it remains pending unexpectedly.", false);
            }
            else
            {
                Set(assessment, "healthy", "No queue failure is currently reported.", "No action is needed.", false);
            }

            return assessment;
        }

        private static void Set(HygieneQueueAssessment assessment, string code, string explanation, string action, bool needsAttention)
        {
            assessment.ReasonCode = code;
            assessment.Explanation = explanation;
            assessment.RecommendedAction = action;
            assessment.NeedsAttention = needsAttention;
        }
    }
}
