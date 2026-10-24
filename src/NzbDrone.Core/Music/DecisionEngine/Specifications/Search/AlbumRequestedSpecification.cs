using System.Linq;
using NLog;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Search;

namespace NzbDrone.Core.Music.DecisionEngine.Specifications.Search
{
    public class AlbumRequestedSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public AlbumRequestedSpecification(Logger logger)
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

            var criteriaAlbum = searchCriteria.Albums.Select(v => v.Id).ToList();
            var remoteAlbums = remoteAlbum.Albums.Select(v => v.Id).ToList();

            if (!criteriaAlbum.Intersect(remoteAlbums).Any())
            {
                _logger.Debug("Release rejected since the album wasn't requested: {0}", remoteAlbum.ParsedAlbumInfo);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.AlbumNotRequested, "Album wasn't requested");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
