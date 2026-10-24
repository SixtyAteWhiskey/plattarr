using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Music.MediaFiles.Commands;
using NzbDrone.Core.Music.MediaFiles.Events;
using NzbDrone.Core.Music.RootFolders;

namespace NzbDrone.Core.Music.MediaFiles
{
    public interface IDiskScanService
    {
        void Scan(List<string> folders = null, FilterFilesType filter = FilterFilesType.Known, bool addNewArtists = false, List<int> artistIds = null);
    }

    // The part of Lidarr's DiskScanService the metadata step needs: it checks the root and artist
    // folders, creates empty artist folders when asked and publishes ArtistScannedEvent, which
    // starts album monitoring (ArtistScannedHandler). Reading the audio files, import decisions and
    // table cleanup are the import step (step 5), which extends this class.
    public class DiskScanService :
        IDiskScanService,
        IExecute<RescanFoldersCommand>
    {
        private readonly IConfigService _configService;
        private readonly IDiskProvider _diskProvider;
        private readonly IArtistService _artistService;
        private readonly IRootFolderService _rootFolderService;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public DiskScanService(IConfigService configService,
                               IDiskProvider diskProvider,
                               IArtistService artistService,
                               IRootFolderService rootFolderService,
                               IEventAggregator eventAggregator,
                               Logger logger)
        {
            _configService = configService;
            _diskProvider = diskProvider;
            _artistService = artistService;
            _rootFolderService = rootFolderService;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public void Scan(List<string> folders = null, FilterFilesType filter = FilterFilesType.Known, bool addNewArtists = false, List<int> artistIds = null)
        {
            folders ??= _rootFolderService.All().Select(x => x.Path).ToList();
            artistIds ??= new List<int>();

            foreach (var folder in folders)
            {
                // We could be scanning a root folder or a subset of a root folder. If it's a subset,
                // check if the root folder exists before going on.
                var rootFolder = _rootFolderService.GetBestRootFolder(folder);

                if (rootFolder == null)
                {
                    _logger.Error("Not scanning {0}, it's not a subdirectory of a defined root folder", folder);
                    return;
                }

                if (_diskProvider.FolderExists(folder))
                {
                    _logger.ProgressInfo("Scanning {0}", folder);
                    continue;
                }

                if (!_diskProvider.FolderExists(rootFolder.Path))
                {
                    _logger.Warn("Artists' root folder ({0}) doesn't exist.", rootFolder.Path);
                    _artistService.GetArtists(artistIds).ForEach(x => _eventAggregator.PublishEvent(new ArtistScanSkippedEvent(x, ArtistScanSkippedReason.RootFolderDoesNotExist)));
                    return;
                }

                if (_diskProvider.FolderEmpty(rootFolder.Path))
                {
                    _logger.Warn("Artists' root folder ({0}) is empty.", rootFolder.Path);
                    _artistService.GetArtists(artistIds).ForEach(x => _eventAggregator.PublishEvent(new ArtistScanSkippedEvent(x, ArtistScanSkippedReason.RootFolderIsEmpty)));
                    return;
                }

                _logger.Debug("Specified scan folder ({0}) doesn't exist.", folder);
            }

            var artists = _artistService.GetArtists(artistIds);

            // Check for missing artist folders if specific artists are being scanned
            foreach (var artist in artists.Where(artist => !_diskProvider.FolderExists(artist.Path)))
            {
                if (!_configService.CreateEmptyArtistFolders)
                {
                    _logger.Debug("Artist folder doesn't exist: {0}", artist.Path);
                }
                else if (_configService.DeleteEmptyFolders)
                {
                    _logger.Debug("Not creating missing artist folder: {0} because delete empty folders is enabled", artist.Path);
                }
                else
                {
                    _logger.Debug("Creating missing artist folder: {0}", artist.Path);
                    _diskProvider.CreateFolder(artist.Path);
                }
            }

            foreach (var artist in artists)
            {
                _logger.Info("Completed scanning disk for {0}", artist.Name);
                _eventAggregator.PublishEvent(new ArtistScannedEvent(artist));
            }
        }

        public void Execute(RescanFoldersCommand message)
        {
            Scan(message.Folders, message.Filter, message.AddNewArtists, message.ArtistIds);
        }
    }
}
