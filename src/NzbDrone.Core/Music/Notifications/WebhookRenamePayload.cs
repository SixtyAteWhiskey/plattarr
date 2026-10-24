using System.Collections.Generic;
using NzbDrone.Core.Notifications.Webhook;

namespace NzbDrone.Core.Music.Notifications
{
    public class WebhookRenamePayload : WebhookPayload
    {
        public WebhookArtist Artist { get; set; }
        public List<WebhookRenamedTrackFile> RenamedTrackFiles { get; set; }
    }
}
