using NzbDrone.Core.Notifications.Webhook;

namespace NzbDrone.Core.Music.Notifications
{
    public class WebhookRetagPayload : WebhookPayload
    {
        public WebhookArtist Artist { get; set; }
        public WebhookTrackFile TrackFile { get; set; }
    }
}
