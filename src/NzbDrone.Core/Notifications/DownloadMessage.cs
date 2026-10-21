using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Download;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Notifications
{
    public class DownloadMessage
    {
        public string Message { get; set; }
        public ActivitySubject Subject { get; set; }
        public IMediaNotificationData Data { get; set; }
        public NotificationFile File { get; set; }
        public List<NotificationFile> OldFiles { get; set; } = new();
        public string SourcePath { get; set; }
        public DownloadClientItemClientInfo DownloadClientInfo { get; set; }
        public string DownloadId { get; set; }
        public List<string> CustomFormats { get; set; } = new();
        public int CustomFormatScore { get; set; }

        public bool IsUpgrade => OldFiles.Any();

        public override string ToString()
        {
            return Message;
        }
    }
}
