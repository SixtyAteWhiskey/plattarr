using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications.Webhook;

namespace NzbDrone.Core.Movies.Notifications
{
    public class WebhookMovieFileDeletePayload : WebhookPayload
    {
        public WebhookMovie Movie { get; set; }
        public WebhookMovieFile MovieFile { get; set; }
        public DeleteMediaFileReason DeleteReason { get; set; }
    }
}
