using NzbDrone.Common.Messaging;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Music.MediaFiles.Events
{
    public class TrackFileDeletedEvent : IEvent
    {
        public TrackFile TrackFile { get; private set; }
        public DeleteMediaFileReason Reason { get; private set; }

        public TrackFileDeletedEvent(TrackFile trackFile, DeleteMediaFileReason reason)
        {
            TrackFile = trackFile;
            Reason = reason;
        }
    }
}
