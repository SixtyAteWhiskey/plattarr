using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Music.Parser.Model;

namespace NzbDrone.Core.Music.DecisionEngine.Specifications
{
    // Lidarr's IDecisionEngineSpecification behind the module contract (docs/module-interface.md,
    // contract 4): it runs only for Music releases, in one priority order with the core specifications.
    public interface IDownloadDecisionEngineSpecification
    {
        RejectionType Type { get; }

        SpecificationPriority Priority { get; }

        DownloadSpecDecision IsSatisfiedBy(RemoteAlbum subject, ReleaseDecisionInformation information);
    }
}
