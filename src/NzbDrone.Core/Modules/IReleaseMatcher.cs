using System.Collections.Generic;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Modules
{
    // Contract 2 in docs/module-interface.md. A media module maps releases from the
    // indexer layer to its own library entities. The core never parses titles.
    public interface IReleaseMatcher
    {
        MediaKind Kind { get; }

        // Top-level Newznab categories this media type searches and receives from RSS
        // (2000 for movies). Moves to IMediaModule.DefaultCategories with the module
        // boundary (porting step 9).
        IReadOnlyList<int> Categories { get; }

        // Null when the title doesn't parse. A parsed release that matches no library
        // entity comes back with a null Parent, so the decision engine can report it.
        RemoteRelease Map(ReleaseInfo release, ModuleSearchRequest request);
    }
}
