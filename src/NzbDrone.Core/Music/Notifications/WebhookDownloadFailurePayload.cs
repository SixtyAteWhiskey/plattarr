using NzbDrone.Core.Notifications.Webhook;

namespace NzbDrone.Core.Music.Notifications
{
    public class WebhookDownloadFailurePayload : WebhookPayload
    {
        public string Quality { get; set; }
        public int QualityVersion { get; set; }
        public string ReleaseTitle { get; set; }
        public string DownloadClient { get; set; }
        public string DownloadId { get; set; }
    }
}
