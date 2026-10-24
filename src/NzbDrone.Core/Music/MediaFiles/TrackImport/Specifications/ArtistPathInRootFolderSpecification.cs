using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.RootFolders;

namespace NzbDrone.Core.Music.MediaFiles.TrackImport.Specifications
{
    public class ArtistPathInRootFolderSpecification : IImportDecisionEngineSpecification<LocalAlbumRelease>
    {
        private readonly IRootFolderService _rootFolderService;
        private readonly Logger _logger;

        public ArtistPathInRootFolderSpecification(IRootFolderService rootFolderService,
                                                   Logger logger)
        {
            _rootFolderService = rootFolderService;
            _logger = logger;
        }

        public ImportSpecDecision IsSatisfiedBy(LocalAlbumRelease item, DownloadClientItem downloadClientItem)
        {
            // Prevent imports to artists that are no longer inside a root folder Lidarr manages
            var artist = item.AlbumRelease.Album.Value.Artist.Value;

            // a new artist will have empty path, and will end up having path assigned based on file location
            var pathToCheck = artist.Path.IsNotNullOrWhiteSpace() ? artist.Path : item.LocalTracks.First().Path.GetParentPath();

            if (_rootFolderService.GetBestRootFolder(pathToCheck) == null)
            {
                _logger.Warn($"Destination folder {pathToCheck} not in a Root Folder, skipping import");
                return ImportSpecDecision.Reject(ImportRejectionReason.ArtistPathNotInRootFolder, $"Destination folder {pathToCheck} is not in a Root Folder");
            }

            return ImportSpecDecision.Accept();
        }
    }
}
