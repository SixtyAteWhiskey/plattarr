using System.Collections.Generic;
using NzbDrone.Core.Movies.ImportLists.ImportListMovies;

namespace NzbDrone.Core.Movies.ImportLists
{
    public interface IParseImportListResponse
    {
        IList<ImportListMovie> ParseResponse(ImportListResponse importListResponse);
    }
}
