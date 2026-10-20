using NLog;
using NzbDrone.Core.Indexers.Overrides;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class RequiredIndexerFlagsSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly IIndexerOverrideService _indexerOverrideService;
        private readonly Logger _logger;

        public RequiredIndexerFlagsSpecification(IIndexerOverrideService indexerOverrideService, Logger logger)
        {
            _indexerOverrideService = indexerOverrideService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, ReleaseDecisionInformation information)
        {
            // Radarr kept RequiredFlags on torrent indexers only; the override keeps that scope.
            if (subject.Release is not TorrentInfo torrentInfo || torrentInfo.IndexerId == 0)
            {
                return DownloadSpecDecision.Accept();
            }

            var requiredFlags = _indexerOverrideService.Get(torrentInfo.IndexerId, MediaKind.Movies).RequiredFlags;

            if (requiredFlags.Count == 0)
            {
                return DownloadSpecDecision.Accept();
            }

            var releaseFlags = torrentInfo.ToIndexerFlags();
            var requiredFlag = (IndexerFlags)0;

            foreach (var flag in requiredFlags)
            {
                if (releaseFlags.HasFlag((IndexerFlags)flag))
                {
                    return DownloadSpecDecision.Accept();
                }

                requiredFlag |= (IndexerFlags)flag;
            }

            _logger.Debug("None of the required indexer flags {0} where found. Found flags: {1}", requiredFlag, releaseFlags);
            return DownloadSpecDecision.Reject(DownloadRejectionReason.RequiredFlags, "None of the required indexer flags {0} where found. Found flags: {1}", requiredFlag, releaseFlags);
        }
    }
}
