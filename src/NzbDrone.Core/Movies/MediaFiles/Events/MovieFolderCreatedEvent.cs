using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Movies.MediaFiles.Events
{
    public class MovieFolderCreatedEvent : IEvent
    {
        public Movie Movie { get; private set; }
        public MovieFile MovieFile { get; private set; }
        public string MovieFileFolder { get; set; }
        public string MovieFolder { get; set; }

        public MovieFolderCreatedEvent(Movie movie, MovieFile movieFile)
        {
            Movie = movie;
            MovieFile = movieFile;
        }
    }
}
