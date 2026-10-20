using System;
using System.Collections.Generic;
using NzbDrone.Core.Movies.Search;

namespace NzbDrone.Core.Indexers
{
    public interface IIndexerRequestGenerator
    {
        IndexerPageableRequestChain GetRecentRequests();
        IndexerPageableRequestChain GetSearchRequests(MovieSearchRequest searchCriteria);
        Func<IDictionary<string, string>> GetCookies { get; set; }
        Action<IDictionary<string, string>, DateTime?> CookiesUpdater { get; set; }
    }
}
