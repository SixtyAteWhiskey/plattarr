using System;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.Events;
using NzbDrone.Core.Music.History;
using NzbDrone.Core.Music.MediaFiles.Events;

namespace NzbDrone.Core.Music.Downloads
{
    // The Music events that write download history: a file imported from a download (or one found by
    // manual import), and artists deleted. The core DownloadHistoryService handles the events that
    // every media type raises.
    public class MusicDownloadHistoryHandler : IHandle<TrackImportedEvent>,
                                               IHandle<ArtistsDeletedEvent>
    {
        private readonly IDownloadHistoryRepository _repository;
        private readonly IHistoryService _historyService;

        public MusicDownloadHistoryHandler(IDownloadHistoryRepository repository, IHistoryService historyService)
        {
            _repository = repository;
            _historyService = historyService;
        }

        public void Handle(TrackImportedEvent message)
        {
            if (!message.NewDownload)
            {
                return;
            }

            var downloadId = message.DownloadId;

            // Try to find the downloadId if the user used manual import (from wanted: missing) or the
            // API to import and downloadId wasn't provided.
            if (downloadId.IsNullOrWhiteSpace())
            {
                downloadId = _historyService.FindDownloadId(message);
            }

            if (downloadId.IsNullOrWhiteSpace())
            {
                return;
            }

            var history = new DownloadHistory
            {
                MediaKind = MediaKind.Music,
                EventType = DownloadHistoryEventType.FileImported,
                ParentId = message.ImportedTrack.Artist.Value.Id,
                DownloadId = downloadId,
                SourceTitle = message.TrackInfo.Path,
                Date = DateTime.UtcNow
            };

            // A manual import has no download client item, so the client details can be missing even
            // when FindDownloadId matched the file to a grab.
            var client = message.DownloadClientInfo;

            if (client != null)
            {
                history.Protocol = client.Protocol;
                history.DownloadClientId = client.Id;
                history.Data.Add("DownloadClient", client.Type);
                history.Data.Add("DownloadClientName", client.Name);
            }

            history.Data.Add("SourcePath", message.TrackInfo.Path);
            history.Data.Add("DestinationPath", message.ImportedTrack.Path);

            _repository.Insert(history);
        }

        public void Handle(ArtistsDeletedEvent message)
        {
            _repository.DeleteByParentIds(MediaKind.Music, message.Artists.Select(a => a.Id).ToList());
        }
    }
}
