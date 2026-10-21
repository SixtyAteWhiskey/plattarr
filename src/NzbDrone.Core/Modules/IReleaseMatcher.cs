using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Modules
{
    // Contract 2 in docs/module-interface.md. A media module maps releases from the
    // indexer layer to its own library entities. The core never parses titles.
    public interface IReleaseMatcher
    {
        MediaKind Kind { get; }

        // Null when the title doesn't parse. A parsed release that matches no library
        // entity comes back with a null Parent, so the decision engine can report it.
        RemoteRelease Map(ReleaseInfo release, ModuleSearchRequest request);
    }
}
