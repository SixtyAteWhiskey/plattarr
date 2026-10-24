using System;
using System.Collections.Generic;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Notifications
{
    // The tags of a media file were rewritten (Lidarr's "On Track Retag").
    public class RetagMessage
    {
        public string Message { get; set; }
        public ActivitySubject Subject { get; set; }
        public IMediaNotificationData Data { get; set; }
        public NotificationFile File { get; set; }

        // Tag name to (old value, new value).
        public Dictionary<string, Tuple<string, string>> Diff { get; set; } = new();
        public bool Scrubbed { get; set; }

        public override string ToString()
        {
            return Message;
        }
    }
}
