using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Movies.Covers
{
    public class MovieCoverBackfillSource : IMediaCoverBackfillSource
    {
        private readonly IMovieService _movies;
        private readonly IEnsureMovieCovers _covers;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        public MovieCoverBackfillSource(IMovieService movies, IEnsureMovieCovers covers, IDiskProvider diskProvider, Logger logger)
        {
            _movies = movies;
            _covers = covers;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public MediaKind Kind => MediaKind.Movies;

        public CoverBackfillResult Backfill(IEnumerable<int> ids)
        {
            var importedIds = ids.Distinct().ToList();
            return importedIds.Count == 0 ? new CoverBackfillResult() : Run(_movies.GetMovies(importedIds));
        }

        public CoverBackfillResult BackfillAll()
        {
            return Run(_movies.GetAllMovies());
        }

        private CoverBackfillResult Run(IEnumerable<Movie> movies)
        {
            return MediaCoverBackfill.Run(
                movies,
                m => m.Id,
                m => m.MovieMetadata.Value.Images,
                (id, type, height) => _covers.GetCoverPath(id, type, height),
                m => _covers.EnsureCovers(m),
                _diskProvider,
                _logger,
                "Movie");
        }
    }
}
