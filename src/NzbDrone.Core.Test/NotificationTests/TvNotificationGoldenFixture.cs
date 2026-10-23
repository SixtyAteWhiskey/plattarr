using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Processes;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.CustomScript;
using NzbDrone.Core.Notifications.Discord;
using NzbDrone.Core.Notifications.Discord.Payloads;
using NzbDrone.Core.Notifications.Webhook;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Events;
using NzbDrone.Core.Tv.History;
using NzbDrone.Core.Tv.MediaFiles;
using NzbDrone.Core.Tv.MediaFiles.Events;
using NzbDrone.Core.Tv.MediaFiles.MediaInfo;
using NzbDrone.Core.Tv.Notifications;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Tv.RootFolders;

namespace NzbDrone.Core.Test.NotificationTests
{
    // Regression net for the TV half of contract 7, as NotificationGoldenFixture is for movies. A fixed
    // Game of Thrones library goes through the real TvNotificationHandler and TvActivityRenderer into one
    // provider, and what the provider sends is compared with Files/Notifications/Golden/tv-*. The
    // webhook and script files are Sonarr's payload shapes and variable names. After a reviewed change,
    // set GOLDEN_OUT=<dir>, run the fixture and copy the files from <dir>.
    internal static class TvGoldenData
    {
        public static Series Series()
        {
            return new Series
            {
                Id = 1,
                TvdbId = 121361,
                TvMazeId = 82,
                TmdbId = 1399,
                ImdbId = "tt0944947",
                Title = "Game of Thrones",
                TitleSlug = "game-of-thrones",
                Year = 2011,
                Path = "/tv/Game of Thrones",
                SeriesType = SeriesTypes.Standard,
                OriginalLanguage = Language.English,
                OriginalCountry = "us",
                Overview = "Nine noble families fight for control over the lands of Westeros.",
                Genres = new List<string> { "Drama", "Fantasy" },
                Ratings = new Ratings { Votes = 1000, Value = 9.2m },
                Tags = new HashSet<int> { 1, 2 },
                Added = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
                Images = new List<MediaCover.MediaCover>
                {
                    new MediaCover.MediaCover(MediaCoverTypes.Poster, "https://img.example/poster.jpg"),
                    new MediaCover.MediaCover(MediaCoverTypes.Fanart, "https://img.example/fanart.jpg")
                }
            };
        }

        public static List<Episode> Episodes(Series series)
        {
            return new List<Episode>
            {
                new Episode { Id = 101, SeriesId = series.Id, TvdbId = 3254641, SeasonNumber = 1, EpisodeNumber = 1, Title = "Winter Is Coming", AirDate = "2011-04-17", AirDateUtc = new DateTime(2011, 4, 18, 1, 0, 0, DateTimeKind.Utc), Overview = "Eddard Stark is torn between his family and an old friend.", AbsoluteEpisodeNumber = 1, FinaleType = null },
                new Episode { Id = 102, SeriesId = series.Id, TvdbId = 3436411, SeasonNumber = 1, EpisodeNumber = 2, Title = "The Kingsroad", AirDate = "2011-04-24", AirDateUtc = new DateTime(2011, 4, 25, 1, 0, 0, DateTimeKind.Utc), Overview = "The Starks leave Winterfell.", AbsoluteEpisodeNumber = 2, FinaleType = null }
            };
        }

        public static EpisodeFile EpisodeFile(Series series, List<Episode> episodes, int id = 10, string rel = "Season 01/Game of Thrones - S01E01-E02 WEBDL-1080p.mkv")
        {
            return new EpisodeFile
            {
                Id = id,
                SeriesId = series.Id,
                SeasonNumber = 1,
                RelativePath = rel,
                Path = Path.Combine(series.Path, rel),
                Size = 4294967296,
                DateAdded = new DateTime(2026, 10, 2, 3, 4, 5, DateTimeKind.Utc),
                SceneName = "Game.of.Thrones.S01E01E02.1080p.WEB-DL-GRP",
                ReleaseGroup = "GRP",
                Quality = new QualityModel(Quality.WEBDL1080p, new Revision(2)),
                IndexerFlags = IndexerFlags.G_Freeleech,
                ReleaseType = ReleaseType.MultiEpisode,
                Languages = new List<Language> { Language.English },
                Episodes = new LazyLoaded<List<Episode>>(episodes),
                Series = new LazyLoaded<Series>(series),
                MediaInfo = new MediaInfoModel
                {
                    VideoFormat = "h264",
                    VideoBitDepth = 8,
                    Height = 1080,
                    Width = 1920,
                    VideoFps = 23.976m,
                    RunTime = TimeSpan.FromMinutes(57),
                    AudioStreams = new List<MediaInfoAudioStreamModel>
                    {
                        new MediaInfoAudioStreamModel { Language = "eng", Format = "ac3", Channels = 6, ChannelPositions = "3/2/0.1" },
                        new MediaInfoAudioStreamModel { Language = "fra", Format = "ac3", Channels = 2, ChannelPositions = "2/0/0" }
                    },
                    SubtitleStreams = new List<MediaInfoSubtitleStreamModel>
                    {
                        new MediaInfoSubtitleStreamModel { Language = "eng" },
                        new MediaInfoSubtitleStreamModel { Language = "spa" }
                    }
                }
            };
        }

