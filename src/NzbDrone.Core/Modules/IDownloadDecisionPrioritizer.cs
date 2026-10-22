using System.Collections.Generic;
using NzbDrone.Core.DecisionEngine;

namespace NzbDrone.Core.Modules
{
    // Orders the download decisions of one media type (contract 4). Which release wins for an item
    // is the module's rule: Radarr and Sonarr compare quality, custom formats, protocol and
    // indexer differently. The core splits a mixed list by Kind and concatenates the results.
    public interface IDownloadDecisionPrioritizer
    {
        MediaKind Kind { get; }

        List<DownloadDecision> Prioritize(List<DownloadDecision> decisions);
    }
}
