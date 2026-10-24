using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.RootFolders;
using NzbDrone.Core.Notifications;

namespace NzbDrone.Core.Music.Notifications
{
    // Contract 7. Turns an artist, its albums, a track file and a release into the neutral values
    // notification providers show. The item label is the album titles ("Album" or "One + Two"), so a
    // provider that appends it reads like Lidarr's "Artist - Album" message.
    public class MusicActivityRenderer : IActivityRenderer
    {
        private readonly IArtistService _artistService;
        private readonly IAlbumService _albumService;
        private readonly IRootFolderService _rootFolderService;

        public MusicActivityRenderer(IArtistService artistService,
                                     IAlbumService albumService,
                                     IRootFolderService rootFolderService)
        {
            _artistService = artistService;
            _albumService = albumService;
            _rootFolderService = rootFolderService;
        }

        public MediaKind Kind => MediaKind.Music;

        public ActivitySubject Describe(int parentId, IReadOnlyList<int> itemIds)
        {
            var artist = _artistService.GetArtist(parentId);
            var albums = itemIds == null || itemIds.Count == 0
                ? new List<Album>()
                : _albumService.GetAlbums(itemIds);

            return Describe(artist, albums);
        }

        public static string DescribeAlbums(IReadOnlyList<Album> albums)
        {
            return albums == null || albums.Count == 0
                ? string.Empty
                : string.Join(" + ", albums.Select(a => a.Title));
        }

        public ActivitySubject Describe(Artist artist, IReadOnlyList<Album> albums = null)
        {
            if (artist == null)
            {
                return null;
            }

            var metadata = artist.Metadata.Value;

            var subject = new ActivitySubject
            {
                Kind = MediaKind.Music,
                KindLabel = "Artist",
                ItemKindLabel = "Album",
                OmitYearFromTitle = true,
                ParentId = artist.Id,
                ParentTitle = metadata.Name,
                ItemLabel = DescribeAlbums(albums),
                ParentPath = artist.Path,
                Overview = metadata.Overview,
                Genres = metadata.Genres ?? new List<string>(),
                Rating = metadata.Ratings?.Value,
                PosterUrl = metadata.Images?.FirstOrDefault(x => x.CoverType == MediaCoverTypes.Poster)?.RemoteUrl,
                FanartUrl = metadata.Images?.FirstOrDefault(x => x.CoverType == MediaCoverTypes.Fanart)?.RemoteUrl,
                Tags = artist.Tags ?? new HashSet<int>()
            };

            subject.ResolveRootFolderWith(() => artist.Path.IsNotNullOrWhiteSpace() ? _rootFolderService.GetBestRootFolderPath(artist.Path) : null);

            if (metadata.ForeignArtistId.IsNotNullOrWhiteSpace())
            {
                // MusicBrainz has no MetadataLinkType (the setting lists video databases), so the link is
                // untyped: it renders but cannot be picked in a provider's MetadataLinks.
                var musicBrainz = new NotificationMetadataLink(null, "MusicBrainz", $"https://musicbrainz.org/artist/{metadata.ForeignArtistId}");

                subject.ExternalIds[ExternalIdTypes.MusicBrainz] = metadata.ForeignArtistId;
                subject.Links.Add(musicBrainz);
                subject.MetadataUrl = musicBrainz.Link;
            }

            return subject;
        }

        public NotificationFile DescribeFile(Artist artist, TrackFile trackFile)
        {
            if (trackFile == null)
            {
                return null;
            }

            var mediaInfo = trackFile.MediaInfo;

            return new NotificationFile
            {
                Id = trackFile.Id,
                Path = trackFile.Path,
                RelativePath = artist?.Path.IsNotNullOrWhiteSpace() == true ? artist.Path.GetRelativePath(trackFile.Path) : trackFile.Path,
                Size = trackFile.Size,
                DateAdded = trackFile.DateAdded,
                SceneName = trackFile.SceneName,
                ReleaseGroup = trackFile.ReleaseGroup,
                QualityName = trackFile.Quality?.Quality?.Name,
                MediaInfo = mediaInfo == null ? null : new NotificationMediaInfo
                {
                    AudioCodec = mediaInfo.AudioFormat,
                    AudioChannels = mediaInfo.AudioChannels
                }
            };
        }

        public NotificationRelease DescribeRelease(RemoteAlbum remoteAlbum)
        {
            return new NotificationRelease
            {
                Title = remoteAlbum.Release.Title,
                Indexer = remoteAlbum.Release.Indexer,
                Size = remoteAlbum.Release.Size.GetValueOrDefault(),
                ReleaseGroup = remoteAlbum.ParsedAlbumInfo.ReleaseGroup,
                QualityName = remoteAlbum.ParsedAlbumInfo.Quality.Quality.Name,
                CustomFormats = remoteAlbum.CustomFormats?.Select(c => c.Name).ToList() ?? new List<string>(),
                CustomFormatScore = remoteAlbum.CustomFormatScore
            };
        }
    }
}
