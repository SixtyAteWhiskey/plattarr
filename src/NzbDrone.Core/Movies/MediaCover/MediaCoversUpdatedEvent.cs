using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Movies.MediaCover
{
    public class MediaCoversUpdatedEvent : IEvent
    {
        public Movie Movie { get; set; }
        public bool Updated { get; set; }

        public MediaCoversUpdatedEvent(Movie movie, bool updated)
        {
            Movie = movie;
            Updated = updated;
        }
    }
}
