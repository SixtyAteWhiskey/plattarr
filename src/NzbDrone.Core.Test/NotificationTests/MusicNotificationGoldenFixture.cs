using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Processes;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Events;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.MediaFiles.Events;
using NzbDrone.Core.Music.Notifications;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.RootFolders;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.CustomScript;
using NzbDrone.Core.Notifications.Discord;
using NzbDrone.Core.Notifications.Discord.Payloads;
using NzbDrone.Core.Notifications.Webhook;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.NotificationTests
{
    // Regression net for the Music half of contract 7, as TvNotificationGoldenFixture is for TV. A fixed
    // The Black Keys library goes through the real MusicNotificationHandler and MusicActivityRenderer into
    // one provider, and what the provider sends is compared with Files/Notifications/Golden/music-*. The
    // webhook and script files are Lidarr's payload shapes and variable names. After a reviewed change,
    // set GOLDEN_OUT=<dir>, run the fixture and copy the files from <dir>.
    internal static class MusicGoldenData
    {
        public static Artist Artist()
        {
            var metadata = new ArtistMetadata
            {
                Id = 1,
                ForeignArtistId = "d15721d8-56b4-453d-b506-fc915b14cba2",
                Name = "The Black Keys",
                Overview = "An American rock duo.",
                Type = "Group",
                Genres = new List<string> { "Rock", "Blues" },
                Ratings = new Ratings { Votes = 40, Value = 8.5m },
                Images = new List<MediaCover.MediaCover>
                {
                    new MediaCover.MediaCover(MediaCoverTypes.Poster, "https://img.example/poster.jpg"),
                    new MediaCover.MediaCover(MediaCoverTypes.Fanart, "https://img.example/fanart.jpg")
                }
            };

            return new Artist
            {
                Id = 1,
                ArtistMetadataId = 1,
                Metadata = new LazyLoaded<ArtistMetadata>(metadata),
                Path = "/music/The Black Keys",
                Tags = new HashSet<int> { 1, 2 },
                Added = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc)
            };
        }

        public static Album Album(int id = 21, string title = "Brothers", string foreignId = "a1b2c3d4-0001")
        {
            return new Album
            {
                Id = id,
                ArtistMetadataId = 1,
                ForeignAlbumId = foreignId,
                Title = title,
                Overview = "The sixth studio album.",
                ReleaseDate = new DateTime(2010, 5, 18, 0, 0, 0, DateTimeKind.Utc),
                AlbumType = "Album",
                Genres = new List<string> { "Rock" },
                Images = new List<MediaCover.MediaCover> { new MediaCover.MediaCover(MediaCoverTypes.Cover, "https://img.example/cover.jpg") }
            };
        }

        public static AlbumRelease Release(int albumId = 21)
        {
            return new AlbumRelease { Id = 31, AlbumId = albumId, ForeignReleaseId = "r1b2c3d4-0001", Title = "Brothers", Status = "Official" };
        }

        public static List<Track> Tracks()
        {
            return new List<Track>
            {
                new Track { Id = 41, AlbumReleaseId = 31, ForeignTrackId = "t-1", Title = "Everlasting Light", TrackNumber = "1", AbsoluteTrackNumber = 1, Duration = 235000, TrackFileId = 51 },
                new Track { Id = 42, AlbumReleaseId = 31, ForeignTrackId = "t-2", Title = "Next Girl", TrackNumber = "2", AbsoluteTrackNumber = 2, Duration = 190000, TrackFileId = 52 }
            };
        }

        public static TrackFile TrackFile(Artist artist, Album album, int id, string name, AudioQuality quality)
        {
            return new TrackFile
            {
                Id = id,
                AlbumId = album.Id,
                Path = $"{artist.Path}/Brothers/{name}",
                Size = 31457280,
                DateAdded = new DateTime(2026, 10, 2, 3, 4, 5, DateTimeKind.Utc),
                SceneName = "The.Black.Keys-Brothers-2010-FLAC-GRP",
                OriginalFilePath = $"Brothers/{name}",
                ReleaseGroup = "GRP",
                Quality = new AudioQualityModel(quality, new Revision(2)),
                IndexerFlags = IndexerFlags.G_Freeleech,
                MediaInfo = new MediaInfoModel { AudioFormat = "FLAC", AudioChannels = 2, AudioBits = 16, AudioSampleRate = 44100 },
                Artist = new LazyLoaded<Artist>(artist),
                Album = new LazyLoaded<Album>(album),
                Tracks = new LazyLoaded<List<Track>>(Tracks().Where(t => t.TrackFileId == id).ToList())
            };
        }

        public static RemoteAlbum Remote(Artist artist, List<Album> albums)
        {
            return new RemoteAlbum
            {
                Artist = artist,
                Albums = albums,
                ParsedAlbumInfo = new ParsedAlbumInfo
                {
                    ArtistName = "The Black Keys",
                    AlbumTitle = "Brothers",
                    Quality = new AudioQualityModel(AudioQuality.FLAC, new Revision(2)),
                    ReleaseGroup = "GRP",
                    ReleaseTitle = "The.Black.Keys-Brothers-2010-FLAC-GRP"
                },
                Release = new ReleaseInfo
                {
                    Title = "The.Black.Keys-Brothers-2010-FLAC-GRP",
                    Indexer = "Knaben",
                    Size = 314572800,
                    InfoUrl = "https://example/info",
                    PublishDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                CustomFormats = new List<CustomFormat> { new CustomFormat { Id = 1, Name = "Lossless" }, new CustomFormat { Id = 2, Name = "Vinyl" } },
                CustomFormatScore = 150
            };
        }

        public static TrackedDownload Tracked(RemoteAlbum remote)
        {
            return new TrackedDownload
            {
                Kind = MediaKind.Music,
                RemoteRelease = remote,
                DownloadItem = new DownloadClientItem
                {
                    DownloadId = "HASH123",
                    Title = "The.Black.Keys-Brothers-2010-FLAC-GRP",
                    TotalSize = 314572800,
                    DownloadClientInfo = GoldenData.ClientInfo()
                }
            };
        }

        public static MusicActivityRenderer Renderer()
        {
            return new MusicActivityRenderer(new Mock<IArtistService>().Object, new Mock<IAlbumService>().Object, new Mock<IRootFolderService>().Object);
        }

        // Publishes every Music event the handler turns into a notification.
        public static void Play(INotification provider, bool allDeleteVariants)
        {
            var artist = Artist();
            var album = Album();
            var files = new List<TrackFile>
            {
                TrackFile(artist, album, 51, "01 - Everlasting Light.flac", AudioQuality.FLAC),
                TrackFile(artist, album, 52, "02 - Next Girl.flac", AudioQuality.FLAC)
            };
            var old = new List<TrackFile> { TrackFile(artist, album, 49, "01 - Everlasting Light.mp3", AudioQuality.MP3_320) };
            var remote = Remote(artist, new List<Album> { album });
            var item = Tracked(remote).DownloadItem;

            var handler = new MusicNotificationHandler(new GoldenData.ProviderDispatcher(provider), Renderer());

            handler.Handle(new ReleaseGrabbedEvent(remote) { DownloadClient = "qBittorrent", DownloadClientName = "qb-main", DownloadId = "HASH123" });
            handler.Handle(new AlbumImportedEvent(artist, album, Release(), files, new List<TrackFile>(), true, item));
            handler.Handle(new AlbumImportedEvent(artist, album, Release(), files, old, true, item));
            handler.Handle(new ArtistRenamedEvent(artist, files.ConvertAll(f => new RenamedTrackFile { TrackFile = f, PreviousPath = "/music/The Black Keys/Brothers/old-" + System.IO.Path.GetFileName(f.Path) })));
            handler.Handle(new ArtistAddCompletedEvent(artist));
            handler.Handle(new ArtistsDeletedEvent(new List<Artist> { artist }, true, false));

            if (allDeleteVariants)
            {
                handler.Handle(new ArtistsDeletedEvent(new List<Artist> { artist }, false, false));
            }

            handler.Handle(new ManualInteractionRequiredEvent(Tracked(remote), new MusicNotificationData { Artist = artist, RemoteAlbum = remote }));
        }
    }

    [TestFixture]
    public class MusicNotificationGoldenWebhookFixture : CoreTest<Webhook>
    {
        [Test]
        public void should_send_lidarr_shaped_webhook_payloads()
        {
            GoldenData.Setup(Mocker);
            Mocker.SetConstant<IEnumerable<IWebhookPayloadBuilder>>(new[] { Mocker.Resolve<MusicWebhookPayloadBuilder>() });
            Subject.Definition = new NotificationDefinition { Settings = new WebhookSettings { Url = "http://x", Method = 1 } };

            var captured = new List<string>();
            Mocker.GetMock<IWebhookProxy>()
                .Setup(p => p.SendWebhook(It.IsAny<WebhookPayload>(), It.IsAny<WebhookSettings>()))
                .Callback<WebhookPayload, WebhookSettings>((p, s) => captured.Add(p.ToJson(Newtonsoft.Json.Formatting.Indented)));

            MusicGoldenData.Play(Subject, true);

            GoldenData.Verify("music-webhook.json", string.Join("\n=====\n", captured), ReadAllText);
        }
    }

    [TestFixture]
    public class MusicNotificationGoldenCustomScriptFixture : CoreTest<CustomScript>
    {
        [Test]
        public void should_run_the_script_with_lidarr_named_variables()
        {
            GoldenData.Setup(Mocker);
            Mocker.SetConstant<IEnumerable<ICustomScriptEnvironmentBuilder>>(new[] { Mocker.Resolve<MusicCustomScriptEnvironmentBuilder>() });
            Subject.Definition = new NotificationDefinition { Settings = new CustomScriptSettings { Path = "/script.sh" } };

            var captured = new List<string>();
            Mocker.GetMock<IProcessProvider>()
                .Setup(p => p.StartAndCapture(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<StringDictionary>()))
                .Returns(new ProcessOutput())
                .Callback<string, string, StringDictionary>((path, args, env) =>
                    captured.Add(string.Join("\n", env.Cast<System.Collections.DictionaryEntry>().Select(e => $"{e.Key}={e.Value}").OrderBy(x => x, StringComparer.Ordinal))));

            MusicGoldenData.Play(Subject, false);

            GoldenData.Verify("music-customscript.txt", string.Join("\n=====\n", captured), ReadAllText);
        }
    }

    [TestFixture]
    public class MusicNotificationGoldenDiscordFixture : CoreTest<Discord>
    {
        [Test]
        public void should_send_lidarr_style_discord_payloads()
        {
            GoldenData.Setup(Mocker);
            var settings = new DiscordSettings { WebHookUrl = "http://x" };
            settings.GrabFields = Enum.GetValues(typeof(DiscordGrabFieldType)).Cast<int>().ToArray();
            settings.ImportFields = Enum.GetValues(typeof(DiscordImportFieldType)).Cast<int>().ToArray();
            settings.ManualInteractionFields = Enum.GetValues(typeof(DiscordManualInteractionFieldType)).Cast<int>().ToArray();
            Subject.Definition = new NotificationDefinition { Settings = settings };

            var captured = new List<string>();
            Mocker.GetMock<IDiscordProxy>()
                .Setup(p => p.SendPayload(It.IsAny<DiscordPayload>(), It.IsAny<DiscordSettings>()))
                .Callback<DiscordPayload, DiscordSettings>((p, s) =>
                {
                    var json = p.ToJson(Newtonsoft.Json.Formatting.Indented);
                    json = System.Text.RegularExpressions.Regex.Replace(json, "\"timestamp\": \"[^\"]*\"", "\"timestamp\": \"T\"");
                    captured.Add(json);
                });

            MusicGoldenData.Play(Subject, false);

            GoldenData.Verify("music-discord.json", string.Join("\n=====\n", captured), ReadAllText);
        }
    }
}
