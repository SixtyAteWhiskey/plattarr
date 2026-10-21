using NzbDrone.Core.IndexerSearch;

namespace NzbDrone.Core.DecisionEngine;

// Core (contract 4). SearchCriteria is the module's own request type, null for RSS and pushed releases.
public class ReleaseDecisionInformation
{
    public bool PushedRelease { get; set; }
    public ModuleSearchRequest SearchCriteria { get; set; }

    public ReleaseDecisionInformation()
    {
        PushedRelease = false;
        SearchCriteria = null;
    }

    public ReleaseDecisionInformation(bool pushedRelease, ModuleSearchRequest searchCriteria)
    {
        PushedRelease = pushedRelease;
        SearchCriteria = searchCriteria;
    }
}
