using System.Collections.Generic;

namespace NzbDrone.Core.Movies.MetadataSource
{
    public interface ISearchForNewMovie
    {
        List<Movie> SearchForNewMovie(string title);

        MovieMetadata MapMovieToTmdbMovie(MovieMetadata movie);
    }
}
