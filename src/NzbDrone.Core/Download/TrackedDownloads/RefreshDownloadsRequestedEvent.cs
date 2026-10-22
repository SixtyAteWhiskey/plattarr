using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Download.TrackedDownloads
{
    // A media type's library changed in a way that settles a download (a file was imported). The
    // download monitor refreshes soon after, debounced with the other triggers.
    public class RefreshDownloadsRequestedEvent : IEvent
    {
    }
}
