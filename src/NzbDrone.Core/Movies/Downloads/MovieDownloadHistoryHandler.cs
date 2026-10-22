using System;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.Events;
using NzbDrone.Core.Movies.History;
using NzbDrone.Core.Movies.MediaFiles.Events;

namespace NzbDrone.Core.Movies.Downloads
{
    // The movie events that write download history: a file imported outside the completed
    // download path, and movies deleted. The core DownloadHistoryService handles the events
    // that every media type raises.
    public class MovieDownloadHistoryHandler : IHandle<MovieFileImportedEvent>,
                                               IHandle<MoviesDeletedEvent>
    {
        private readonly IDownloadHistoryRepository _repository;
        private readonly IHistoryService _historyService;

        public MovieDownloadHistoryHandler(IDownloadHistoryRepository repository, IHistoryService historyService)
        {
            _repository = repository;
            _historyService = historyService;
        }

        public void Handle(MovieFileImportedEvent message)
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
                MediaKind = MediaKind.Movies,
                EventType = DownloadHistoryEventType.FileImported,
                ParentId = message.ImportedMovie.MovieId,
                DownloadId = downloadId,
                SourceTitle = message.MovieInfo.Path,
                Date = DateTime.UtcNow,
                Protocol = message.DownloadClientInfo.Protocol,
                DownloadClientId = message.DownloadClientInfo.Id
            };

            history.Data.Add("DownloadClient", message.DownloadClientInfo.Type);
            history.Data.Add("DownloadClientName", message.DownloadClientInfo.Name);

            _repository.Insert(history);
        }

        public void Handle(MoviesDeletedEvent message)
        {
            _repository.DeleteByParentIds(MediaKind.Movies, message.Movies.Select(m => m.Id).ToList());
        }
    }
}
