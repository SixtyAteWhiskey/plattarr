using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Notifications
{
    public class ManualInteractionRequiredMessage
    {
        public string Message { get; set; }

        // Null when the download is not matched to a library entity yet.
        public ActivitySubject Subject { get; set; }
        public IMediaNotificationData Data { get; set; }
        public TrackedDownload TrackedDownload { get; set; }
        public string QualityName { get; set; }
        public string ReleaseGroup { get; set; }
        public DownloadClientItemClientInfo DownloadClientInfo { get; set; }
        public string DownloadId { get; set; }
        public NotificationRelease Release { get; set; }

        public override string ToString()
        {
            return Message;
        }
    }
}
