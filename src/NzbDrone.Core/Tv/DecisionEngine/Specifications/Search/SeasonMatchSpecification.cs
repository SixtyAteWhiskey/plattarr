using NLog;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Tv.Search;

namespace NzbDrone.Core.Tv.DecisionEngine.Specifications.Search
{
    public class SeasonMatchSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public SeasonMatchSpecification(Logger logger)
        {
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public DownloadSpecDecision IsSatisfiedBy(RemoteEpisode remoteEpisode, ReleaseDecisionInformation information)
        {
            if (information.SearchCriteria == null)
            {
                return DownloadSpecDecision.Accept();
            }

            var singleEpisodeSpec = information.SearchCriteria as SeasonSearchRequest;

            if (singleEpisodeSpec == null)
            {
                return DownloadSpecDecision.Accept();
            }

            var seasonNumbers = remoteEpisode.ParsedEpisodeInfo.SeasonNumbers;

            var matches = seasonNumbers.Count > 0
                ? seasonNumbers.Contains(singleEpisodeSpec.SeasonNumber)
                : singleEpisodeSpec.SeasonNumber == remoteEpisode.MappedSeasonNumber;

            if (!matches)
            {
                _logger.Debug("Season number does not match searched season number, skipping.");
                return DownloadSpecDecision.Reject(DownloadRejectionReason.WrongSeason, "Wrong season");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
