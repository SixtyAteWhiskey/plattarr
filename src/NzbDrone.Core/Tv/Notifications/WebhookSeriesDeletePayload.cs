using NzbDrone.Core.Notifications.Webhook;

namespace NzbDrone.Core.Tv.Notifications
{
    public class WebhookSeriesDeletePayload : WebhookPayload
    {
        public WebhookSeries Series { get; set; }
        public bool DeletedFiles { get; set; }
    }
}
