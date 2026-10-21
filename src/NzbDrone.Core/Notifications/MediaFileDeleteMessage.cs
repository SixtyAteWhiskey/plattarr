using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Notifications
{
    public class MediaFileDeleteMessage
    {
        public string Message { get; set; }
        public ActivitySubject Subject { get; set; }
        public IMediaNotificationData Data { get; set; }
        public NotificationFile File { get; set; }

        public DeleteMediaFileReason Reason { get; set; }

        public override string ToString()
        {
            return Message;
        }
    }
}
