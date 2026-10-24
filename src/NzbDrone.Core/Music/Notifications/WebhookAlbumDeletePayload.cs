using NzbDrone.Core.Notifications.Webhook;

namespace NzbDrone.Core.Music.Notifications
{
    public class WebhookAlbumDeletePayload : WebhookPayload
    {
        public WebhookArtist Artist { get; set; }
        public WebhookAlbum Album { get; set; }
        public bool DeletedFiles { get; set; }
    }
}
