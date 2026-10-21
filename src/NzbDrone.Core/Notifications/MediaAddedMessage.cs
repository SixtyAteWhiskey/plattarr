using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Notifications
{
    public class MediaAddedMessage
    {
        public ActivitySubject Subject { get; set; }
        public IMediaNotificationData Data { get; set; }
    }
}
