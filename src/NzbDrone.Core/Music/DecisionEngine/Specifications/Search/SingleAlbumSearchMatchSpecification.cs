using System.Linq;
using NLog;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Search;

namespace NzbDrone.Core.Music.DecisionEngine.Specifications.Search
{
    public class SingleAlbumSearchMatchSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public SingleAlbumSearchMatchSpecification(Logger logger)
        {
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual DownloadSpecDecision IsSatisfiedBy(RemoteAlbum remoteAlbum, ReleaseDecisionInformation information)
        {
            if (information.SearchCriteria is not AlbumSearchRequest singleAlbumSpec)
            {
                return DownloadSpecDecision.Accept();
            }

            if (Parser.Parser.CleanArtistName(singleAlbumSpec.AlbumTitle) != Parser.Parser.CleanArtistName(remoteAlbum.ParsedAlbumInfo.AlbumTitle))
            {
                _logger.Debug("Album does not match searched album title, skipping.");
                return DownloadSpecDecision.Reject(DownloadRejectionReason.WrongAlbum, "Wrong album");
            }

            if (!remoteAlbum.ParsedAlbumInfo.AlbumTitle.Any())
            {
                _logger.Debug("Full discography result during single album search, skipping.");
                return DownloadSpecDecision.Reject(DownloadRejectionReason.FullArtistPack, "Full artist pack");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
