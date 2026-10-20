using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Movies.Search
{
    // Contract 3 for movies. Same request shape Radarr sent to Prowlarr:
    // tier 1 searches by the IDs the indexer supports; tier 2 searches each scene title
    // with the year, and runs only when tier 1 found nothing.
    public class MovieSearchTranslator : ISearchTranslator<MovieSearchRequest>
    {
        public const string Source = "Movies";

        public IEnumerable<IReadOnlyList<SearchCriteriaBase>> Translate(MovieSearchRequest request, IndexerDefinition indexer)
        {
            var movie = request.Movie;
            var metadata = movie.MovieMetadata.Value;
            var capabilities = indexer.Capabilities;
            var categories = MovieReleaseMatcher.MovieCategories.ToArray();

            if (capabilities != null)
            {
                var tmdbId = capabilities.MovieSearchTmdbAvailable && metadata.TmdbId > 0 ? metadata.TmdbId : (int?)null;
                var imdbId = capabilities.MovieSearchImdbAvailable ? ParseUtil.GetImdbId(metadata.ImdbId)?.ToString("D7") : null;

                if (tmdbId.HasValue || imdbId.IsNotNullOrWhiteSpace())
                {
                    yield return new SearchCriteriaBase[]
                    {
                        new MovieSearchCriteria
                        {
                            TmdbId = tmdbId,
                            ImdbId = imdbId,
                            Categories = categories,
                            SearchType = "movie",
                            Source = Source
                        }
                    };
                }
            }

            if (movie.Year > 0 && request.SceneTitles != null && (capabilities == null || capabilities.SearchAvailable))
            {
                // RemoveYear becomes a per-indexer override (IndexerOverrides, step 7c).
                yield return request.SceneTitles
                    .Where(t => t.IsNotNullOrWhiteSpace())
                    .Select(t => (SearchCriteriaBase)new BasicSearchCriteria
                    {
                        SearchTerm = $"{t} {movie.Year}",
                        Categories = categories,
                        SearchType = "search",
                        Source = Source
                    })
                    .ToList();
            }
        }
    }
}
