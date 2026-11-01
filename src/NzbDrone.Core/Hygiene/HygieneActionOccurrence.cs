using NzbDrone.Core.Download.History;

namespace NzbDrone.Core.Hygiene
{
    public static class HygieneActionOccurrence
    {
        // A torrent hash can be grabbed again after a completed cleanup. The
        // completed journal entry applies to the earlier grab, not the new one.
        public static bool WasGrabbedAgain(HygieneAction action, DownloadHistory latestGrab)
        {
            return action != null && latestGrab != null &&
                   latestGrab.EventType == DownloadHistoryEventType.DownloadGrabbed &&
                   latestGrab.DownloadClientId == action.DownloadClientId &&
                   latestGrab.DownloadId == action.DownloadId &&
                   latestGrab.Date > action.CreatedAt;
        }
    }
}
