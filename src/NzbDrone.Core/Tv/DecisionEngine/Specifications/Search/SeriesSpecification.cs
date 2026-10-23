using NLog;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Tv.Search;

namespace NzbDrone.Core.Tv.DecisionEngine.Specifications.Search
{
    public class SeriesSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public SeriesSpecification(Logger logger)
        {
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public DownloadSpecDecision IsSatisfiedBy(RemoteEpisode remoteEpisode, ReleaseDecisionInformation information)
        {
            var searchCriteria = information.SearchCriteria as TvSearchRequest;

            if (searchCriteria == null)
            {
                return DownloadSpecDecision.Accept();
            }

            _logger.Debug("Checking if series matches searched series");

            if (remoteEpisode.Series.Id != searchCriteria.Series.Id)
            {
                _logger.Debug("Series {0} does not match {1}", remoteEpisode.Series, searchCriteria.Series);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.WrongSeries, "Wrong series");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
