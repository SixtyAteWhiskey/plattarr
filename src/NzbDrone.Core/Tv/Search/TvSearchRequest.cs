using System.Collections.Generic;
using NzbDrone.Core.IndexerSearch;

namespace NzbDrone.Core.Tv.Search
{
    // Sonarr's search criteria on the module request base (as MovieSearchRequest is for Movies).
    // TV step 3 needs only what the parsing service reads: the series and the episodes asked for.
    // The search shapes (single episode, season, daily, anime, special) come with step 4.
    public class TvSearchRequest : ModuleSearchRequest
    {
        public Series Series { get; set; }
        public List<Episode> Episodes { get; set; }

        public override string ToString()
        {
            return string.Format("[{0}]", Series?.Title);
        }
    }
}
