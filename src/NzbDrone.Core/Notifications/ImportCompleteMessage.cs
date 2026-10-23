using System.Collections.Generic;
using NzbDrone.Core.Download;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Notifications
{
    // Every file of one download is imported (Sonarr's "On Import Complete"). OnDownload fires once per
    // file; this fires once per download, after the last file, so a season pack sends one message.
    public class ImportCompleteMessage
    {
        public string Message { get; set; }
        public ActivitySubject Subject { get; set; }
        public IMediaNotificationData Data { get; set; }
        public List<NotificationFile> Files { get; set; } = new();
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
        public DownloadClientItemClientInfo DownloadClientInfo { get; set; }
        public string DownloadId { get; set; }
        public NotificationRelease Release { get; set; }
        public string ReleaseGroup { get; set; }
        public string QualityName { get; set; }

        public override string ToString()
        {
            return Message;
        }
    }
}