        public static RemoteEpisode Remote(Series series, List<Episode> episodes)
        {
            return new RemoteEpisode
            {
                Series = series,
                Episodes = episodes,
                ParsedEpisodeInfo = new ParsedEpisodeInfo
                {
                    SeriesTitle = "Game of Thrones",
                    SeasonNumbers = new List<int> { 1 },
                    EpisodeNumbers = new[] { 1, 2 },
                    Quality = new QualityModel(Quality.WEBDL1080p, new Revision(2)),
                    ReleaseGroup = "GRP",
                    ReleaseTitle = "Game.of.Thrones.S01E01E02.1080p.WEB-DL-GRP"
                },
                Release = new ReleaseInfo
                {
                    Title = "Game.of.Thrones.S01E01E02.1080p.WEB-DL-GRP",
                    Indexer = "Knaben",
                    Size = 4294967296,
                    InfoUrl = "https://example/info",
                    PublishDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                CustomFormats = new List<CustomFormat> { new CustomFormat { Id = 1, Name = "WEB" }, new CustomFormat { Id = 2, Name = "x264" } },
                CustomFormatScore = 150,
                Languages = new List<Language> { Language.English }
            };
        }

        public static GrabbedReleaseInfo Grabbed()
        {
            return new GrabbedReleaseInfo(GrabbedHistory());
        }

        public static List<EpisodeHistory> GrabbedHistory()
        {
            return new List<EpisodeHistory>
            {
                new EpisodeHistory
                {
                    Date = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                    SourceTitle = "Game.of.Thrones.S01E01E02.1080p.WEB-DL-GRP",
                    ParentId = 1,
                    ItemId = 101,
                    Data = new Dictionary<string, string> { { "indexer", "Knaben" }, { "size", "4294967296" }, { "indexerFlags", "G_Freeleech" }, { "releaseType", "MultiEpisode" } }
                },
                new EpisodeHistory
                {
                    Date = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                    SourceTitle = "Game.of.Thrones.S01E01E02.1080p.WEB-DL-GRP",
                    ParentId = 1,
                    ItemId = 102,
                    Data = new Dictionary<string, string> { { "indexer", "Knaben" }, { "size", "4294967296" }, { "indexerFlags", "G_Freeleech" }, { "releaseType", "MultiEpisode" } }
                }
            };
        }

        public static LocalEpisode Local(Series series, List<Episode> episodes)
        {
            return new LocalEpisode
            {
                Series = series,
                Episodes = episodes,
                Path = "/downloads/src.mkv",
                CustomFormats = new List<CustomFormat> { new CustomFormat { Id = 1, Name = "WEB" } },
                CustomFormatScore = 75,
                Quality = new QualityModel(Quality.WEBDL1080p, new Revision(2)),
                Release = Grabbed()
            };
        }

        public static TrackedDownload Tracked(RemoteEpisode remote)
        {
            return new TrackedDownload
            {
                RemoteRelease = remote,
                DownloadItem = new DownloadClientItem
                {
                    DownloadId = "HASH123",
                    Title = "Game.of.Thrones.S01E01E02.1080p.WEB-DL-GRP",
                    TotalSize = 4294967296,
                    DownloadClientInfo = GoldenData.ClientInfo()
                }
            };
        }

        // Publishes every TV event the handler turns into a notification.
        public static void Play(INotification provider, bool allFileDeleteReasons)
        {
            var series = Series();
            var episodes = Episodes(series);
            var file = EpisodeFile(series, episodes);
            var remote = Remote(series, episodes);
            var old = new DeletedEpisodeFile(EpisodeFile(series, episodes, 9, "Season 01/Game of Thrones - S01E01-E02 HDTV-720p.mkv"), "/recycle/old.mkv");
            var item = Tracked(remote).DownloadItem;

            var renderer = new TvActivityRenderer(new Mock<ISeriesService>().Object, new Mock<IEpisodeService>().Object, new Mock<IRootFolderService>().Object);
            var handler = new TvNotificationHandler(new GoldenData.ProviderDispatcher(provider), renderer);

            handler.Handle(new ReleaseGrabbedEvent(remote) { DownloadClient = "qBittorrent", DownloadClientName = "qb-main", DownloadId = "HASH123" });
            handler.Handle(new EpisodeImportedEvent(Local(series, episodes), file, new List<DeletedEpisodeFile>(), true, item));
            handler.Handle(new EpisodeImportedEvent(Local(series, episodes), file, new List<DeletedEpisodeFile> { old }, true, item));
            handler.Handle(new SeriesRenamedEvent(series, new List<RenamedEpisodeFile> { new RenamedEpisodeFile { EpisodeFile = file, PreviousPath = "/tv/Game of Thrones/Season 01/old.mkv", PreviousRelativePath = "Season 01/old.mkv" } }));
            handler.Handle(new SeriesAddCompletedEvent(series));
            handler.Handle(new EpisodeFileDeletedEvent(file, DeleteMediaFileReason.Manual));

            if (allFileDeleteReasons)
            {
                handler.Handle(new EpisodeFileDeletedEvent(file, DeleteMediaFileReason.Upgrade));
            }

            handler.Handle(new SeriesDeletedEvent(new List<Series> { series }, true, false));

            if (allFileDeleteReasons)
            {
                handler.Handle(new SeriesDeletedEvent(new List<Series> { series }, false, false));
            }

            handler.Handle(new ManualInteractionRequiredEvent(Tracked(remote), new TvNotificationData { Release = Grabbed() }));

            PlayImportComplete(provider, renderer, series, episodes, file, remote);
        }

        // The import-complete event, which a separate handler turns into a notification from the download's history.
        private static void PlayImportComplete(INotification provider, TvActivityRenderer renderer, Series series, List<Episode> episodes, EpisodeFile file, RemoteEpisode remote)
        {
            episodes.ForEach(e => e.EpisodeFileId = file.Id);

            var seriesService = new Mock<ISeriesService>();
            seriesService.Setup(s => s.GetSeries(series.Id)).Returns(series);

            var history = new Mock<IHistoryService>();
            history.Setup(h => h.Find("HASH123", EpisodeHistoryEventType.DownloadFolderImported))
                   .Returns(episodes.ConvertAll(e => new EpisodeHistory { ParentId = series.Id, ItemId = e.Id }));
            history.Setup(h => h.Find("HASH123", EpisodeHistoryEventType.Grabbed)).Returns(GrabbedHistory());

            var episodeService = new Mock<IEpisodeService>();
            episodeService.Setup(s => s.GetEpisodes(It.IsAny<IEnumerable<int>>())).Returns(episodes);

            var mediaFileService = new Mock<IMediaFileService>();
            mediaFileService.Setup(s => s.GetFiles(It.IsAny<IEnumerable<int>>())).Returns(new List<EpisodeFile> { file });

            var tracked = Tracked(remote);
            tracked.Kind = MediaKind.Tv;
            tracked.DownloadItem.OutputPath = new OsPath("/downloads/Game.of.Thrones.S01E01E02.1080p.WEB-DL-GRP");

            var notifier = new TvImportCompleteNotifier(new GoldenData.ProviderDispatcher(provider), renderer, history.Object, seriesService.Object, episodeService.Object, mediaFileService.Object, NzbDrone.Common.Instrumentation.NzbDroneLogger.GetLogger(typeof(TvGoldenData)));

            notifier.Handle(new DownloadCompletedEvent(tracked, series.Id));
        }
    }

