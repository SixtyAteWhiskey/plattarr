using System.Collections.Generic;
using System.Threading.Tasks;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Modules
{
    // Receives the RSS releases routed to one media type (contract 2). The core fetches
    // RSS once per indexer; each module makes its own download decisions.
    public interface IRssReleaseHandler
    {
        MediaKind Kind { get; }

        Task Process(IReadOnlyList<ReleaseInfo> releases);
    }
}
