using NzbDrone.Common.Messaging;
using NzbDrone.Core.Download.TrackedDownloads;

namespace NzbDrone.Core.Download
{
    public class DownloadCompletedEvent : IEvent
    {
        public TrackedDownload TrackedDownload { get; private set; }

        // The movie, series or artist the download was imported for. The media type is
        // TrackedDownload.Kind.
        public int ParentId { get; set; }

        public DownloadCompletedEvent(TrackedDownload trackedDownload, int parentId)
        {
            TrackedDownload = trackedDownload;
            ParentId = parentId;
        }
    }
}
