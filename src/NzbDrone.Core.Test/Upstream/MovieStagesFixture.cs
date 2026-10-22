using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Extras.Metadata;
using NzbDrone.Core.Extras.Metadata.Files;
using NzbDrone.Core.Extras.Others;
using NzbDrone.Core.Extras.Subtitles;
using NzbDrone.Core.ImportLists.ImportExclusions;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.AlternativeTitles;
using NzbDrone.Core.Movies.Collections;
using NzbDrone.Core.Movies.Credits;
using NzbDrone.Core.Movies.Translations;
using NzbDrone.Core.Movies.Upstream;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    // The movie stages against a migrated target (SQLite and Postgres). Source rows follow the
    // shapes in the session 30 dump and the Radarr 5.x schema: dates as text with fractional
    // seconds, documents as JSON text, NOT NULL columns always supplied.
    [TestFixture]
    public class MovieStagesFixture : DbTest
    {
        private const string Quality1080 = "{\"quality\":7,\"revision\":{\"version\":1,\"real\":0,\"isRepack\":false}}";

        // Radarr writes seven fractional digits; Postgres keeps six, so compare to the millisecond.
        private const string Stamp = "2024-03-01 12:30:45.1234567Z";
        private static readonly DateTime StampUtc = new DateTime(2024, 3, 1, 12, 30, 45, DateTimeKind.Utc).AddTicks(1234567);

        private MovieRepository _movies;
        private MovieMetadataRepository _metadata;
        private AlternativeTitleRepository _alternativeTitles;
        private MovieTranslationRepository _translations;
        private CreditRepository _credits;
        private MovieCollectionRepository _collections;
        private MediaFileRepository _files;
        private SubtitleFileRepository _subtitles;
        private OtherExtraFileRepository _others;
        private MetadataFileRepository _metadataFiles;
        private IImportListExclusionRepository _exclusions;
        private QualityProfileRepository _profiles;

        [SetUp]
        public void SetUpStages()
        {
            var customFormats = Mocker.Resolve<CustomFormatRepository>();

            Mocker.GetMock<ICustomFormatService>().Setup(s => s.All()).Returns(() => customFormats.All().ToList());

            _profiles = Mocker.Resolve<QualityProfileRepository>();
            _metadata = Mocker.Resolve<MovieMetadataRepository>();
            _alternativeTitles = Mocker.Resolve<AlternativeTitleRepository>();

            // MovieRepository reads profiles and titles through these; register the real ones before it is built.
            Mocker.SetConstant<IQualityProfileRepository>(_profiles);
            Mocker.SetConstant<IAlternativeTitleRepository>(_alternativeTitles);
            _translations = Mocker.Resolve<MovieTranslationRepository>();
            _credits = Mocker.Resolve<CreditRepository>();
            _collections = Mocker.Resolve<MovieCollectionRepository>();
            _files = Mocker.Resolve<MediaFileRepository>();
            _subtitles = Mocker.Resolve<SubtitleFileRepository>();
            _others = Mocker.Resolve<OtherExtraFileRepository>();
            _metadataFiles = Mocker.Resolve<MetadataFileRepository>();
            _exclusions = Mocker.Resolve<ImportListListExclusionRepository>();
            _movies = Mocker.Resolve<MovieRepository>();

            Mocker.SetConstant<ICustomFormatRepository>(customFormats);
            Mocker.SetConstant<IQualityDefinitionRepository>(Mocker.Resolve<QualityDefinitionRepository>());
            Mocker.SetConstant<IDelayProfileRepository>(Mocker.Resolve<DelayProfileRepository>());
            Mocker.SetConstant<ITagRepository>(Mocker.Resolve<TagRepository>());
            Mocker.SetConstant<IMovieRepository>(_movies);
            Mocker.SetConstant<IMovieMetadataRepository>(_metadata);
            Mocker.SetConstant<IMovieTranslationRepository>(_translations);
            Mocker.SetConstant<ICreditRepository>(_credits);
            Mocker.SetConstant<IMovieCollectionRepository>(_collections);
            Mocker.SetConstant<IMediaFileRepository>(_files);
            Mocker.SetConstant<ISubtitleFileRepository>(_subtitles);
            Mocker.SetConstant<IOtherExtraFileRepository>(_others);
            Mocker.SetConstant<IMetadataFileRepository>(_metadataFiles);
            Mocker.SetConstant<IImportListExclusionRepository>(_exclusions);
            Mocker.SetConstant<IEnumerable<IUpstreamCatalog>>(new[] { new RadarrCatalog() });
            Mocker.SetConstant<IEnumerable<IImportStage>>(new IImportStage[]
            {
                Mocker.Resolve<TagsStage>(),
                Mocker.Resolve<ProfilesStage>(),
                Mocker.Resolve<ImportExclusionsStage>(),
                Mocker.Resolve<MovieMetadataStage>(),
                Mocker.Resolve<MoviesStage>(),
                Mocker.Resolve<MovieExtrasStage>()
            });
        }

        private ImportResult Run(params string[] rows)
        {
            var common = new[]
            {
                UpstreamFixtureTest.Insert("Tags", "Id, Label", "1, 'Kids'"),
                Profile(1)
            };

            var snapshot = UpstreamFixtureTest.ReadSnapshot(
                new RadarrAdapter(),
                GetTestPath("Files/Upstream/radarr-242.schema.sql"),
                242,
                common.Concat(rows),
                TempFolder);

            return Mocker.Resolve<UpstreamImporter>().Run(new[] { snapshot }, new ImportOptions(), false);
        }

        private static string Profile(int id)
        {
            return UpstreamFixtureTest.Insert(
                "QualityProfiles",
                "Id, Name, Cutoff, Items, Language, FormatItems, UpgradeAllowed, MinFormatScore, CutoffFormatScore, MinUpgradeFormatScore",
                $"{id}, 'HD', 7, '[{{\"quality\":7,\"items\":[],\"allowed\":true}}]', 1, '[]', 1, 0, 0, 1");
        }

        private static string Metadata(int id, int tmdbId, string title = "Heat", string images = "[]", string year = "1995")
        {
            return UpstreamFixtureTest.Insert(
                "MovieMetadata",
                "Id, TmdbId, Images, Title, CleanTitle, OriginalLanguage, Status, Runtime, Recommendations, Year",
                $"{id}, {tmdbId}, {UpstreamFixtureTest.Sql(images)}, {UpstreamFixtureTest.Sql(title)}, {UpstreamFixtureTest.Sql(title.ToLowerInvariant())}, 1, 3, 170, '[]', {year}");
        }

        private static string MovieRow(int id, int metadataId, int fileId = 0, string path = "/movies/Heat (1995)", int profile = 1, string tags = "[1]")
        {
            return UpstreamFixtureTest.Insert(
                "Movies",
                "Id, Path, Monitored, QualityProfileId, Added, Tags, MovieFileId, MinimumAvailability, MovieMetadataId",
                $"{id}, {UpstreamFixtureTest.Sql(path)}, 1, {profile}, '{Stamp}', {UpstreamFixtureTest.Sql(tags)}, {fileId}, 4, {metadataId}");
        }

        private static string FileRow(int id, int movieId, string quality = Quality1080, string languages = "[1]", string mediaInfo = "NULL", string path = "Heat.1995.mkv")
        {
            return UpstreamFixtureTest.Insert(
                "MovieFiles",
                "Id, MovieId, Quality, Size, DateAdded, MediaInfo, RelativePath, Languages, IndexerFlags",
                $"{id}, {movieId}, {UpstreamFixtureTest.Sql(quality)}, 8000000000, '{Stamp}', {mediaInfo}, {UpstreamFixtureTest.Sql(path)}, {UpstreamFixtureTest.Sql(languages)}, 1");
        }

        [Test]
        public void should_import_metadata_with_remapped_language_and_utc_dates()
        {
            var result = Run(UpstreamFixtureTest.Insert(
                "MovieMetadata",
                "Id, TmdbId, ImdbId, Images, Genres, Title, CleanTitle, OriginalLanguage, Status, Runtime, InCinemas, Year, Recommendations, Popularity, CollectionTmdbId, Keywords",
                $"1, 949, 'tt0113277', '[{{\"coverType\":\"poster\",\"url\":\"https://img/p.jpg\"}}]', '[\"Crime\",\"Drama\"]', 'Heat', 'heat', 3, 3, 170, '{Stamp}', NULL, '[11,12]', 12.5, NULL, '[\"heist\"]'"));

            var metadata = _metadata.All().Single();

            metadata.TmdbId.Should().Be(949);
            metadata.ImdbId.Should().Be("tt0113277");
            metadata.Genres.Should().Equal("Crime", "Drama");
            metadata.Keywords.Should().Equal("heist");
            metadata.Recommendations.Should().Equal(11, 12);
            metadata.Images.Should().ContainSingle(i => i.Url == "https://img/p.jpg");
            metadata.OriginalLanguage.Should().Be(Language.Spanish);
            metadata.InCinemas.Should().BeCloseTo(StampUtc, TimeSpan.FromMilliseconds(1));
            metadata.InCinemas.Value.Kind.Should().Be(DateTimeKind.Utc);
            metadata.Year.Should().Be(0);
            metadata.CollectionTmdbId.Should().Be(0);
            metadata.Popularity.Should().Be(12.5f);
            result.IdMap.Resolve("Radarr", "MovieMetadata", 1).Should().Be(metadata.Id);
        }

        // Shapes read from the owner's Radarr 242 database (session 31), values replaced.
        [Test]
        public void should_read_the_document_shapes_a_live_radarr_database_holds()
        {
            const string images = "[{\"coverType\":\"poster\",\"remoteUrl\":\"https://img.example/p.jpg\"},{\"coverType\":\"fanart\",\"remoteUrl\":\"https://img.example/f.jpg\"}]";
            const string ratings = "{\"imdb\":{\"votes\":1234,\"type\":\"user\",\"value\":8.3},\"rottenTomatoes\":{\"votes\":0,\"type\":\"user\",\"value\":93}}";
            const string mediaInfo = "{\"containerFormat\":\"matroska,webm\",\"videoFormat\":\"hevc\",\"videoCodecID\":\"V_MPEGH/ISO/HEVC\",\"videoBitDepth\":10,\"videoBitrate\":8000000,\"videoColourPrimaries\":\"bt2020\",\"videoTransferCharacteristics\":\"smpte2084\",\"videoHdrFormat\":\"dolbyVisionHdr10\",\"videoFps\":23.976,\"height\":2160,\"width\":3840,\"scanType\":\"Progressive\",\"runTime\":\"01:42:10.1234567\",\"audioFormat\":\"truehd\",\"audioChannels\":8,\"audioLanguages\":[\"eng\"],\"subtitles\":[\"eng\"],\"schemaRevision\":8,\"rawStreamData\":\"{}\"}";

            var result = Run(
                UpstreamFixtureTest.Insert(
                    "MovieMetadata",
                    "Id, TmdbId, Images, Ratings, Title, CleanTitle, OriginalLanguage, Status, Runtime, Recommendations, Year",
                    $"1, 949, {UpstreamFixtureTest.Sql(images)}, {UpstreamFixtureTest.Sql(ratings)}, 'Heat', 'heat', 1, 3, 170, '[]', 1995"),
                MovieRow(1, 1, fileId: 5),
                FileRow(5, 1, mediaInfo: UpstreamFixtureTest.Sql(mediaInfo)));

            var metadata = _metadata.All().Single();

            metadata.Images.Select(i => i.CoverType).Should().Equal(MediaCover.MediaCoverTypes.Poster, MediaCover.MediaCoverTypes.Fanart);
            metadata.Images[0].RemoteUrl.Should().Be("https://img.example/p.jpg");
            metadata.Ratings.Imdb.Value.Should().Be(8.3m);
            metadata.Ratings.Imdb.Votes.Should().Be(1234);
            metadata.Ratings.RottenTomatoes.Value.Should().Be(93m);
            metadata.Ratings.Tmdb.Should().BeNull();

            var info = _files.All().Single().MediaInfo;

            info.VideoHdrFormat.Should().Be(HdrFormat.DolbyVisionHdr10);
            info.VideoFormat.Should().Be("hevc");
            info.RunTime.Should().Be(new TimeSpan(1, 42, 10).Add(TimeSpan.FromTicks(1234567)));
            info.Height.Should().Be(2160);
            info.VideoFps.Should().Be(23.976m);
            info.AudioChannels.Should().Be(8);
            result.Skips.Should().BeEmpty();
        }

        [Test]
        public void should_skip_metadata_with_malformed_json_and_everything_that_refers_to_it()
        {
            var result = Run(
                Metadata(1, 100, "Broken", images: "{not json"),
                Metadata(2, 200, "Fine"),
                UpstreamFixtureTest.Insert("AlternativeTitles", "Id, Title, CleanTitle, SourceType, MovieMetadataId", "1, 'Orphan', 'orphan', 1, 1"),
                UpstreamFixtureTest.Insert("AlternativeTitles", "Id, Title, CleanTitle, SourceType, MovieMetadataId", "2, 'Kept', 'kept', 1, 2"));

            _metadata.All().Select(m => m.Title).Should().Equal("Fine");
            result.Skips.Should().Contain(s => s.Table == "MovieMetadata" && s.SourceId == 1 && s.Name == "Broken" && s.Reason == SkipReason.Corrupt);
            result.Skips.Should().Contain(s => s.Table == "AlternativeTitles" && s.SourceId == 1 && s.Reason == SkipReason.Corrupt);
            _alternativeTitles.All().Select(t => t.Title).Should().Equal("Kept");
            result.Tables.Single(t => t.Table == "MovieMetadata").Should().Match<ReconciliationRow>(r => r.Read == 2 && r.Imported == 1 && r.Skipped == 1);
            result.Tables.Single(t => t.Table == "AlternativeTitles").Balanced.Should().BeTrue();
        }

        [Test]
        public void should_import_titles_translations_and_credits_under_the_new_metadata_id()
        {
            var result = Run(
                Metadata(1, 100),
                UpstreamFixtureTest.Insert("AlternativeTitles", "Id, Title, CleanTitle, SourceType, MovieMetadataId", "1, 'Heat 2', 'heat2', 2, 1"),
                UpstreamFixtureTest.Insert("MovieTranslations", "Id, Title, CleanTitle, Overview, Language, MovieMetadataId", "1, 'Fuego', 'fuego', 'Un robo', 3, 1"),
                UpstreamFixtureTest.Insert("Credits", "Id, CreditTmdbId, PersonTmdbId, Name, Images, Character, \"Order\", Job, Department, Type, MovieMetadataId", "1, 'c1', 1158, 'Al Pacino', '[]', 'Vincent', 0, NULL, 'Acting', 0, 1"));

            var metadataId = _metadata.All().Single().Id;

            _alternativeTitles.All().Single().Should().Match<AlternativeTitle>(t => t.MovieMetadataId == metadataId && t.Title == "Heat 2" && t.SourceType == SourceType.User);
            _translations.All().Single().Should().Match<MovieTranslation>(t => t.MovieMetadataId == metadataId && t.Language == Language.Spanish && t.Overview == "Un robo");
            _credits.All().Single().Should().Match<Credit>(c => c.MovieMetadataId == metadataId && c.Name == "Al Pacino" && c.Character == "Vincent" && c.Type == CreditType.Cast);
            result.Tables.Where(t => t.Table is "AlternativeTitles" or "MovieTranslations" or "Credits").Should().OnlyContain(t => t.Balanced);
        }

        [Test]
        public void should_allow_two_metadata_rows_to_share_an_alternative_title_clean_title()
        {
            Run(Metadata(1, 100), Metadata(2, 200, "Other"));

            var ids = _metadata.All().Select(m => m.Id).ToList();

            _alternativeTitles.InsertMany(ids.Select(id => new AlternativeTitle { Title = "Same", CleanTitle = "same", SourceType = SourceType.Tmdb, MovieMetadataId = id }).ToList());

            _alternativeTitles.All().Should().HaveCount(2);
        }

        [Test]
        public void should_import_a_collection_with_remapped_profile_tags_and_a_fallback_added_date()
        {
            var result = Run(
                UpstreamFixtureTest.Insert(
                    "Collections",
                    "Id, TmdbId, QualityProfileId, RootFolderPath, MinimumAvailability, SearchOnAdd, Title, CleanTitle, Images, Monitored, Tags",
                    "1, 1241, 1, '/movies', 4, 1, 'Harry Potter', 'harrypotter', '[]', 1, '[1,99]'"));

            var collection = _collections.All().Single();

            collection.QualityProfileId.Should().Be((int)result.IdMap.Resolve("Radarr", "QualityProfiles", 1));
            collection.Tags.Should().HaveCount(1);
            collection.Added.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
            result.Warnings.Should().ContainSingle(w => w.Contains("Harry Potter") && w.Contains("dropped 1"));
        }

        [Test]
        public void should_skip_a_collection_whose_profile_was_not_imported()
        {
            var result = Run(
                UpstreamFixtureTest.Insert(
                    "Collections",
                    "Id, TmdbId, QualityProfileId, RootFolderPath, MinimumAvailability, SearchOnAdd, Title, CleanTitle, Images, Monitored",
                    "1, 1241, 77, '/movies', 4, 1, 'Harry Potter', 'harrypotter', '[]', 1"));

            _collections.All().Should().BeEmpty();
            result.Skips.Should().ContainSingle(s => s.Table == "Collections" && s.SourceId == 1 && s.Reason == SkipReason.Corrupt);
        }

        [Test]
        public void should_import_a_movie_with_its_file_and_link_them_both_ways()
        {
            var result = Run(
                Metadata(1, 949),
                MovieRow(1, 1, fileId: 5),
                FileRow(5, 1, mediaInfo: "'{\"videoFormat\":\"HEVC\",\"audioChannels\":6}'"));

            var movie = _movies.All().Single();
            var file = _files.All().Single();

            movie.MovieFileId.Should().Be(file.Id);
            file.MovieId.Should().Be(movie.Id);
            movie.MovieMetadataId.Should().Be(_metadata.All().Single().Id);
            movie.QualityProfileId.Should().Be((int)result.IdMap.Resolve("Radarr", "QualityProfiles", 1));
            movie.Path.Should().Be("/movies/Heat (1995)");
            movie.Monitored.Should().BeTrue();
            movie.Tags.Should().HaveCount(1);
            movie.Added.Should().BeCloseTo(StampUtc, TimeSpan.FromMilliseconds(1));

            file.Quality.Quality.Should().Be(Quality.Bluray1080p);
            file.Languages.Should().Equal(Language.English);
            file.Size.Should().Be(8000000000);
            file.MediaInfo.VideoFormat.Should().Be("HEVC");
            file.RelativePath.Should().Be("Heat.1995.mkv");
            result.Tables.Where(t => t.Table is "Movies" or "MovieFiles").Should().OnlyContain(t => t.Balanced);
        }

        [Test]
        public void should_import_a_movie_without_a_file_and_keep_a_missing_media_info_null()
        {
            Run(Metadata(1, 949), MovieRow(1, 1), Metadata(2, 950, "Ronin"), MovieRow(2, 2, fileId: 6, path: "/movies/Ronin"), FileRow(6, 2));

            var movies = _movies.All().OrderBy(m => m.Id).ToList();

            movies[0].MovieFileId.Should().Be(0);
            movies[1].MovieFileId.Should().BeGreaterThan(0);
            _files.All().Single().MediaInfo.Should().BeNull();
        }

        [Test]
        public void should_skip_a_movie_whose_profile_was_not_imported_and_name_it_by_its_title()
        {
            var result = Run(Metadata(1, 949), MovieRow(1, 1, profile: 77), FileRow(5, 1));

            _movies.All().Should().BeEmpty();
            result.Skips.Should().Contain(s => s.Table == "Movies" && s.SourceId == 1 && s.Name == "Heat" && s.Reason == SkipReason.Corrupt);
            result.Skips.Should().Contain(s => s.Table == "MovieFiles" && s.SourceId == 5 && s.Reason == SkipReason.Corrupt);
            _files.All().Should().BeEmpty();
        }

        [Test]
        public void should_skip_a_movie_whose_metadata_was_not_imported()
        {
            var result = Run(Metadata(1, 949, images: "{not json"), MovieRow(1, 1));

            _movies.All().Should().BeEmpty();
            result.Skips.Should().Contain(s => s.Table == "Movies" && s.SourceId == 1 && s.Reason == SkipReason.Corrupt);
        }

        [Test]
        public void should_skip_a_movie_with_no_path()
        {
            var result = Run(Metadata(1, 949), MovieRow(1, 1, path: ""));

            _movies.All().Should().BeEmpty();
            result.Skips.Should().ContainSingle(s => s.Table == "Movies" && s.Reason == SkipReason.Corrupt);
        }

        [Test]
        public void should_import_a_movie_without_a_file_when_the_file_was_skipped_without_a_warning()
        {
            var result = Run(Metadata(1, 949), MovieRow(1, 1, fileId: 5), FileRow(5, 1, quality: "{not json"));

            _movies.All().Single().MovieFileId.Should().Be(0);
            _files.All().Should().BeEmpty();
            result.Skips.Should().ContainSingle(s => s.Table == "MovieFiles" && s.SourceId == 5 && s.Reason == SkipReason.Corrupt);
            result.Warnings.Should().NotContain(w => w.Contains("not in Radarr's database"));
        }

        [Test]
        public void should_warn_and_import_without_a_file_when_the_movie_points_at_a_file_that_does_not_exist()
        {
            var result = Run(Metadata(1, 949), MovieRow(1, 1, fileId: 42));

            _movies.All().Single().MovieFileId.Should().Be(0);
            result.Warnings.Should().ContainSingle(w => w.Contains("not in Radarr's database"));
        }

        [Test]
        public void should_import_extra_files_under_the_new_movie_and_file_ids()
        {
            Run(
                Metadata(1, 949),
                MovieRow(1, 1, fileId: 5),
                FileRow(5, 1),
                UpstreamFixtureTest.Insert("SubtitleFiles", "Id, MovieId, MovieFileId, RelativePath, Extension, Added, LastUpdated, Language, LanguageTags, Title, Copy", $"1, 1, 5, 'Heat.es.srt', '.srt', '{Stamp}', '{Stamp}', 3, '[\"forced\"]', 'Forced', 2"),
                UpstreamFixtureTest.Insert("SubtitleFiles", "Id, MovieId, MovieFileId, RelativePath, Extension, Added, LastUpdated, Language", $"2, 1, 0, 'Loose.en.srt', '.srt', '{Stamp}', '{Stamp}', 1"),
                UpstreamFixtureTest.Insert("ExtraFiles", "Id, MovieId, MovieFileId, RelativePath, Extension, Added, LastUpdated", $"1, 1, 5, 'Heat.txt', '.txt', '{Stamp}', '{Stamp}'"),
                UpstreamFixtureTest.Insert("MetadataFiles", "Id, MovieId, Consumer, Type, RelativePath, LastUpdated, MovieFileId, Hash, Added, Extension", $"1, 1, 'XbmcMetadata', 1, 'Heat.nfo', '{Stamp}', NULL, 'abc', NULL, '.nfo'"));

            var movieId = _movies.All().Single().Id;
            var fileId = _files.All().Single().Id;
            var subtitles = _subtitles.All().OrderBy(s => s.Id).ToList();

            subtitles[0].Should().Match<SubtitleFile>(s => s.MovieId == movieId && s.MovieFileId == fileId && s.Language == Language.Spanish && s.Copy == 2 && s.Title == "Forced");
            subtitles[0].LanguageTags.Should().Equal("forced");
            subtitles[1].MovieFileId.Should().Be(0);
            _others.All().Single().Should().Match<OtherExtraFile>(f => f.MovieId == movieId && f.MovieFileId == fileId && f.RelativePath == "Heat.txt");

            var metadataFile = _metadataFiles.All().Single();

            metadataFile.MovieFileId.Should().BeNull();
            metadataFile.Consumer.Should().Be("XbmcMetadata");
            metadataFile.Type.Should().Be(MetadataType.MovieMetadata);
            metadataFile.Hash.Should().Be("abc");
            metadataFile.Added.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        }

        [Test]
        public void should_skip_extra_files_that_refer_to_a_movie_or_file_that_was_not_imported()
        {
            var result = Run(
                Metadata(1, 949),
                MovieRow(1, 1, fileId: 5),
                FileRow(5, 1, quality: "{not json"),
                UpstreamFixtureTest.Insert("ExtraFiles", "Id, MovieId, MovieFileId, RelativePath, Extension, Added, LastUpdated", $"1, 1, 5, 'Dangling.txt', '.txt', '{Stamp}', '{Stamp}'"),
                UpstreamFixtureTest.Insert("ExtraFiles", "Id, MovieId, MovieFileId, RelativePath, Extension, Added, LastUpdated", $"2, 9, 0, 'Nomovie.txt', '.txt', '{Stamp}', '{Stamp}'"));

            _others.All().Should().BeEmpty();
            result.Skips.Count(s => s.Table == "ExtraFiles" && s.Reason == SkipReason.Corrupt).Should().Be(2);
            result.Tables.Single(t => t.Table == "ExtraFiles").Balanced.Should().BeTrue();
        }

        [Test]
        public void should_import_exclusions_and_default_a_missing_year_to_zero()
        {
            var result = Run(
                UpstreamFixtureTest.Insert("ImportExclusions", "Id, TmdbId, MovieTitle, MovieYear", "1, 949, 'Heat', 1995"),
                UpstreamFixtureTest.Insert("ImportExclusions", "Id, TmdbId, MovieTitle, MovieYear", "2, 950, 'Ronin', NULL"));

            _exclusions.All().OrderBy(e => e.TmdbId).Select(e => (e.TmdbId, e.MovieTitle, e.MovieYear))
                .Should().Equal((949, "Heat", 1995), (950, "Ronin", 0));
            result.Tables.Single(t => t.Table == "ImportExclusions").Balanced.Should().BeTrue();
        }
    }
}
