using NzbDrone.Core.Download;
using NzbDrone.Core.Movies.Parser.Model;

namespace NzbDrone.Core.Movies.MediaFiles.MovieImport.Aggregation.Aggregators
{
    public class AggregateMovie : IAggregateLocalMovie
    {
        public int Order => 1;

        private readonly IMovieService _movieService;

        public AggregateMovie(IMovieService movieService)
        {
            _movieService = movieService;
        }

        public LocalMovie Aggregate(LocalMovie localMovie, DownloadClientItem downloadClientItem)
        {
            localMovie.Movie = _movieService.GetMovie(localMovie.Movie.Id);

            return localMovie;
        }
    }
}
