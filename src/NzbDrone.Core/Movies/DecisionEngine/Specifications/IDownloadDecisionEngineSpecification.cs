using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Movies.Parser.Model;

namespace NzbDrone.Core.Movies.DecisionEngine.Specifications
{
    public interface IDownloadDecisionEngineSpecification
    {
        RejectionType Type { get; }

        SpecificationPriority Priority { get; }

        DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, ReleaseDecisionInformation information);
    }
}
