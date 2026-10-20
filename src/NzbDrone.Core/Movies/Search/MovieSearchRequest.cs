using NzbDrone.Core.IndexerSearch;

namespace NzbDrone.Core.Movies.Search
{
    // Formerly Radarr's NzbDrone.Core.IndexerSearch.Definitions.MovieSearchCriteria.
    // Renamed so Prowlarr's indexer-layer criteria types can take that namespace.
    public class MovieSearchRequest : ModuleSearchRequest
    {
        public Movie Movie { get; set; }

        public override string ToString()
        {
            return string.Format("[{0}]", Movie.Title);
        }
    }
}
