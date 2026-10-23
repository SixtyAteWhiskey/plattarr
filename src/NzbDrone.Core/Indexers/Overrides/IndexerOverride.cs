using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Indexers.Overrides
{
    // Per-media-type indexer settings with no global equivalent (docs/module-interface.md,
    // "Indexer settings"). One row per indexer and media type, only when a user sets one.
    // An empty list or a null value means "not set".
    public class IndexerOverride : ModelBase
    {
        public IndexerOverride()
        {
            MultiLanguages = new List<int>();
            RequiredFlags = new List<int>();
            Categories = new List<int>();
        }

        public int IndexerId { get; set; }
        public MediaKind MediaKind { get; set; }

        // Languages a release tagged "MULTi" on this indexer contains. Movie language IDs
        // until the global catalog lands in step 8.
        public List<int> MultiLanguages { get; set; }

        // Indexer flags a release must carry. Radarr IndexerFlags values for movies.
        public List<int> RequiredFlags { get; set; }

        // Leave the year out of title searches (Radarr's RemoveYear).
        public bool? RemoveYear { get; set; }

        // Newznab categories to search and route instead of the media type's defaults,
        // for indexers that miscategorize.
        public List<int> Categories { get; set; }

        // TV: days after a season's last episode aired that this indexer's single episodes are
        // refused in favor of a season pack (Sonarr's SeasonSearchMaximumSingleEpisodeAge). 0 is off.
        public int SeasonSearchMaximumSingleEpisodeAge { get; set; }
    }
}
