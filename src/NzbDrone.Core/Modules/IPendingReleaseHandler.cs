using System;
using System.Collections.Generic;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download.Pending;

namespace NzbDrone.Core.Modules
{
    // Stores the releases the core decided to hold back (delayed, or the download client was
    // unavailable) for one media type (contract 6). The pending table keeps the module's own
    // parsed release, so only the module can write it.
    public interface IPendingReleaseHandler
    {
        MediaKind Kind { get; }

        void Add(DownloadDecision decision, PendingReleaseReason reason);

        void AddMany(List<Tuple<DownloadDecision, PendingReleaseReason>> decisions);
    }
}
