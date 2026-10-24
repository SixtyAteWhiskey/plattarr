using System;
using System.Linq;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.Events;
using NzbDrone.Core.Music.MediaFiles.Events;

namespace NzbDrone.Core.Music.Downloads
{
    // Keeps the tracked downloads in step with the artist library, as Lidarr's TrackedDownloadService
    // did: when an artist is added, edited or deleted, or albums are removed in a refresh, the
    // downloads that matched (or failed to match) are matched again; when a track is imported, the
    // download monitor is asked to refresh.
    public class MusicTrackedDownloadRefresher : IHandle<ArtistAddedEvent>,
                                                 IHandle<ArtistEditedEvent>,
                                                 IHandle<ArtistsDeletedEvent>,
                                                 IHandle<AlbumDeletedEvent>,
                                                 IHandle<AlbumInfoRefreshedEvent>,
                                                 IHandle<TrackImportedEvent>
    {
        private readonly ITrackedDownloadService _trackedDownloadService;
        private readonly IEventAggregator _eventAggregator;

        public MusicTrackedDownloadRefresher(ITrackedDownloadService trackedDownloadService, IEventAggregator eventAggregator)
        {
            _trackedDownloadService = trackedDownloadService;
            _eventAggregator = eventAggregator;
        }

        public void Handle(TrackImportedEvent message)
        {
            _eventAggregator.PublishEvent(new RefreshDownloadsRequestedEvent());
        }

        public void Handle(ArtistAddedEvent message)
        {
            Rematch(t =>
                t.GetArtist() == null ||
                message.Artist?.ForeignArtistId == t.GetArtist().ForeignArtistId);
        }

        public void Handle(ArtistEditedEvent message)
        {
            Rematch(t =>
                t.GetArtist() != null &&
                (t.GetArtist().Id == message.Artist?.Id || t.GetArtist().ForeignArtistId == message.Artist?.ForeignArtistId));
        }

        public void Handle(ArtistsDeletedEvent message)
        {
            Rematch(t =>
                t.GetArtist() != null &&
                message.Artists.Any(a => a.Id == t.GetArtist().Id || a.ForeignArtistId == t.GetArtist().ForeignArtistId));
        }

        public void Handle(AlbumDeletedEvent message)
        {
            Rematch(t =>
                t.GetRemoteAlbum()?.Albums != null &&
                t.GetRemoteAlbum().Albums.Any(a => a.Id == message.Album.Id));
        }

        public void Handle(AlbumInfoRefreshedEvent message)
        {
            Rematch(t =>
                t.GetRemoteAlbum()?.Albums != null &&
                message.Removed.Any(r => t.GetRemoteAlbum().Albums.Any(a => a.Id == r.Id)));
        }

        private void Rematch(Func<TrackedDownload, bool> affected)
        {
            var trackedDownloads = _trackedDownloadService.GetTrackedDownloads()
                .Where(t => t.Kind == MediaKind.Music && affected(t))
                .ToList();

            _trackedDownloadService.Rematch(trackedDownloads);
        }
    }
}
