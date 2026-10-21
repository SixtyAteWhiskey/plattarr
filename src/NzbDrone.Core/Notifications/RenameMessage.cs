using System.Collections.Generic;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Notifications
{
    public class RenameMessage
    {
        public ActivitySubject Subject { get; set; }
        public IMediaNotificationData Data { get; set; }
        public List<NotificationRenamedFile> Files { get; set; } = new();
    }
}
