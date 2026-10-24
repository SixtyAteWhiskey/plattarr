using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Music.MediaFiles.Events
{
    public class ArtistScannedEvent : IEvent
    {
        public Artist Artist { get; private set; }

        public ArtistScannedEvent(Artist artist)
        {
            Artist = artist;
        }
    }
}
