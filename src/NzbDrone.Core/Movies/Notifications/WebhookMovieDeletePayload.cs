using NzbDrone.Core.Notifications.Webhook;

namespace NzbDrone.Core.Movies.Notifications
{
    public class WebhookMovieDeletePayload : WebhookPayload
    {
        public WebhookMovie Movie { get; set; }
        public bool DeletedFiles { get; set; }
        public long MovieFolderSize { get; set; }
    }
}
