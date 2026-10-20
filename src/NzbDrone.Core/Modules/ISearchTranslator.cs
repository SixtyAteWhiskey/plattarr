using System.Collections.Generic;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace NzbDrone.Core.Modules
{
    // Contract 3 in docs/module-interface.md. Translates a module's search request into
    // the indexer layer's criteria for one indexer.
    //
    // The result is ordered tiers. Each tier is a set of criteria run together; the next
    // tier runs only when the previous one returned nothing from that indexer. Radarr
    // searches by ID first and by title only as a fallback, and Sonarr does the same
    // with scene titles.
    public interface ISearchTranslator<in TRequest>
        where TRequest : ModuleSearchRequest
    {
        IEnumerable<IReadOnlyList<SearchCriteriaBase>> Translate(TRequest request, IndexerDefinition indexer);
    }
}
