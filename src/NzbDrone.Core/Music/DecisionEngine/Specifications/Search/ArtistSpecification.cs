using NLog;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Search;

namespace NzbDrone.Core.Music.DecisionEngine.Specifications.Search
{
    public class ArtistSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public ArtistSpecification(Logger logger)
        {
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public DownloadSpecDecision IsSatisfiedBy(RemoteAlbum remoteAlbum, ReleaseDecisionInformation information)
        {
            if (information.SearchCriteria is not MusicSearchRequest searchCriteria)
            {
                return DownloadSpecDecision.Accept();
            }

            _logger.Debug("Checking if artist matches searched artist");

            if (remoteAlbum.Artist.Id != searchCriteria.Artist.Id)
            {
                _logger.Debug("Artist {0} does not match {1}", remoteAlbum.Artist, searchCriteria.Artist);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.WrongArtist, "Wrong artist");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
