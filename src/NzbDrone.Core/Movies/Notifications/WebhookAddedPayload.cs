using NzbDrone.Core.Notifications.Webhook;

namespace NzbDrone.Core.Movies.Notifications
{
    public class WebhookAddedPayload : WebhookPayload
    {
        public WebhookMovie Movie { get; set; }
        public AddMovieMethod AddMethod { get; set; }
    }
}
