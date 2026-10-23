using System.Collections.Generic;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications.Webhook;

namespace NzbDrone.Core.Tv.Notifications
{
    public class WebhookEpisodeDeletePayload : WebhookPayload
    {
        public WebhookSeries Series { get; set; }
        public List<WebhookEpisode> Episodes { get; set; }
        public WebhookEpisodeFile EpisodeFile { get; set; }
        public DeleteMediaFileReason DeleteReason { get; set; }
    }
}
