using System.Collections.Generic;
using NzbDrone.Core.Notifications.Webhook;

namespace NzbDrone.Core.Tv.Notifications
{
    public class WebhookRenamePayload : WebhookPayload
    {
        public WebhookSeries Series { get; set; }
        public List<WebhookRenamedEpisodeFile> RenamedEpisodeFiles { get; set; }
    }
}
