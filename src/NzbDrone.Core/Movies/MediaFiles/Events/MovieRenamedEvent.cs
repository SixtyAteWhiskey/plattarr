using System.Collections.Generic;
using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Movies.MediaFiles.Events
{
    public class MovieRenamedEvent : IEvent
    {
        public Movie Movie { get; private set; }
        public List<RenamedMovieFile> RenamedFiles { get; private set; }

        public MovieRenamedEvent(Movie movie, List<RenamedMovieFile> renamedFiles)
        {
            Movie = movie;
            RenamedFiles = renamedFiles;
        }
    }
}
