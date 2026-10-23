using NzbDrone.Common.Messaging;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Tv.MediaFiles.Events
{
    public class EpisodeFileDeletedEvent : IEvent
    {
        public EpisodeFile EpisodeFile { get; private set; }
        public DeleteMediaFileReason Reason { get; private set; }

        public EpisodeFileDeletedEvent(EpisodeFile episodeFile, DeleteMediaFileReason reason)
        {
            EpisodeFile = episodeFile;
            Reason = reason;
        }
    }
}
