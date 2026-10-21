using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Notifications
{
    // The module-typed data a message carries beside its neutral fields. Only the owning module's
    // builders (webhook payloads, script environment, Trakt collection) read it: they cast to the
    // module's own class after choosing the builder by Kind. Providers that format text never touch it.
    public interface IMediaNotificationData
    {
        MediaKind Kind { get; }
    }
}
