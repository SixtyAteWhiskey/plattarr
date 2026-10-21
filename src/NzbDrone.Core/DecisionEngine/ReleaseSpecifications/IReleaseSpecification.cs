using NzbDrone.Core.Modules;

namespace NzbDrone.Core.DecisionEngine.ReleaseSpecifications
{
    // Contract 4 (docs/module-interface.md): a download decision rule that reads only the release
    // and the parent entity's IMediaParent members, so it runs for every media type. Each module's
    // decision maker runs these together with its own typed specifications, grouped by Priority.
    public interface IReleaseSpecification
    {
        SpecificationPriority Priority { get; }

        RejectionType Type { get; }

        DownloadSpecDecision IsSatisfiedBy(RemoteRelease subject, ReleaseDecisionInformation information);
    }
}
