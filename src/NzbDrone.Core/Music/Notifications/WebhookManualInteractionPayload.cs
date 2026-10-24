using System.Collections.Generic;
using NzbDrone.Core.Notifications.Webhook;

namespace NzbDrone.Core.Music.Notifications
{
    // Lidarr never sent a manual-interaction webhook. This is Radarr's and Sonarr's payload with the
    // artist and albums in Lidarr's shapes, so a receiver reads the same keys for every media type.
    public class WebhookManualInteractionPayload : WebhookPayload
    {
        public WebhookArtist Artist { get; set; }
        public List<WebhookAlbum> Albums { get; set; }
        public WebhookDownloadClientItem DownloadInfo { get; set; }
        public string DownloadClient { get; set; }
        public string DownloadClientType { get; set; }
        public string DownloadId { get; set; }
        public string DownloadStatus { get; set; }
        public List<WebhookDownloadStatusMessage> DownloadStatusMessages { get; set; }
    }
}
