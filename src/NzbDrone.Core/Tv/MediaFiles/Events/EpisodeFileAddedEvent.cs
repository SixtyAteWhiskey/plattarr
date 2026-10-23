using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Tv.MediaFiles.Events
{
    public class EpisodeFileAddedEvent : IEvent
    {
        public EpisodeFile EpisodeFile { get; private set; }

        public EpisodeFileAddedEvent(EpisodeFile episodeFile)
        {
            EpisodeFile = episodeFile;
        }
    }
}
