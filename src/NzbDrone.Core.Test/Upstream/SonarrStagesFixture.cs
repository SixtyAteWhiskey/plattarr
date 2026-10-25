using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Extras.Metadata;
using NzbDrone.Core.Tv.Extras.Metadata.Files;
using NzbDrone.Core.Tv.Extras.Others;
using NzbDrone.Core.Tv.Extras.Subtitles;
using NzbDrone.Core.Tv.MediaFiles;
using NzbDrone.Core.Tv.RootFolders;
using NzbDrone.Core.Tv.Upstream;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    // The TV stages against a migrated target (SQLite and Postgres). Source rows follow the
    // Sonarr 4.0.20 schema (sonarr-217.schema.sql) and the shapes a fresh Sonarr writes: dates as
    // text with fractional seconds, documents as camelCase JSON, NOT NULL columns always supplied.
    // No live Sonarr rows were available when this was written (session 61); the shapes are to
    // be checked against the owner's sonarr.db.
    [TestFixture]
    public class SonarrStagesFixture : DbTest
    {
        // Sonarr quality 13 is Bluray-480p, which has a different ID in the catalog.
        private const string Quality480 = "{\"quality\":13,\"revision\":{\"version\":1,\"real\":0,\"isRepack\":false}}";

        private const string Stamp = "2024-03-01 12:30:45.1234567Z";

        private const string Seasons =
            "[{\"seasonNumber\":0,\"monitored\":false,\"images\":[]},{\"seasonNumber\":1,\"monitored\":true,\"images\":[]},{\"seasonNumber\":2,\"monitored\":false}]";

        private SeriesRepository _series;
        private EpisodeRepository _episodes;
        private MediaFileRepository _files;
        private RootFolderRepository _rootFolders;
        private SubtitleFileRepository _subtitles;
        private OtherExtraFileRepository _others;
        private MetadataFileRepository _metadataFiles;

        [SetUp]
        public void SetUpStages()
        {
            var customFormats = Mocker.Resolve<CustomFormatRepository>();

            Mocker.GetMock<ICustomFormatService>().Setup(s => s.All()).Returns(() => customFormats.All().ToList());

            _series = Mocker.Resolve<SeriesRepository>();
            _episodes = Mocker.Resolve<EpisodeRepository>();
            _files = Mocker.Resolve<MediaFileRepository>();
            _rootFolders = Mocker.Resolve<RootFolderRepository>();
            _subtitles = Mocker.Resolve<SubtitleFileRepository>();
            _others = Mocker.Resolve<OtherExtraFileRepository>();
            _metadataFiles = Mocker.Resolve<MetadataFileRepository>();

            Mocker.SetConstant<ICustomFormatRepository>(customFormats);
            Mocker.SetConstant<IQualityProfileRepository>(Mocker.Resolve<QualityProfileRepository>());
            Mocker.SetConstant<IQualityDefinitionRepository>(Mocker.Resolve<QualityDefinitionRepository>());
            Mocker.SetConstant<IDelayProfileRepository>(Mocker.Resolve<DelayProfileRepository>());
            Mocker.SetConstant<ITagRepository>(Mocker.Resolve<TagRepository>());
            Mocker.SetConstant<ISeriesRepository>(_series);
            Mocker.SetConstant<IEpisodeRepository>(_episodes);
            Mocker.SetConstant<IMediaFileRepository>(_files);
            Mocker.SetConstant<IRootFolderRepository>(_rootFolders);
            Mocker.SetConstant<ISubtitleFileRepository>(_subtitles);
            Mocker.SetConstant<IOtherExtraFileRepository>(_others);
            Mocker.SetConstant<IMetadataFileRepository>(_metadataFiles);
            Mocker.SetConstant<IEnumerable<IUpstreamCatalog>>(new IUpstreamCatalog[] { new RadarrCatalog(), new SonarrCatalog() });
            Mocker.SetConstant<IEnumerable<IImportStage>>(new IImportStage[]
            {
                Mocker.Resolve<TagsStage>(),
                Mocker.Resolve<ProfilesStage>(),
                Mocker.Resolve<TvRootFoldersStage>(),
                Mocker.Resolve<SeriesStage>(),
                Mocker.Resolve<TvExtrasStage>()
            });
        }

        private ImportResult Run(params string[] rows)
        {
            return Run(new ImportOptions(), rows);
        }

        private ImportResult Run(ImportOptions options, params string[] rows)
        {
            var common = new[]
            {
                UpstreamFixtureTest.Insert("Tags", "Id, Label", "1, 'Kids'"),
                Profile(1)
            };

            var snapshot = UpstreamFixtureTest.ReadSnapshot(
                new SonarrAdapter(),
                GetTestPath("Files/Upstream/sonarr-217.schema.sql"),
                217,
                common.Concat(rows),
                TempFolder);

            return Mocker.Resolve<UpstreamImporter>().Run(new[] { snapshot }, options, false);
        }

        // Sonarr 4 profiles have no Language column.
        private static string Profile(int id)
        {
            return UpstreamFixtureTest.Insert(
                "QualityProfiles",
                "Id, Name, Cutoff, Items, UpgradeAllowed, FormatItems, MinFormatScore, CutoffFormatScore, MinUpgradeFormatScore",
                $"{id}, 'HD', 13, '[{{\"quality\":13,\"items\":[],\"allowed\":true}}]', 1, '[]', 0, 0, 1");
        }

        private static string SeriesRow(int id, string title = "Bluey", string path = "/tv/Bluey", string profile = "1", string tags = "[1]", string seasons = Seasons)
        {
            return UpstreamFixtureTest.Insert(
                "Series",
                "Id, TvdbId, TvRageId, Title, CleanTitle, Status, Images, Path, Monitored, SeasonFolder, Runtime, SeriesType, UseSceneNumbering, Seasons, QualityProfileId, Tags, Added, TvMazeId, OriginalLanguage, MonitorNewItems",
                $"{id}, {353545 + id}, 0, {UpstreamFixtureTest.Sql(title)}, {UpstreamFixtureTest.Sql(title.ToLowerInvariant())}, 1, '[]', {UpstreamFixtureTest.Sql(path)}, 1, 1, 7, 0, 0, {UpstreamFixtureTest.Sql(seasons)}, {profile}, {UpstreamFixtureTest.Sql(tags)}, '{Stamp}', 0, 3, 1");
        }

        private static string FileRow(int id, int seriesId, int seasonNumber = 1, string path = "Season 1/Bluey - S01E01.mkv", string mediaInfo = "NULL", int flags = 8 | 16)
        {
            return UpstreamFixtureTest.Insert(
                "EpisodeFiles",
                "Id, SeriesId, Quality, Size, DateAdded, SeasonNumber, MediaInfo, RelativePath, Languages, IndexerFlags, ReleaseType",
                $"{id}, {seriesId}, {UpstreamFixtureTest.Sql(Quality480)}, 500000000, '{Stamp}', {seasonNumber}, {mediaInfo}, {UpstreamFixtureTest.Sql(path)}, '[3]', {flags}, 1");
        }

        private static string EpisodeRow(int id, int seriesId, int season, int episode, string monitored, int fileId = 0)
        {
            return UpstreamFixtureTest.Insert(
                "Episodes",
                "Id, SeriesId, SeasonNumber, EpisodeNumber, Title, EpisodeFileId, Monitored, AirDate, UnverifiedSceneNumbering, Runtime",
                $"{id}, {seriesId}, {season}, {episode}, 'Episode {episode}', {fileId}, {monitored}, '2018-10-01', 0, 7");
        }

        [Test]
        public void should_import_root_folders_and_merge_two_that_map_to_one_path()
        {
            var result = Run(new ImportOptions
                {
                    PathMap = new Dictionary<string, string>
                    {
                        ["/tv"] = "/shows",
                        ["/anime"] = "/shows"
                    }
                },
                UpstreamFixtureTest.Insert("RootFolders", "Id, Path", "1, '/tv'"),
                UpstreamFixtureTest.Insert("RootFolders", "Id, Path", "2, '/anime'"));

            _rootFolders.All().Select(r => r.Path).Should().BeEquivalentTo("/shows");
            result.IdMap.Resolve("Sonarr", "RootFolders", 2).Should().Be(result.IdMap.Resolve("Sonarr", "RootFolders", 1));
            result.Tables.Single(t => t.App == "Sonarr" && t.Table == "RootFolders").Should().Match<ReconciliationRow>(r => r.Read == 2 && r.Imported == 2);
        }

        [Test]
        public void should_import_a_series_with_its_seasons_tags_and_profile()
        {
            var result = Run(SeriesRow(1));

            var series = _series.All().Single();

            series.Title.Should().Be("Bluey");
            series.Path.Should().Be("/tv/Bluey");
            series.Monitored.Should().BeTrue();
            series.MonitorNewItems.Should().Be(NewItemMonitorTypes.None);
            series.QualityProfileId.Should().Be((int)result.IdMap.Resolve("Sonarr", "QualityProfiles", 1));
            series.Tags.Should().Equal((int)result.IdMap.Resolve("Sonarr", "Tags", 1));
            series.OriginalLanguage.Should().Be(Language.Spanish);
            series.Language.Should().Be(Language.English);
            series.SeasonType.Should().Be(SeasonType.Official);
            series.Seasons.Select(s => (s.SeasonNumber, s.Monitored)).Should().Equal((0, false), (1, true), (2, false));
            series.Seasons.Should().OnlyContain(s => s.Images != null);
            result.IdMap.Resolve("Sonarr", "Series", 1).Should().Be(series.Id);
        }

        [Test]
        public void should_keep_each_episode_monitored_flag_and_link_its_file()
        {
            var result = Run(
                SeriesRow(1),
                FileRow(10, 1),
                EpisodeRow(100, 1, 0, 1, "0"),
                EpisodeRow(101, 1, 1, 1, "1", fileId: 10),
                EpisodeRow(102, 1, 1, 2, "0"),
                EpisodeRow(103, 1, 1, 3, "NULL"));

            var file = _files.All().Single();
            var episodes = _episodes.All().OrderBy(e => e.SeasonNumber).ThenBy(e => e.EpisodeNumber).ToList();

            episodes.Select(e => e.Monitored).Should().Equal(false, true, false, false);
            episodes[1].EpisodeFileId.Should().Be(file.Id);
            episodes.Where(e => e != episodes[1]).Should().OnlyContain(e => e.EpisodeFileId == 0);
            episodes.Should().OnlyContain(e => e.SeriesId == _series.All().Single().Id);
            result.Skips.Should().BeEmpty();
        }

        [Test]
        public void should_remap_the_quality_languages_and_indexer_flags_of_an_episode_file()
        {
            Run(SeriesRow(1), FileRow(10, 1));

            var file = _files.All().Single();

            file.Quality.Quality.Should().Be(Quality.Bluray480p);
            file.Languages.Should().Equal(Language.Spanish);
            file.IndexerFlags.Should().Be(IndexerFlags.G_Internal | IndexerFlags.G_Scene);
            file.SeasonNumber.Should().Be(1);
            file.RelativePath.Should().Be("Season 1/Bluey - S01E01.mkv");
        }

        [Test]
        public void should_skip_an_episode_file_with_an_indexer_flag_sonarr_never_had()
        {
            var result = Run(SeriesRow(1), FileRow(10, 1, flags: 256), EpisodeRow(100, 1, 1, 1, "1", fileId: 10));

            result.Skips.Should().ContainSingle(s => s.Table == "EpisodeFiles" && s.SourceId == 10 && s.Reason == SkipReason.Corrupt);
            _files.All().Should().BeEmpty();
            _episodes.All().Single().EpisodeFileId.Should().Be(0);
        }

        [Test]
        public void should_drop_unreadable_media_info_and_keep_the_file()
        {
            var result = Run(SeriesRow(1), FileRow(10, 1, mediaInfo: "'{not json'"));

            _files.All().Single().MediaInfo.Should().BeNull();
            result.Warnings.Should().ContainSingle(w => w.StartsWith("1 episode file(s) had media info"));
        }

        [Test]
        public void should_skip_a_series_without_a_profile_and_everything_under_it()
        {
            var result = Run(
                SeriesRow(1, profile: "NULL"),
                SeriesRow(2, title: "Taskmaster", path: "/tv/Taskmaster"),
                FileRow(10, 1),
                EpisodeRow(100, 1, 1, 1, "1", fileId: 10),
                EpisodeRow(200, 2, 1, 1, "1"));

            _series.All().Select(s => s.Title).Should().Equal("Taskmaster");
            result.Skips.Should().Contain(s => s.Table == "Series" && s.SourceId == 1 && s.Name == "Bluey" && s.Reason == SkipReason.Corrupt);
            result.Skips.Should().Contain(s => s.Table == "EpisodeFiles" && s.SourceId == 10 && s.Reason == SkipReason.Corrupt);
            result.Skips.Should().Contain(s => s.Table == "Episodes" && s.SourceId == 100 && s.Name == "Bluey S01E01" && s.Reason == SkipReason.Corrupt);
            result.Tables.Single(t => t.App == "Sonarr" && t.Table == "Episodes").Should().Match<ReconciliationRow>(r => r.Read == 2 && r.Imported == 1 && r.Skipped == 1);
        }

        [Test]
        public void should_skip_a_series_without_a_path()
        {
            var result = Run(SeriesRow(1, path: ""));

            _series.All().Should().BeEmpty();
            result.Skips.Should().ContainSingle(s => s.Table == "Series" && s.SourceId == 1 && s.Reason == SkipReason.Corrupt);
        }

        [Test]
        public void should_import_an_episode_whose_file_is_not_in_the_database_without_a_file_and_warn()
        {
            var result = Run(SeriesRow(1), EpisodeRow(100, 1, 1, 1, "1", fileId: 99));

            _episodes.All().Single().EpisodeFileId.Should().Be(0);
            result.Warnings.Should().ContainSingle(w => w.StartsWith("1 episode(s) pointed at a file"));
        }

        [Test]
        public void should_import_extras_linked_to_the_series_and_the_episode_file()
        {
            var result = Run(
                SeriesRow(1),
                FileRow(10, 1),
                UpstreamFixtureTest.Insert(
                    "SubtitleFiles",
                    "Id, SeriesId, SeasonNumber, EpisodeFileId, RelativePath, Extension, Added, LastUpdated, Language, LanguageTags, Copy",
                    $"1, 1, 1, 10, 'Season 1/Bluey - S01E01.es.srt', '.srt', '{Stamp}', '{Stamp}', 3, '[\"forced\"]', 0"),
                UpstreamFixtureTest.Insert(
                    "ExtraFiles",
                    "Id, SeriesId, SeasonNumber, EpisodeFileId, RelativePath, Extension, Added, LastUpdated",
                    $"1, 1, 1, 10, 'Season 1/Bluey - S01E01.nfo-orig', '.nfo-orig', '{Stamp}', '{Stamp}'"),
                UpstreamFixtureTest.Insert(
                    "MetadataFiles",
                    "Id, SeriesId, Consumer, Type, RelativePath, LastUpdated, SeasonNumber, EpisodeFileId, Added, Extension",
                    $"1, 1, 'XbmcMetadata', 1, 'tvshow.nfo', '{Stamp}', NULL, NULL, '{Stamp}', '.nfo'"),
                UpstreamFixtureTest.Insert(
                    "SubtitleFiles",
                    "Id, SeriesId, SeasonNumber, EpisodeFileId, RelativePath, Extension, Added, LastUpdated, Language",
                    $"2, 1, 1, 77, 'Season 1/gone.srt', '.srt', '{Stamp}', '{Stamp}', 1"));

            var series = _series.All().Single();
            var file = _files.All().Single();

            var subtitle = _subtitles.All().Single();
            subtitle.SeriesId.Should().Be(series.Id);
            subtitle.EpisodeFileId.Should().Be(file.Id);
            subtitle.SeasonNumber.Should().Be(1);
            subtitle.Language.Should().Be(Language.Spanish);
            subtitle.LanguageTags.Should().Equal("forced");

            _others.All().Single().EpisodeFileId.Should().Be(file.Id);

            var metadata = _metadataFiles.All().Single();
            metadata.Consumer.Should().Be("XbmcMetadata");
            metadata.Type.Should().Be(MetadataType.SeriesMetadata);
            metadata.EpisodeFileId.Should().BeNull();

            result.Skips.Should().ContainSingle(s => s.Table == "SubtitleFiles" && s.SourceId == 2 && s.Reason == SkipReason.Corrupt);
        }

        [Test]
        public void should_scope_the_sonarr_profile_to_tv_and_add_the_qualities_sonarr_does_not_have()
        {
            Run();

            var profile = Mocker.Resolve<QualityProfileRepository>().All().Single(p => p.Name == "HD");

            profile.AppliesTo.Should().Equal(MediaKind.Tv);
            profile.Language.Should().Be(Language.Unknown);
            profile.Items.Select(i => i.Quality?.Id).Should().Contain(Quality.Bluray480p.Id);
            profile.Items.Should().HaveCount(Quality.DefaultQualityDefinitions.Count);
            profile.Items.Where(i => i.Allowed).Select(i => i.Quality).Should().Equal(Quality.Bluray480p);
            profile.Items.Last().Quality.Should().Be(Quality.Bluray480p);
        }

        [Test]
        public void should_account_for_every_row_of_the_tv_tables()
        {
            var result = Run(
                UpstreamFixtureTest.Insert("RootFolders", "Id, Path", "1, '/tv'"),
                SeriesRow(1),
                FileRow(10, 1),
                EpisodeRow(100, 1, 1, 1, "1", fileId: 10));

            foreach (var table in new[] { "RootFolders", "Series", "EpisodeFiles", "Episodes" })
            {
                result.Tables.Single(t => t.App == "Sonarr" && t.Table == table).Should().Match<ReconciliationRow>(r => r.Read == 1 && r.Imported == 1 && r.Skipped == 0);
            }
        }
    }
}
