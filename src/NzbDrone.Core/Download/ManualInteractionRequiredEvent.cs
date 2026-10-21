using NzbDrone.Common.Messaging;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications;

namespace NzbDrone.Core.Download
{
    // A tracked download needs the user. The owning module publishes it with its own data (Data.Kind),
    // and the same module's notification handler turns it into a notification.
    public class ManualInteractionRequiredEvent : IEvent
    {
        public TrackedDownload TrackedDownload { get; private set; }
        public IMediaNotificationData Data { get; private set; }

        public MediaKind Kind => Data.Kind;

        public ManualInteractionRequiredEvent(TrackedDownload trackedDownload, IMediaNotificationData data)
        {
            TrackedDownload = trackedDownload;
            Data = data;
        }
    }
}
