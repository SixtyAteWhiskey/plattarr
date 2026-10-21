using NzbDrone.Core.Housekeeping;

namespace NzbDrone.Core.Movies.Housekeepers
{
    public class UpdateCleanTitleForMovies : IHousekeepingTask
    {
        private readonly IMovieRepository _movieRepository;

        public UpdateCleanTitleForMovies(IMovieRepository movieRepository)
        {
            _movieRepository = movieRepository;
        }

        public void Clean()
        {
            /*var movies = _movieRepository.All().ToList();

            movies.ForEach(m =>
            {
                m.CleanTitle = m.CleanTitle.CleanSeriesTitle();
                _movieRepository.Update(m);
            });*/
        }
    }
}
