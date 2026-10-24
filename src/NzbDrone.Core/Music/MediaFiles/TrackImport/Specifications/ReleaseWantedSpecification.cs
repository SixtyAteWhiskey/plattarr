using NLog;
using NzbDrone.Core.Download;
using NzbDrone.Core.Music.Parser.Model;

namespace NzbDrone.Core.Music.MediaFiles.TrackImport.Specifications
{
    public class ReleaseWantedSpecification : IImportDecisionEngineSpecification<LocalAlbumRelease>
    {
        private readonly Logger _logger;

        public ReleaseWantedSpecification(Logger logger)
        {
            _logger = logger;
        }

        public ImportSpecDecision IsSatisfiedBy(LocalAlbumRelease item, DownloadClientItem downloadClientItem)
        {
            if (item.AlbumRelease.Monitored || item.AlbumRelease.Album.Value.AnyReleaseOk)
            {
                return ImportSpecDecision.Accept();
            }

            _logger.Debug("AlbumRelease {0} was not requested", item);
            return ImportSpecDecision.Reject(ImportRejectionReason.AlbumNotRequested, "Album release not requested");
        }
    }
}
