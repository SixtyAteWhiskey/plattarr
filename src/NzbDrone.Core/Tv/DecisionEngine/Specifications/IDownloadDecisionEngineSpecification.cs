using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv.DecisionEngine.Specifications
{
    public interface IDownloadDecisionEngineSpecification
    {
        RejectionType Type { get; }

        SpecificationPriority Priority { get; }

        DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information);
    }
}
