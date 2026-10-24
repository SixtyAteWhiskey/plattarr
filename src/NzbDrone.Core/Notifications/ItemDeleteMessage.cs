using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Notifications
{
    // A library item below the media entry is deleted: an album of an artist (Lidarr's "On Album Delete").
    // The media entry itself is MediaDeleteMessage.
    public class ItemDeleteMessage
    {
        public string Message { get; set; }
        public ActivitySubject Subject { get; set; }
        public IMediaNotificationData Data { get; set; }
        public bool DeletedFiles { get; set; }
        public string DeletedFilesMessage { get; set; }

        public override string ToString()
        {
            return Message;
        }
    }
}
