using NzbDrone.Core.Notifications.Webhook;

namespace NzbDrone.Core.Movies.Notifications
{
    public class WebhookGrabPayload : WebhookPayload
    {
        public WebhookMovie Movie { get; set; }
        public WebhookRemoteMovie RemoteMovie { get; set; }
        public WebhookRelease Release { get; set; }
        public string DownloadClient { get; set; }
        public string DownloadClientType { get; set; }
        public string DownloadId { get; set; }
        public WebhookCustomFormatInfo CustomFormatInfo { get; set; }
    }
}
