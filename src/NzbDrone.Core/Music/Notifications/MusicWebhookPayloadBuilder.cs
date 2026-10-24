using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.Covers;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.Webhook;
using NzbDrone.Core.Tags;

namespace NzbDrone.Core.Music.Notifications
{
    // The Music module's webhook payloads, in Lidarr's upstream shape so existing receivers keep working.
    // Lidarr's events with no neutral counterpart yet (album delete, download failure, import failure,
    // retag) are not sent; see MusicNotificationHandler.
    public class MusicWebhookPayloadBuilder : IWebhookPayloadBuilder
    {
        private readonly ITagRepository _tagRepository;
        private readonly IMapCoversToLocal _mediaCoverService;

        public MusicWebhookPayloadBuilder(ITagRepository tagRepository, IMapCoversToLocal mediaCoverService)
        {
            _tagRepository = tagRepository;
            _mediaCoverService = mediaCoverService;
        }

        public MediaKind Kind => MediaKind.Music;

        public WebhookPayload BuildGrab(GrabMessage message)
        {
            var data = (MusicNotificationData)message.Data;
            var remoteAlbum = data.RemoteAlbum;

            return new WebhookGrabPayload
            {
                EventType = WebhookEventType.Grab,
                Artist = GetArtist(data.Artist),
                Albums = remoteAlbum.Albums.Select(GetAlbum).ToList(),
                Release = new WebhookRelease(remoteAlbum.ParsedAlbumInfo.Quality, remoteAlbum),
                DownloadClient = message.DownloadClientName,
                DownloadClientType = message.DownloadClientType,
                DownloadId = message.DownloadId
            };
        }

        public WebhookPayload BuildDownload(DownloadMessage message)
        {
            var data = (MusicNotificationData)message.Data;
            var trackFiles = data.TrackFiles;

            var payload = new WebhookImportPayload
            {
                EventType = WebhookEventType.Download,
                Artist = GetArtist(data.Artist),
                Album = GetAlbum(data.Album),
                Tracks = trackFiles.SelectMany(x => x.Tracks.Value.Select(y => new WebhookTrack(y))).ToList(),
                TrackFiles = trackFiles.ConvertAll(x => new WebhookTrackFile(x)),
                IsUpgrade = data.OldTrackFiles.Any(),
                DownloadClient = message.DownloadClientInfo?.Name,
                DownloadClientType = message.DownloadClientInfo?.Type,
                DownloadId = message.DownloadId
            };

            if (data.OldTrackFiles.Any())
            {
                payload.DeletedFiles = data.OldTrackFiles.ConvertAll(x => new WebhookTrackFile(x));
            }

            return payload;
        }

        public WebhookPayload BuildRename(RenameMessage message)
        {
            var data = (MusicNotificationData)message.Data;

            return new WebhookRenamePayload
            {
                EventType = WebhookEventType.Rename,
                Artist = GetArtist(data.Artist),
                RenamedTrackFiles = data.RenamedFiles.ConvertAll(x => new WebhookRenamedTrackFile(x))
            };
        }

        public WebhookPayload BuildMediaAdded(MediaAddedMessage message)
        {
            var data = (MusicNotificationData)message.Data;

            return new WebhookArtistAddPayload
            {
                EventType = WebhookEventType.ArtistAdd,
                Artist = GetArtist(data.Artist)
            };
        }

        public WebhookPayload BuildMediaFileDelete(MediaFileDeleteMessage message)
        {
            throw new NotSupportedException("Lidarr has no track file delete notification, so the Music module never sends one.");
        }

        public WebhookPayload BuildMediaDelete(MediaDeleteMessage message)
        {
            var data = (MusicNotificationData)message.Data;

            return new WebhookArtistDeletePayload
            {
                EventType = WebhookEventType.ArtistDelete,
                Artist = GetArtist(data.Artist),
                DeletedFiles = message.DeletedFiles
            };
        }

        public WebhookPayload BuildManualInteraction(ManualInteractionRequiredMessage message)
        {
            var data = (MusicNotificationData)message.Data;
            var remoteAlbum = data.RemoteAlbum;
            var downloadItem = message.TrackedDownload.DownloadItem;
            var quality = remoteAlbum?.ParsedAlbumInfo?.Quality;

            return new WebhookManualInteractionPayload
            {
                EventType = WebhookEventType.ManualInteractionRequired,
                Artist = GetArtist(data.Artist),
                Albums = remoteAlbum?.Albums.Select(GetAlbum).ToList() ?? new List<WebhookAlbum>(),
                DownloadInfo = quality == null ? null : new WebhookDownloadClientItem(quality.ToDisplayModel(), downloadItem),
                DownloadClient = message.DownloadClientInfo?.Name,
                DownloadClientType = message.DownloadClientInfo?.Type,
                DownloadId = message.DownloadId,
                DownloadStatus = message.TrackedDownload.Status.ToString(),
                DownloadStatusMessages = message.TrackedDownload.StatusMessages.Select(x => new WebhookDownloadStatusMessage(x)).ToList()
            };
        }

        public WebhookPayload BuildImportComplete(ImportCompleteMessage message)
        {
            throw new NotSupportedException("Lidarr notifies once per imported album, so the Music module never sends an import complete message.");
        }

        public WebhookPayload BuildTest()
        {
            return new WebhookGrabPayload
            {
                EventType = WebhookEventType.Test,
                Artist = new WebhookArtist
                {
                    Id = 1,
                    Name = "Test Name",
                    Path = "C:\\testpath",
                    MBId = "aaaaa-aaa-aaaa-aaaaaa",
                    Tags = new List<string> { "test-tag" }
                },
                Albums = new List<WebhookAlbum>
                {
                    new()
                    {
                        Id = 123,
                        Title = "Test title"
                    }
                }
            };
        }

        private WebhookArtist GetArtist(Artist artist)
        {
            if (artist?.Metadata?.Value == null)
            {
                return null;
            }

            _mediaCoverService.ConvertToLocalUrls(artist.Id, MediaCoverEntity.Artist, artist.Metadata.Value.Images);

            return new WebhookArtist(artist, GetTagLabels(artist));
        }

        private WebhookAlbum GetAlbum(Album album)
        {
            if (album == null)
            {
                return null;
            }

            _mediaCoverService.ConvertToLocalUrls(album.Id, MediaCoverEntity.Album, album.Images);

            return new WebhookAlbum(album);
        }

        private List<string> GetTagLabels(Artist artist)
        {
            return _tagRepository.GetTags(artist.Tags)
                .Select(t => t.Label)
                .Where(l => l.IsNotNullOrWhiteSpace())
                .OrderBy(l => l)
                .ToList();
        }
    }
}
