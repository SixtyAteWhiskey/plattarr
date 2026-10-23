using NzbDrone.Core.Notifications.Webhook;

namespace NzbDrone.Core.Tv.Notifications
{
    public class WebhookSeriesAddPayload : WebhookPayload
    {
        public WebhookSeries Series { get; set; }
    }
}
