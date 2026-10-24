using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.Parser;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Test.MusicTests
{
    // Fully populated Music entities, so a round trip through the database proves every column.
    internal static class MusicTestData
    {
        // The clean name the artist service stores and looks up: the real parser's CleanArtistName.
        public static string Clean(string name)
        {
            return name.CleanArtistName();
        }

        public static List<AudioQualityProfileItem> DefaultQualities(params AudioQuality[] allowed)
        {
            return AudioQuality.All
                .Select(q => new AudioQualityProfileItem { Quality = q, Allowed = allowed.Contains(q) })
                .ToList();
        }

        public static AudioQualityProfile NewQualityProfile(string name = "TestProfile", params AudioQuality[] allowed)
        {
            allowed = allowed.Length == 0 ? new[] { AudioQuality.FLAC, AudioQuality.MP3_320 } : allowed;

            return new AudioQualityProfile
            {
                Name = name,
                UpgradeAllowed = true,
                Cutoff = allowed[0].Id,
                MinFormatScore = 3,
                CutoffFormatScore = 7,
                Items = DefaultQualities(allowed)
            };
        }

        public static MetadataProfile NewMetadataProfile(string name = "TestMetadataProfile")
        {
            return new MetadataProfile
            {
                Name = name,
                PrimaryAlbumTypes = PrimaryAlbumType.All.Select(t => new ProfilePrimaryAlbumTypeItem { PrimaryAlbumType = t, Allowed = t == PrimaryAlbumType.Album }).ToList(),
                SecondaryAlbumTypes = SecondaryAlbumType.All.Select(t => new ProfileSecondaryAlbumTypeItem { SecondaryAlbumType = t, Allowed = t == SecondaryAlbumType.Studio }).ToList(),
                ReleaseStatuses = ReleaseStatus.All.Select(t => new ProfileReleaseStatusItem { ReleaseStatus = t, Allowed = t == ReleaseStatus.Official }).ToList()
            };
        }

        public static ArtistMetadata NewMetadata(string name = "The Black Keys", string foreignId = "d15721d8-56b4-453d-b506-fc915b14cba2")
        {
            return new ArtistMetadata
            {
                ForeignArtistId = foreignId,
                OldForeignArtistIds = new List<string> { "6f2ed437-825c-4cea-bb58-bf7688c6317a" },
                Name = name,
                Aliases = new List<string> { "TBK" },
                Overview = "An American rock duo.",
                Disambiguation = "rock duo",
                Type = "Group",
                Status = ArtistStatusType.Continuing,
                Images = new List<MediaCover.MediaCover>
                {
                    new MediaCover.MediaCover(MediaCoverTypes.Poster, "https://img.example/poster.jpg"),
                    new MediaCover.MediaCover(MediaCoverTypes.Banner, "https://img.example/banner.jpg")
                },
                Links = new List<Links> { new Links { Name = "homepage", Url = "https://blackkeys.example" } },
                Genres = new List<string> { "Rock", "Blues" },
                Ratings = new Ratings { Votes = 40, Value = 8.5m },
                Members = new List<Member> { new Member { Name = "Dan Auerbach", Instrument = "Guitar" } }
            };
        }

        public static Artist NewArtist(int metadataId, int qualityProfileId = 1, int metadataProfileId = 1, string name = "The Black Keys")
        {
            return new Artist
            {
                ArtistMetadataId = metadataId,
                CleanName = Clean(name),
                SortName = name.ToLowerInvariant(),
                Monitored = true,
                MonitorNewItems = NewItemMonitorTypes.New,
                LastInfoSync = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
                Path = "/data/music/" + name,
                Added = new DateTime(2026, 9, 1, 8, 30, 0, DateTimeKind.Utc),
                QualityProfileId = qualityProfileId,
                MetadataProfileId = metadataProfileId,
                Tags = new HashSet<int> { 3, 5 },
                AddOptions = new AddArtistOptions
                {
                    Monitor = MonitorTypes.Future,
                    AlbumsToMonitor = new List<string> { "album-1" },
                    Monitored = true,
                    SearchForMissingAlbums = true
                }
            };
        }

        public static Album NewAlbum(int artistMetadataId, string title = "Brothers", string foreignId = "album-1")
        {
            return new Album
            {
                ArtistMetadataId = artistMetadataId,
                ForeignAlbumId = foreignId,
                OldForeignAlbumIds = new List<string> { "old-album-1" },
                Title = title,
                Overview = "The sixth studio album.",
                Disambiguation = "deluxe",
                ReleaseDate = new DateTime(2010, 5, 18, 0, 0, 0, DateTimeKind.Utc),
                Images = new List<MediaCover.MediaCover> { new MediaCover.MediaCover(MediaCoverTypes.Cover, "https://img.example/cover.jpg") },
                Links = new List<Links> { new Links { Name = "wiki", Url = "https://wiki.example/brothers" } },
                Genres = new List<string> { "Rock" },
                AlbumType = "Album",
                SecondaryTypes = new List<SecondaryAlbumType> { SecondaryAlbumType.Studio, SecondaryAlbumType.Live },
                Ratings = new Ratings { Votes = 12, Value = 9.1m },
                LastSearchTime = new DateTime(2026, 10, 2, 3, 4, 5, DateTimeKind.Utc),
                CleanTitle = Clean(title),
                ProfileId = 0,
                Monitored = true,
                AnyReleaseOk = false,
                LastInfoSync = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
                Added = new DateTime(2026, 9, 1, 8, 30, 0, DateTimeKind.Utc),
                AddOptions = new AddAlbumOptions { AddType = AlbumAddType.Manual, SearchForNewAlbum = true }
            };
        }

        public static AlbumRelease NewRelease(int albumId, string foreignId = "release-1", bool monitored = true)
        {
            return new AlbumRelease
            {
                AlbumId = albumId,
                ForeignReleaseId = foreignId,
                OldForeignReleaseIds = new List<string> { "old-" + foreignId },
                Title = "Brothers",
                Status = "Official",
                Duration = 3_100_000,
                Label = new List<string> { "Nonesuch" },
                Disambiguation = "US",
                Country = new List<string> { "US", "CA" },
                ReleaseDate = new DateTime(2010, 5, 18, 0, 0, 0, DateTimeKind.Utc),
                Media = new List<Medium> { new Medium { Number = 1, Name = "Disc 1", Format = "CD" } },
                TrackCount = 15,
                Monitored = monitored
            };
        }

        public static Track NewTrack(int releaseId, int artistMetadataId, string foreignId = "track-1", int trackFileId = 0)
        {
            return new Track
            {
                ForeignTrackId = foreignId,
                OldForeignTrackIds = new List<string> { "old-" + foreignId },
                ForeignRecordingId = "rec-" + foreignId,
                OldForeignRecordingIds = new List<string> { "old-rec-" + foreignId },
                AlbumReleaseId = releaseId,
                ArtistMetadataId = artistMetadataId,
                TrackNumber = "A1",
                AbsoluteTrackNumber = 1,
                Title = "Everlasting Light",
                Duration = 235_000,
                Explicit = true,
                Ratings = new Ratings { Votes = 3, Value = 7m },
                MediumNumber = 1,
                TrackFileId = trackFileId
            };
        }

        public static TrackFile NewTrackFile(int albumId, string path = "/data/music/The Black Keys/Brothers/01.flac", AudioQuality quality = null)
        {
            return new TrackFile
            {
                AlbumId = albumId,
                Path = path,
                Size = 31_457_280,
                Modified = new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc),
                DateAdded = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc),
                OriginalFilePath = "Brothers/01.flac",
                SceneName = "The.Black.Keys-Brothers-2010-FLAC-GRP",
                ReleaseGroup = "GRP",
                Quality = new AudioQualityModel(quality ?? AudioQuality.FLAC, new Revision(2, 1, true)),
                IndexerFlags = IndexerFlags.G_Freeleech,
                MediaInfo = new MediaInfoModel { AudioFormat = "FLAC", AudioBitrate = 900_000, AudioChannels = 2, AudioBits = 16, AudioSampleRate = 44_100 }
            };
        }
    }
}
