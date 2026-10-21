using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.DecisionEngine.ReleaseSpecifications
{
    public class NotSampleSpecification : IReleaseSpecification
    {
        private readonly Logger _logger;

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public NotSampleSpecification(Logger logger)
        {
            _logger = logger;
        }

        public DownloadSpecDecision IsSatisfiedBy(RemoteRelease subject, ReleaseDecisionInformation information)
        {
            if (subject.Release.Title.ToLower().Contains("sample") && subject.Release.Size.GetValueOrDefault() < 70.Megabytes())
            {
                _logger.Debug("Sample release, rejecting.");
                return DownloadSpecDecision.Reject(DownloadRejectionReason.Sample, "Sample");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
