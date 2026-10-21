using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Indexers
{
    // Radarr's indexer filters over Prowlarr's factory. Enable is the master switch; the three
    // flags pick what the indexer is used for (docs/module-interface.md, "Indexer settings").
    public static class IndexerFactoryExtensions
    {
        public static List<IIndexer> RssEnabled(this IIndexerFactory factory, bool filterBlockedIndexers = true)
        {
            return factory.Enabled(filterBlockedIndexers)
                .Where(i => i.SupportsRss && ((IndexerDefinition)i.Definition).EnableRss)
                .ToList();
        }

        public static List<IIndexer> AutomaticSearchEnabled(this IIndexerFactory factory, bool filterBlockedIndexers = true)
        {
            return factory.Enabled(filterBlockedIndexers)
                .Where(i => i.SupportsSearch && ((IndexerDefinition)i.Definition).EnableAutomaticSearch)
                .ToList();
        }

        public static List<IIndexer> InteractiveSearchEnabled(this IIndexerFactory factory, bool filterBlockedIndexers = true)
        {
            return factory.Enabled(filterBlockedIndexers)
                .Where(i => i.SupportsSearch && ((IndexerDefinition)i.Definition).EnableInteractiveSearch)
                .ToList();
        }
    }
}
