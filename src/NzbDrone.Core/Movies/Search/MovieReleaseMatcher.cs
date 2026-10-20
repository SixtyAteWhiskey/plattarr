using System.Collections.Generic;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Movies.Search
{
    public interface IMovieReleaseMatcher : IReleaseMatcher
    {
        RemoteMovie Map(ReleaseInfo release, MovieSearchRequest request);
    }

    // Contract 2 for movies: Radarr's title parser and movie lookup, moved out of
    // DownloadDecisionMaker unchanged.
    public class MovieReleaseMatcher : IMovieReleaseMatcher
    {
        public static readonly IReadOnlyList<int> MovieCategories = new[] { NewznabStandardCategory.Movies.Id };

        private readonly IParsingService _parsingService;

        public MovieReleaseMatcher(IParsingService parsingService)
        {
            _parsingService = parsingService;
        }

        public MediaKind Kind => MediaKind.Movies;

        public IReadOnlyList<int> Categories => MovieCategories;

        public RemoteMovie Map(ReleaseInfo release, MovieSearchRequest request)
        {
            var parsedMovieInfo = Parser.Parser.ParseMovieTitle(release.Title);

            if (parsedMovieInfo == null || parsedMovieInfo.PrimaryMovieTitle.IsNullOrWhiteSpace())
            {
                return null;
            }

            var remoteMovie = _parsingService.Map(parsedMovieInfo, release.ImdbId.ToString(), release.TmdbId, request);
            remoteMovie.Release = release;

            return remoteMovie;
        }

        RemoteRelease IReleaseMatcher.Map(ReleaseInfo release, ModuleSearchRequest request)
        {
            return Map(release, request as MovieSearchRequest);
        }
    }
}
