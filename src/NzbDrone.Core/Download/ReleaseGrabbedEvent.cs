using NzbDrone.Common.Messaging;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Download
{
    // Published by the core after a release was sent to a download client. Module handlers act
    // on the kinds they own (Release.Kind) and read the release through their own extension.
    public class ReleaseGrabbedEvent : IEvent
    {
        public RemoteRelease Release { get; private set; }
        public int DownloadClientId { get; set; }
        public string DownloadClient { get; set; }
        public string DownloadClientName { get; set; }
        public string DownloadId { get; set; }

        public ReleaseGrabbedEvent(RemoteRelease release)
        {
            Release = release;
        }
    }
}
