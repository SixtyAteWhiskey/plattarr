using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Notifications
{
    public class GrabMessage
    {
        public string Message { get; set; }
        public ActivitySubject Subject { get; set; }
        public IMediaNotificationData Data { get; set; }
        public string QualityName { get; set; }
        public NotificationRelease Release { get; set; }
        public string DownloadClientType { get; set; }
        public string DownloadClientName { get; set; }
        public string DownloadId { get; set; }

        public override string ToString()
        {
            return Message;
        }
    }
}
