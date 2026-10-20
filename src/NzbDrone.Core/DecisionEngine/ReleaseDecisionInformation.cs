using NzbDrone.Core.Movies.Search;

namespace NzbDrone.Core.DecisionEngine;

public class ReleaseDecisionInformation
{
    public bool PushedRelease { get; set; }
    public MovieSearchRequest SearchCriteria { get; set; }

    public ReleaseDecisionInformation()
    {
        PushedRelease = false;
        SearchCriteria = null;
    }

    public ReleaseDecisionInformation(bool pushedRelease, MovieSearchRequest searchCriteria)
    {
        PushedRelease = pushedRelease;
        SearchCriteria = searchCriteria;
    }
}
