using NzbDrone.Core.Notifications.Webhook;

namespace NzbDrone.Core.Music.Notifications
{
    public class WebhookArtistAddPayload : WebhookPayload
    {
        public WebhookArtist Artist { get; set; }
    }
}