    [TestFixture]
    public class TvNotificationGoldenWebhookFixture : CoreTest<Webhook>
    {
        [Test]
        public void should_send_sonarr_shaped_webhook_payloads()
        {
            GoldenData.Setup(Mocker);
            Mocker.SetConstant<IEnumerable<IWebhookPayloadBuilder>>(new[] { Mocker.Resolve<TvWebhookPayloadBuilder>() });
            Subject.Definition = new NotificationDefinition { Settings = new WebhookSettings { Url = "http://x", Method = 1 } };

            var captured = new List<string>();
            Mocker.GetMock<IWebhookProxy>()
                .Setup(p => p.SendWebhook(It.IsAny<WebhookPayload>(), It.IsAny<WebhookSettings>()))
                .Callback<WebhookPayload, WebhookSettings>((p, s) => captured.Add(p.ToJson(Newtonsoft.Json.Formatting.Indented)));

            TvGoldenData.Play(Subject, true);

            GoldenData.Verify("tv-webhook.json", string.Join("\n=====\n", captured), ReadAllText);
        }
    }

    [TestFixture]
    public class TvNotificationGoldenCustomScriptFixture : CoreTest<CustomScript>
    {
        [Test]
        public void should_run_the_script_with_sonarr_named_variables()
        {
            GoldenData.Setup(Mocker);
            Mocker.SetConstant<IEnumerable<ICustomScriptEnvironmentBuilder>>(new[] { Mocker.Resolve<TvCustomScriptEnvironmentBuilder>() });
            Subject.Definition = new NotificationDefinition { Settings = new CustomScriptSettings { Path = "/script.sh" } };

            var captured = new List<string>();
            Mocker.GetMock<IProcessProvider>()
                .Setup(p => p.StartAndCapture(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<StringDictionary>()))
                .Returns(new ProcessOutput())
                .Callback<string, string, StringDictionary>((path, args, env) =>
                    captured.Add(string.Join("\n", env.Cast<System.Collections.DictionaryEntry>().Select(e => $"{e.Key}={e.Value}").OrderBy(x => x, StringComparer.Ordinal))));

            TvGoldenData.Play(Subject, false);

            GoldenData.Verify("tv-customscript.txt", string.Join("\n=====\n", captured), ReadAllText);
        }
    }

    [TestFixture]
    public class TvNotificationGoldenDiscordFixture : CoreTest<Discord>
    {
        [Test]
        public void should_send_sonarr_style_discord_payloads()
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

            TvGoldenData.Play(Subject, false);

            GoldenData.Verify("tv-discord.json", string.Join("\n=====\n", captured), ReadAllText);
        }
    }
}
