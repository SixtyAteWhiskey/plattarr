using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.Events;
using NzbDrone.Core.Music.MediaFiles.Events;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Music.Notifications
{
    // Contract 7. Music events become neutral notification messages for the core dispatcher. The message
    // text is Lidarr's own ("Artist - Album - [FLAC]", "Artist - Album (12 Tracks Imported)").
    //
    // Lidarr also notifies on a deleted album, a failed download, an incomplete import and a retag. The
    // neutral contract has no event for them yet (a provider's event flags are columns on the notification
    // definition), so they arrive with the API and UI (step 7), as Sonarr's On Import Complete did for TV.
    // A file delete is not notified at all: Lidarr never did.
    public class MusicNotificationHandler
        : IHandle<ReleaseGrabbedEvent>,
          IHandle<AlbumImportedEvent>,
          IHandle<ArtistAddCompletedEvent>,
          IHandle<ArtistRenamedEvent>,
          IHandle<ArtistsDeletedEvent>,
          IHandle<ManualInteractionRequiredEvent>
    {
        private readonly INotificationDispatcher _dispatcher;
        private readonly MusicActivityRenderer _renderer;

        public MusicNotificationHandler(INotificationDispatcher dispatcher, MusicActivityRenderer renderer)
        {
            _dispatcher = dispatcher;
            _renderer = renderer;
        }

        public static string GetMessage(Artist artist, IReadOnlyList<Album> albums, AudioQualityModel quality)
        {
            var qualityString = quality.Quality.ToString();

            if (quality.Revision.Version > 1)
            {
                qualityString += " Proper";
            }

            var albumTitles = string.Join(" + ", albums.Select(e => e.Title));

            return $"{artist.Metadata.Value.Name} - {albumTitles} - [{qualityString}]";
        }

        public static string GetAlbumDownloadMessage(Artist artist, Album album, int trackCount)
        {
            return $"{artist.Metadata.Value.Name} - {album.Title} ({trackCount} Tracks Imported)";
        }

        public void Handle(ReleaseGrabbedEvent message)
        {
            if (message.Release.Kind != MediaKind.Music)
            {
                return;
            }

            if (!_dispatcher.AnyEnabled(NotificationEvent.Grab))
            {
                return;
            }

            var remoteAlbum = message.GetRemoteAlbum();
            var artist = remoteAlbum.Artist;
            var quality = remoteAlbum.ParsedAlbumInfo.Quality;

            _dispatcher.Grab(new GrabMessage
            {
                Message = GetMessage(artist, remoteAlbum.Albums, quality),
                Subject = _renderer.Describe(artist, remoteAlbum.Albums),
                Data = new MusicNotificationData { Artist = artist, RemoteAlbum = remoteAlbum },
                QualityName = quality.Quality.Name,
                Release = _renderer.DescribeRelease(remoteAlbum),
                DownloadClientType = message.DownloadClient,
                DownloadClientName = message.DownloadClientName,
                DownloadId = message.DownloadId
            });
        }

        public void Handle(AlbumImportedEvent message)
        {
            if (!message.NewDownload || !_dispatcher.AnyEnabled(NotificationEvent.Download))
            {
                return;
            }

            var artist = message.Artist;
            var oldFiles = message.OldFiles ?? new List<MediaFiles.TrackFile>();
            var albums = new List<Album> { message.Album };

            _dispatcher.Download(new DownloadMessage
            {
                Message = GetAlbumDownloadMessage(artist, message.Album, message.ImportedTracks.Count),
                Subject = _renderer.Describe(artist, albums),
                Data = new MusicNotificationData
                {
                    Artist = artist,
                    Album = message.Album,
                    AlbumRelease = message.AlbumRelease,
                    TrackFiles = message.ImportedTracks,
                    OldTrackFiles = oldFiles
                },
                File = _renderer.DescribeFile(artist, message.ImportedTracks.FirstOrDefault()),
                OldFiles = oldFiles.Select(f => _renderer.DescribeFile(artist, f)).ToList(),
                SourcePath = message.ImportedTracks.FirstOrDefault()?.OriginalFilePath,
                DownloadClientInfo = message.DownloadClientInfo,
                DownloadId = message.DownloadId
            });
        }

        public void Handle(ArtistAddCompletedEvent message)
        {
            if (!_dispatcher.AnyEnabled(NotificationEvent.MediaAdded))
            {
                return;
            }

            _dispatcher.MediaAdded(new MediaAddedMessage
            {
                Subject = _renderer.Describe(message.Artist),
                Data = new MusicNotificationData { Artist = message.Artist }
            });
        }

        public void Handle(ArtistRenamedEvent message)
        {
            if (!_dispatcher.AnyEnabled(NotificationEvent.Rename))
            {
                return;
            }

            var artist = message.Artist;

            _dispatcher.Rename(new RenameMessage
            {
                Subject = _renderer.Describe(artist),
                Data = new MusicNotificationData { Artist = artist, RenamedFiles = message.RenamedFiles },
                Files = message.RenamedFiles.ConvertAll(f => new NotificationRenamedFile
                {
                    File = _renderer.DescribeFile(artist, f.TrackFile),
                    PreviousPath = f.PreviousPath,
                    PreviousRelativePath = artist.Path.IsNotNullOrWhiteSpace() ? artist.Path.GetRelativePath(f.PreviousPath) : f.PreviousPath
                })
            });
        }

        public void Handle(ArtistsDeletedEvent message)
        {
            if (!_dispatcher.AnyEnabled(NotificationEvent.MediaDelete))
            {
                return;
            }

            foreach (var artist in message.Artists)
            {
                _dispatcher.MediaDelete(new MediaDeleteMessage(
                    _renderer.Describe(artist),
                    new MusicNotificationData { Artist = artist },
                    message.DeleteFiles));
            }
        }

        public void Handle(ManualInteractionRequiredEvent message)
        {
            if (message.Data is not MusicNotificationData published || !_dispatcher.AnyEnabled(NotificationEvent.ManualInteractionRequired))
            {
                return;
            }

            var trackedDownload = message.TrackedDownload;
            var remoteAlbum = published.RemoteAlbum;
            var artist = published.Artist ?? remoteAlbum?.Artist;
            var text = "";

            if (artist != null && remoteAlbum?.ParsedAlbumInfo != null)
            {
                text = GetMessage(artist, remoteAlbum.Albums, remoteAlbum.ParsedAlbumInfo.Quality);
            }

            if (text.IsNullOrWhiteSpace() && trackedDownload.DownloadItem != null)
            {
                text = trackedDownload.DownloadItem.Title;
            }

            if (text.IsNullOrWhiteSpace())
            {
                return;
            }

            _dispatcher.ManualInteractionRequired(new ManualInteractionRequiredMessage
            {
                Message = text,
                Subject = _renderer.Describe(artist, remoteAlbum?.Albums),
                Data = new MusicNotificationData { Artist = artist, RemoteAlbum = remoteAlbum },
                TrackedDownload = trackedDownload,
                QualityName = remoteAlbum?.ParsedAlbumInfo?.Quality?.Quality?.Name,
                ReleaseGroup = remoteAlbum?.ParsedAlbumInfo?.ReleaseGroup,
                DownloadClientInfo = trackedDownload.DownloadItem?.DownloadClientInfo,
                DownloadId = trackedDownload.DownloadItem?.DownloadId,
                Release = remoteAlbum == null ? null : _renderer.DescribeRelease(remoteAlbum)
            });
        }
    }
}
