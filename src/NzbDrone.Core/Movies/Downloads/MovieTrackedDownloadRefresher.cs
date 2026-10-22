using System;
using System.Linq;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.Events;
using NzbDrone.Core.Movies.MediaFiles.Events;

namespace NzbDrone.Core.Movies.Downloads
{
    // Keeps the tracked downloads in step with the movie library: when a movie is added, edited or
    // deleted, the downloads that matched (or failed to match) a movie are matched again; when a movie
    // file is imported, the download monitor is asked to refresh.
    public class MovieTrackedDownloadRefresher : IHandle<MovieAddedEvent>,
                                                 IHandle<MovieEditedEvent>,
                                                 IHandle<MoviesBulkEditedEvent>,
                                                 IHandle<MoviesDeletedEvent>,
                                                 IHandle<MovieFileImportedEvent>
    {
        private readonly ITrackedDownloadService _trackedDownloadService;
        private readonly IEventAggregator _eventAggregator;

        public MovieTrackedDownloadRefresher(ITrackedDownloadService trackedDownloadService, IEventAggregator eventAggregator)
        {
            _trackedDownloadService = trackedDownloadService;
            _eventAggregator = eventAggregator;
        }

        public void Handle(MovieFileImportedEvent message)
        {
            _eventAggregator.PublishEvent(new RefreshDownloadsRequestedEvent());
        }

        public void Handle(MovieAddedEvent message)
        {
            Rematch(t =>
                t.GetMovie() == null ||
                message.Movie?.TmdbId == t.GetMovie().TmdbId);
        }

        public void Handle(MovieEditedEvent message)
        {
            Rematch(t =>
                t.GetMovie() != null &&
                (t.GetMovie().Id == message.Movie?.Id || t.GetMovie().TmdbId == message.Movie?.TmdbId));
        }

        public void Handle(MoviesBulkEditedEvent message)
        {
            Rematch(t =>
                t.GetMovie() != null &&
                message.Movies.Any(m => m.Id == t.GetMovie().Id || m.TmdbId == t.GetMovie().TmdbId));
        }

        public void Handle(MoviesDeletedEvent message)
        {
            Rematch(t =>
                t.GetMovie() != null &&
                message.Movies.Any(m => m.Id == t.GetMovie().Id || m.TmdbId == t.GetMovie().TmdbId));
        }

        private void Rematch(Func<TrackedDownload, bool> affected)
        {
            var trackedDownloads = _trackedDownloadService.GetTrackedDownloads()
                .Where(t => t.Kind == MediaKind.Movies && affected(t))
                .ToList();

            _trackedDownloadService.Rematch(trackedDownloads);
        }
    }
}
