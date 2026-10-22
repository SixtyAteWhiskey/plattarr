using NLog;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Movies.Search;

namespace NzbDrone.Core.Movies.DecisionEngine.Specifications.Search
{
    public class MovieSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public MovieSpecification(Logger logger)
        {
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, ReleaseDecisionInformation information)
        {
            if (information.SearchCriteria == null)
            {
                return DownloadSpecDecision.Accept();
            }

            // The movie decision maker only receives movie requests; anything else is a routing bug,
            // and the cast failure becomes a DecisionError rejection instead of a silent accept.
            var searchCriteria = (MovieSearchRequest)information.SearchCriteria;

            _logger.Debug("Checking if movie matches searched movie");

            if (subject.Movie.Id != searchCriteria.Movie.Id)
            {
                _logger.Debug("Movie {0} does not match {1}", subject.Movie, searchCriteria.Movie);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.WrongMovie, "Wrong movie");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
