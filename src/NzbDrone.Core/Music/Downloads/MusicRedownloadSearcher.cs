using NLog;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.Search;

namespace NzbDrone.Core.Music.Downloads
{
    // What Lidarr's RedownloadFailedDownloadService does once the settings allow a new search: one album
    // is searched again, a failure that covered every album of the artist runs an artist search, anything
    // else an album search for the albums it covered.
    public class MusicRedownloadSearcher : IRedownloadSearcher
    {
        private readonly IAlbumService _albumService;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly Logger _logger;

        public MusicRedownloadSearcher(IAlbumService albumService,
                                       IManageCommandQueue commandQueueManager,
                                       Logger logger)
        {
            _albumService = albumService;
            _commandQueueManager = commandQueueManager;
            _logger = logger;
        }

        public MediaKind Kind => MediaKind.Music;

        public CommandModel Search(DownloadFailedEvent failure)
        {
            if (failure.ItemIds.Count == 0)
            {
                _logger.Debug("Failed download has no albums, not searching again");
                return null;
            }

            if (failure.ItemIds.Count == 1)
            {
                _logger.Debug("Failed download only contains one album, searching again");

                return _commandQueueManager.Push(new AlbumSearchCommand(failure.ItemIds));
            }

            var albumsInArtist = _albumService.GetAlbumsByArtist(failure.ParentId);

            if (failure.ItemIds.Count == albumsInArtist.Count)
            {
                _logger.Debug("Failed download was entire artist, searching again");

                return _commandQueueManager.Push(new ArtistSearchCommand
                {
                    ArtistId = failure.ParentId
                });
            }

            _logger.Debug("Failed download contains multiple albums, searching again");

            return _commandQueueManager.Push(new AlbumSearchCommand(failure.ItemIds));
        }
    }
}
