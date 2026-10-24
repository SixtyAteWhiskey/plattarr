using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Music.History;

namespace NzbDrone.Core.Music.Import
{
    public interface ITrackedDownloadAlreadyImported
    {
        bool IsImported(TrackedDownload trackedDownload, List<AlbumHistory> historyItems);
    }

    // Lidarr's TrackedDownloadAlreadyImported: a download is imported when the latest history row of
    // every album it was matched to is an import.
    public class TrackedDownloadAlreadyImported : ITrackedDownloadAlreadyImported
    {
        private readonly Logger _logger;

        public TrackedDownloadAlreadyImported(Logger logger)
        {
            _logger = logger;
        }

        public bool IsImported(TrackedDownload trackedDownload, List<AlbumHistory> historyItems)
        {
            _logger.Trace("Checking if all items for '{0}' have been imported", trackedDownload.DownloadItem.Title);

            if (historyItems.Empty())
            {
                _logger.Trace("No history for {0}", trackedDownload.DownloadItem.Title);
                return false;
            }

            var remoteAlbum = trackedDownload.GetRemoteAlbum();

            if (remoteAlbum?.Albums == null)
            {
                return true;
            }

            var allAlbumsImportedInHistory = remoteAlbum.Albums.All(album =>
            {
                var lastHistoryItem = historyItems.FirstOrDefault(h => h.ItemId == album.Id);

                if (lastHistoryItem == null)
                {
                    _logger.Trace("No history for album: {0}", album);
                    return false;
                }

                _logger.Trace("Last event for album: {0} is: {1}", album, lastHistoryItem.EventType);

                return lastHistoryItem.EventType is AlbumHistoryEventType.DownloadImported or AlbumHistoryEventType.TrackFileImported;
            });

            _logger.Trace("All albums for '{0}' have been imported: {1}", trackedDownload.DownloadItem.Title, allAlbumsImportedInHistory);

            return allAlbumsImportedInHistory;
        }
    }
}
