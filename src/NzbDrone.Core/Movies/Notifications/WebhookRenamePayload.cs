using System.Collections.Generic;
using NzbDrone.Core.Notifications.Webhook;

namespace NzbDrone.Core.Movies.Notifications
{
    public class WebhookRenamePayload : WebhookPayload
    {
        public WebhookMovie Movie { get; set; }
        public List<WebhookRenamedMovieFile> RenamedMovieFiles { get; set; }
    }
}
