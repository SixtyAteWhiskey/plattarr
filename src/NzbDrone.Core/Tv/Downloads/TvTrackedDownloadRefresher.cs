using System;
using System.Linq;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Tv.Events;
using NzbDrone.Core.Tv.MediaFiles.Events;

namespace NzbDrone.Core.Tv.Downloads
{
    // Keeps the tracked downloads in step with the series library, as Sonarr's TrackedDownloadService
    // did: when a series is added, edited or deleted, or episodes are removed in a refresh, the
    // downloads that matched (or failed to match) are matched again; when an episode file is
    // imported, the download monitor is asked to refresh.
    public class TvTrackedDownloadRefresher : IHandle<SeriesAddedEvent>,
                                              IHandle<SeriesEditedEvent>,
                                              IHandle<SeriesBulkEditedEvent>,
                                              IHandle<SeriesDeletedEvent>,
                                              IHandle<EpisodeInfoRefreshedEvent>,
                                              IHandle<EpisodeImportedEvent>
    {
        private readonly ITrackedDownloadService _trackedDownloadService;
        private readonly IEventAggregator _eventAggregator;

        public TvTrackedDownloadRefresher(ITrackedDownloadService trackedDownloadService, IEventAggregator eventAggregator)
        {
            _trackedDownloadService = trackedDownloadService;
            _eventAggregator = eventAggregator;
        }

        public void Handle(EpisodeImportedEvent message)
        {
            _eventAggregator.PublishEvent(new RefreshDownloadsRequestedEvent());
        }

        public void Handle(SeriesAddedEvent message)
        {
            Rematch(t =>
                t.GetSeries() == null ||
                message.Series?.TvdbId == t.GetSeries().TvdbId);
        }

        public void Handle(SeriesEditedEvent message)
        {
            Rematch(t =>
                t.GetSeries() != null &&
                (t.GetSeries().Id == message.Series?.Id || t.GetSeries().TvdbId == message.Series?.TvdbId));
        }

        public void Handle(SeriesBulkEditedEvent message)
        {
            Rematch(t =>
                t.GetSeries() != null &&
                message.Series.Any(s => s.Id == t.GetSeries().Id || s.TvdbId == t.GetSeries().TvdbId));
        }

        public void Handle(SeriesDeletedEvent message)
        {
            Rematch(t =>
                t.GetSeries() != null &&
                message.Series.Any(s => s.Id == t.GetSeries().Id || s.TvdbId == t.GetSeries().TvdbId));
        }

        public void Handle(EpisodeInfoRefreshedEvent message)
        {
            Rematch(t =>
                t.GetRemoteEpisode()?.Episodes != null &&
                message.Removed.Any(r => t.GetRemoteEpisode().Episodes.Any(e => e.Id == r.Id)));
        }

        private void Rematch(Func<TrackedDownload, bool> affected)
        {
            var trackedDownloads = _trackedDownloadService.GetTrackedDownloads()
                .Where(t => t.Kind == MediaKind.Tv && affected(t))
                .ToList();

            _trackedDownloadService.Rematch(trackedDownloads);
        }
    }
}
