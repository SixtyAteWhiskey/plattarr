using System.Linq;
using NLog;
using NzbDrone.Core.Download;
using NzbDrone.Core.Music.Parser.Model;

namespace NzbDrone.Core.Music.MediaFiles.TrackImport.Specifications
{
    public class MoreTracksSpecification : IImportDecisionEngineSpecification<LocalAlbumRelease>
    {
        private readonly Logger _logger;

        public MoreTracksSpecification(Logger logger)
        {
            _logger = logger;
        }

        public ImportSpecDecision IsSatisfiedBy(LocalAlbumRelease item, DownloadClientItem downloadClientItem)
        {
            var existingRelease = item.AlbumRelease.Album.Value.AlbumReleases.Value.Single(x => x.Monitored);
            var existingTrackCount = existingRelease.Tracks.Value.Count(x => x.HasFile);
            if (item.AlbumRelease.Id != existingRelease.Id &&
                item.TrackCount < existingTrackCount)
            {
                _logger.Debug($"This release has fewer tracks ({item.TrackCount}) than existing {existingRelease} ({existingTrackCount}). Skipping {item}");
                return ImportSpecDecision.Reject(ImportRejectionReason.FewerTracksThanExisting, "Has fewer tracks than existing release");
            }

            _logger.Trace("Accepting release {0}", item);
            return ImportSpecDecision.Accept();
        }
    }
}
