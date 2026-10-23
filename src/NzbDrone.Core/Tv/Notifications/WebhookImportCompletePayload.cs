using System.Collections.Generic;
using NzbDrone.Core.Notifications.Webhook;

namespace NzbDrone.Core.Tv.Notifications
{
    // Sonarr's "On Import Complete" payload: one per download, with every imported file. Sonarr sends it
    // with the Download event type; receivers tell it from a per-file import by episodeFiles.
    public class WebhookImportCompletePayload : WebhookPayload
    {
        public WebhookSeries Series { get; set; }
        public List<WebhookEpisode> Episodes { get; set; }
        public List<WebhookEpisodeFile> EpisodeFiles { get; set; }
        public string DownloadClient { get; set; }
        public string DownloadClientType { get; set; }
        public string DownloadId { get; set; }
        public WebhookGrabbedRelease Release { get; set; }
        public int FileCount => EpisodeFiles.Count;
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
    }
}
