using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Notifications
{
    // A download the client reported as failed (Lidarr's "On Download Failure").
    public class DownloadFailureMessage
    {
        public string Message { get; set; }
        public ActivitySubject Subject { get; set; }
        public IMediaNotificationData Data { get; set; }
        public string SourceTitle { get; set; }
        public string QualityName { get; set; }
        public int QualityVersion { get; set; }
        public string DownloadClient { get; set; }
        public string DownloadId { get; set; }

        public override string ToString()
        {
            return Message;
        }
    }
}
