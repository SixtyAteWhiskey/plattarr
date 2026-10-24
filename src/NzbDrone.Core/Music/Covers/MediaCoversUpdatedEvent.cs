using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Music.Covers
{
    public class MediaCoversUpdatedEvent : IEvent
    {
        public Artist Artist { get; set; }
        public Album Album { get; set; }
        public bool Updated { get; set; }

        public MediaCoversUpdatedEvent(Artist artist, bool updated)
        {
            Artist = artist;
            Updated = updated;
        }

        public MediaCoversUpdatedEvent(Album album, bool updated)
        {
            Album = album;
            Updated = updated;
        }
    }
}
