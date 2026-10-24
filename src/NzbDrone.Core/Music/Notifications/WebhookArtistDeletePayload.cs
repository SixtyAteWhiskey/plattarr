using NzbDrone.Core.Notifications.Webhook;

namespace NzbDrone.Core.Music.Notifications
{
    public class WebhookArtistDeletePayload : WebhookPayload
    {
        public WebhookArtist Artist { get; set; }
        public bool DeletedFiles { get; set; }
    }
}
