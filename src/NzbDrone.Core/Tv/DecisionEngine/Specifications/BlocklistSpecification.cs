using NLog;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Tv.Blocklisting;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv.DecisionEngine.Specifications
{
    public class BlocklistSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly IBlocklistService _blocklistService;
        private readonly Logger _logger;

        public BlocklistSpecification(IBlocklistService blocklistService, Logger logger)
        {
            _blocklistService = blocklistService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Database;
        public RejectionType Type => RejectionType.Permanent;

        public DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            if (_blocklistService.Blocklisted(subject.Series.Id, subject.Release))
            {
                _logger.Debug("{0} is blocklisted, rejecting.", subject.Release.Title);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.Blocklisted, "Release is blocklisted");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
