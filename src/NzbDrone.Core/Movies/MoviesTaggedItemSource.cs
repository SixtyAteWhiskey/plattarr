using System.Collections.Generic;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Movies
{
    public class MoviesTaggedItemSource : ITaggedItemSource
    {
        private readonly IMovieService _movieService;

        public MoviesTaggedItemSource(IMovieService movieService)
        {
            _movieService = movieService;
        }

        public MediaKind Kind => MediaKind.Movies;

        public Dictionary<int, List<int>> AllItemTags()
        {
            return _movieService.AllMovieTags();
        }
    }
}
