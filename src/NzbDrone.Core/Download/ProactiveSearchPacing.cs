using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Queue;

namespace NzbDrone.Core.Download
{
    public static class ProactiveSearchPacing
    {
        public const int DefaultIntervalMinutes = 60;
        public const int DefaultActiveDownloadLimit = 5;
        public const int DefaultRecentGrabHours = 24;

        public static bool Valid(int intervalMinutes, int activeDownloadLimit, int recentGrabHours)
        {
            return intervalMinutes >= 5 && intervalMinutes <= 1440 &&
                activeDownloadLimit >= 1 && activeDownloadLimit <= 100 &&
                recentGrabHours >= 1 && recentGrabHours <= 168;
        }

        public static int ActiveDownloadCount(IEnumerable<Queue.Queue> queue)
        {
            return queue.Where(item => item.Status != QueueStatus.Completed && item.Status != QueueStatus.Failed)
                .Select(item => string.IsNullOrWhiteSpace(item.DownloadId) ? $"queue:{item.Id}" : item.DownloadId)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count();
        }

        public static string PauseReason(bool enabled, int activeDownloads, int limit)
        {
            return !enabled ? "disabled" : activeDownloads >= limit ? "active-download-limit" : null;
        }
    }
}
