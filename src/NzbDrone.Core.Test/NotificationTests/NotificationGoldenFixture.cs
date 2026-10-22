using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Processes;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Events;
using NzbDrone.Core.Movies.MediaFiles;
using NzbDrone.Core.Movies.MediaFiles.Events;
using NzbDrone.Core.Movies.MediaFiles.MediaInfo;
using NzbDrone.Core.Movies.Notifications;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Movies.RootFolders;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.CustomScript;
using NzbDrone.Core.Notifications.Discord;
using NzbDrone.Core.Notifications.Discord.Payloads;
using NzbDrone.Core.Notifications.Webhook;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.NotificationTests
{
    // Regression net for contract 7. A fixed Interstellar library goes through the real
    // MovieNotificationHandler and MovieActivityRenderer into one provider, and what the provider
    // sends is compared with Files/Notifications/Golden. The webhook and script goldens were captured
    // before notifications became module-neutral and have not changed. The Discord golden differs from
    // that capture only in the Links field URLs (decision log, contract 7).
    // After a reviewed change, set GOLDEN_OUT=<dir>, run the fixture and copy the files from <dir>.
    internal static class GoldenData
    {
        public static Movie Movie()
        {
            var meta = new MovieMetadata
            {
                Title = "Interstellar",
                Year = 2014,
                TmdbId = 157336,
                ImdbId = "tt0816692",
                Overview = "The adventures of a group of explorers who make use of a newly discovered wormhole to surpass the limitations on human space travel and conquer the vast distances involved in an interstellar voyage.",
                Genres = new List<string> { "Adventure", "Drama", "Science Fiction", "Thriller", "Mystery", "Action" },
                InCinemas = new DateTime(2014, 11, 5, 0, 0, 0, DateTimeKind.Utc),
                PhysicalRelease = new DateTime(2015, 3, 31, 0, 0, 0, DateTimeKind.Utc),
                YouTubeTrailerId = "zSWdZVtXT7E",
                Website = "http://www.interstellarmovie.net",
                OriginalLanguage = Language.English,
                Images = new List<MediaCover.MediaCover>
                {
                    new MediaCover.MediaCover(MediaCoverTypes.Poster, "https://img.example/poster.jpg"),
                    new MediaCover.MediaCover(MediaCoverTypes.Fanart, "https://img.example/fanart.jpg")
                },
                Ratings = new Ratings { Tmdb = new RatingChild { Votes = 100, Value = 8.4m, Type = RatingType.User } }
            };

            return new Movie
            {
                Id = 1,
                MovieMetadata = new LazyLoaded<MovieMetadata>(meta),
                Path = "/movies/Interstellar (2014)",
                Tags = new HashSet<int> { 1, 2 },
                Added = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
                AddOptions = new AddMovieOptions { AddMethod = AddMovieMethod.Manual },
                MovieFileId = 10
            };
        }

        public static MovieFile MovieFile(Movie movie, int id = 10, string rel = "Interstellar (2014) Bluray-1080p.mkv")
        {
            var file = new MovieFile
            {
                Id = id,
                MovieId = movie.Id,
                RelativePath = rel,
                Path = Path.Combine(movie.Path, rel),
                Size = 12345678901,
                DateAdded = new DateTime(2026, 10, 2, 3, 4, 5, DateTimeKind.Utc),
                SceneName = "Interstellar.2014.1080p.BluRay.x264-GRP",
                ReleaseGroup = "GRP",
                Quality = new QualityModel(Quality.Bluray1080p, new Revision(2)),
                IndexerFlags = IndexerFlags.G_Freeleech,
                Edition = "IMAX",
                Languages = new List<Language> { Language.English },
                Movie = movie,
                MediaInfo = new MediaInfoModel
                {
                    VideoFormat = "h264",
                    VideoBitDepth = 8,
                    Height = 1080,
                    Width = 1920,
                    AudioFormat = "ac3",
                    AudioChannels = 6,
                    AudioChannelPositions = "3/2/0.1",
                    AudioLanguages = new List<string> { "eng", "fra" },
                    Subtitles = new List<string> { "eng", "spa" },
                    VideoFps = 23.976m,
                    RunTime = TimeSpan.FromMinutes(169)
                }
            };

            movie.MovieFile = file;
            return file;
        }

        public static ParsedMovieInfo Parsed()
        {
            return new ParsedMovieInfo
            {
                MovieTitles = new List<string> { "Interstellar" },
                Year = 2014,
                Quality = new QualityModel(Quality.Bluray1080p, new Revision(2)),
                ReleaseGroup = "GRP",
                ReleaseTitle = "Interstellar.2014.1080p.BluRay.x264-GRP",
                SimpleReleaseTitle = "Interstellar 2014 1080p BluRay x264-GRP",
                ImdbId = "tt0816692",
                TmdbId = 157336,
                Edition = "IMAX"
            };
        }

        public static RemoteMovie Remote(Movie movie)
        {
            return new RemoteMovie
            {
                Movie = movie,
                ParsedMovieInfo = Parsed(),
                Release = new ReleaseInfo
                {
                    Title = "Interstellar.2014.1080p.BluRay.x264-GRP",
                    Indexer = "Knaben",
                    Size = 9876543210,
                    InfoUrl = "https://example/info",
                    PublishDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                CustomFormats = new List<CustomFormat> { new CustomFormat { Id = 1, Name = "x264" }, new CustomFormat { Id = 2, Name = "IMAX" } },
                CustomFormatScore = 150,
                Languages = new List<Language> { Language.English }
            };
        }

        public static DownloadClientItemClientInfo ClientInfo()
        {
            return new DownloadClientItemClientInfo { Type = "qBittorrent", Name = "qb-main", Id = 3, Protocol = DownloadProtocol.Torrent };
        }

        public static GrabbedReleaseInfo Grabbed()
        {
            return new GrabbedReleaseInfo(new List<NzbDrone.Core.Movies.History.MovieHistory>
            {
                new NzbDrone.Core.Movies.History.MovieHistory
                {
                    Date = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                    SourceTitle = "Interstellar.2014.1080p.BluRay.x264-GRP",
                    ParentId = 1,
                    Data = new Dictionary<string, string> { { "indexer", "Knaben" }, { "size", "9876543210" }, { "indexerFlags", "G_Freeleech" } }
                }
            });
        }

        public static LocalMovie Local(Movie movie)
        {
            return new LocalMovie
            {
                Movie = movie,
                Path = "/downloads/src.mkv",
                CustomFormats = new List<CustomFormat> { new CustomFormat { Id = 1, Name = "x264" } },
                CustomFormatScore = 75,
                Quality = new QualityModel(Quality.Bluray1080p, new Revision(2)),
                Release = Grabbed()
            };
        }

        public static TrackedDownload Tracked(RemoteMovie remote)
        {
            var td = new TrackedDownload
            {
                RemoteRelease = remote,
                DownloadItem = new DownloadClientItem
                {
                    DownloadId = "HASH123",
                    Title = "Interstellar.2014.1080p.BluRay.x264-GRP",
                    TotalSize = 9876543210,
                    DownloadClientInfo = ClientInfo()
                }
            };

            return td;
        }

        public static void Setup(NzbDrone.Test.Common.AutoMoq.AutoMoqer mocker)
        {
            mocker.GetMock<ITagRepository>()
                .Setup(r => r.GetTags(It.IsAny<HashSet<int>>()))
                .Returns(new List<Tag> { new Tag { Id = 1, Label = "scifi" }, new Tag { Id = 2, Label = "favorite" } });
            mocker.GetMock<IConfigFileProvider>().SetupGet(c => c.InstanceName).Returns("Plattarr-golden");
            mocker.GetMock<IConfigService>().SetupGet(c => c.ApplicationUrl).Returns("http://golden:7880");
        }

        public static void Verify(string name, string actual, Func<string, string> readExpected)
        {
            actual = actual.Replace("\r\n", "\n");

            var outDir = Environment.GetEnvironmentVariable("GOLDEN_OUT");

            if (!string.IsNullOrEmpty(outDir))
            {
                Directory.CreateDirectory(outDir);
                File.WriteAllText(Path.Combine(outDir, name), actual);
            }

            actual.Should().Be(readExpected("Files/Notifications/Golden/" + name).Replace("\r\n", "\n"));
        }

        // Sends each neutral message straight to one provider, the way NotificationService does
        // once its enabled-event, tag and upgrade rules have passed.
        public class ProviderDispatcher : INotificationDispatcher
        {
            private readonly INotification _provider;

            public ProviderDispatcher(INotification provider)
            {
                _provider = provider;
            }

            public bool AnyEnabled(NotificationEvent notificationEvent) => true;

            public void Grab(GrabMessage message) => _provider.OnGrab(message);
            public void Download(DownloadMessage message) => _provider.OnDownload(message);
            public void Rename(RenameMessage message) => _provider.OnRename(message);
            public void MediaAdded(MediaAddedMessage message) => _provider.OnMediaAdded(message);
            public void MediaFileDelete(MediaFileDeleteMessage message) => _provider.OnMediaFileDelete(message);
            public void MediaDelete(MediaDeleteMessage message) => _provider.OnMediaDelete(message);
            public void ManualInteractionRequired(ManualInteractionRequiredMessage message) => _provider.OnManualInteractionRequired(message);
        }

        // Publishes every movie event the handler turns into a notification.
        public static void Play(INotification provider, bool allFileDeleteReasons)
        {
            var movie = Movie();
            var file = MovieFile(movie);
            var remote = Remote(movie);
            var old = new DeletedMovieFile(MovieFile(movie, 9, "Interstellar (2014) HDTV-720p.mkv"), "/recycle/old.mkv");
            var item = Tracked(remote).DownloadItem;

            var renderer = new MovieActivityRenderer(new Mock<IMovieService>().Object, new Mock<IRootFolderService>().Object);
            var handler = new MovieNotificationHandler(new ProviderDispatcher(provider), renderer);

            handler.Handle(new ReleaseGrabbedEvent(remote) { DownloadClient = "qBittorrent", DownloadClientName = "qb-main", DownloadId = "HASH123" });
            handler.Handle(new MovieFileImportedEvent(Local(movie), file, new List<DeletedMovieFile>(), true, item));
            handler.Handle(new MovieFileImportedEvent(Local(movie), file, new List<DeletedMovieFile> { old }, true, item));
            handler.Handle(new MovieRenamedEvent(movie, new List<RenamedMovieFile> { new RenamedMovieFile { MovieFile = file, PreviousPath = "/movies/Interstellar (2014)/old.mkv", PreviousRelativePath = "old.mkv" } }));
            handler.Handle(new MovieAddedEvent(movie));
            handler.Handle(new MovieFileDeletedEvent(file, DeleteMediaFileReason.Manual));

            if (allFileDeleteReasons)
            {
                handler.Handle(new MovieFileDeletedEvent(file, DeleteMediaFileReason.Upgrade));
            }

            handler.Handle(new MoviesDeletedEvent(new List<Movie> { movie }, true, false));

            if (allFileDeleteReasons)
            {
                handler.Handle(new MoviesDeletedEvent(new List<Movie> { movie }, false, false));
            }

            handler.Handle(new ManualInteractionRequiredEvent(Tracked(remote), new MovieNotificationData { Release = Grabbed() }));
        }
    }

    [TestFixture]
    public class NotificationGoldenWebhookFixture : CoreTest<Webhook>
    {
        [Test]
        public void should_send_the_captured_webhook_payloads()
        {
            GoldenData.Setup(Mocker);
            Mocker.SetConstant<IEnumerable<IWebhookPayloadBuilder>>(new[] { Mocker.Resolve<MovieWebhookPayloadBuilder>() });
            Subject.Definition = new NotificationDefinition { Settings = new WebhookSettings { Url = "http://x", Method = 1 } };

            var captured = new List<string>();
            Mocker.GetMock<IWebhookProxy>()
                .Setup(p => p.SendWebhook(It.IsAny<WebhookPayload>(), It.IsAny<WebhookSettings>()))
                .Callback<WebhookPayload, WebhookSettings>((p, s) => captured.Add(p.ToJson(Newtonsoft.Json.Formatting.Indented)));

            GoldenData.Play(Subject, true);

            GoldenData.Verify("webhook.json", string.Join("\n=====\n", captured), ReadAllText);
        }
    }

    [TestFixture]
    public class NotificationGoldenCustomScriptFixture : CoreTest<CustomScript>
    {
        [Test]
        public void should_run_the_script_with_the_captured_environment()
        {
            GoldenData.Setup(Mocker);
            Mocker.SetConstant<IEnumerable<ICustomScriptEnvironmentBuilder>>(new[] { Mocker.Resolve<MovieCustomScriptEnvironmentBuilder>() });
            Subject.Definition = new NotificationDefinition { Settings = new CustomScriptSettings { Path = "/script.sh" } };

            var captured = new List<string>();
            Mocker.GetMock<IProcessProvider>()
                .Setup(p => p.StartAndCapture(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<StringDictionary>()))
                .Returns(new ProcessOutput())
                .Callback<string, string, StringDictionary>((path, args, env) =>
                    captured.Add(string.Join("\n", env.Cast<System.Collections.DictionaryEntry>().Select(e => $"{e.Key}={e.Value}").OrderBy(x => x, StringComparer.Ordinal))));

            GoldenData.Play(Subject, false);

            GoldenData.Verify("customscript.txt", string.Join("\n=====\n", captured), ReadAllText);
        }
    }

    [TestFixture]
    public class NotificationGoldenDiscordFixture : CoreTest<Discord>
    {
        [Test]
        public void should_send_the_captured_discord_payloads()
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
                    // Timestamps are wall-clock; blank them so the golden file is stable.
                    var json = p.ToJson(Newtonsoft.Json.Formatting.Indented);
                    json = System.Text.RegularExpressions.Regex.Replace(json, "\"timestamp\": \"[^\"]*\"", "\"timestamp\": \"T\"");
                    captured.Add(json);
                });

            GoldenData.Play(Subject, false);

            GoldenData.Verify("discord.json", string.Join("\n=====\n", captured), ReadAllText);
        }
    }
}
