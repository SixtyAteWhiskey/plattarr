using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv.Extras.Metadata;
using NzbDrone.Core.Tv.Extras.Metadata.Consumers.Xbmc;
using NzbDrone.Core.Tv.Extras.Metadata.Files;
using NzbDrone.Core.Tv.Extras.Others;
using NzbDrone.Core.Tv.Extras.Subtitles;

namespace NzbDrone.Core.Test.TvTests.Extras
{
    // Round trips through the Tv_ extras tables: the table map and migration 006 have to agree on
    // every column, and the series-scoped queries have to find only that series' rows.
    [TestFixture]
    public class TvMetadataFileRepositoryFixture : DbTest<MetadataFileRepository, MetadataFile>
    {
        [Test]
        public void should_round_trip_a_metadata_file()
        {
            var added = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

            Subject.Insert(new MetadataFile
            {
                SeriesId = 7,
                SeasonNumber = 2,
                EpisodeFileId = 31,
                RelativePath = "Season 02/ep.nfo",
                Extension = ".nfo",
                Consumer = "XbmcMetadata",
                Type = MetadataType.EpisodeMetadata,
                Hash = "abc123",
                Added = added,
                LastUpdated = added
            });

            var stored = Subject.GetFilesBySeries(7).Single();

            stored.SeasonNumber.Should().Be(2);
            stored.EpisodeFileId.Should().Be(31);
            stored.RelativePath.Should().Be("Season 02/ep.nfo");
            stored.Consumer.Should().Be("XbmcMetadata");
            stored.Type.Should().Be(MetadataType.EpisodeMetadata);
            stored.Hash.Should().Be("abc123");
        }

        [Test]
        public void should_scope_queries_to_the_series_season_and_episode_file()
        {
            Subject.Insert(new MetadataFile { SeriesId = 1, SeasonNumber = 1, EpisodeFileId = 10, RelativePath = "a.nfo", Extension = ".nfo", Consumer = "x", LastUpdated = DateTime.UtcNow });
            Subject.Insert(new MetadataFile { SeriesId = 1, SeasonNumber = 2, EpisodeFileId = 11, RelativePath = "b.nfo", Extension = ".nfo", Consumer = "x", LastUpdated = DateTime.UtcNow });
            Subject.Insert(new MetadataFile { SeriesId = 2, SeasonNumber = 1, EpisodeFileId = 12, RelativePath = "a.nfo", Extension = ".nfo", Consumer = "x", LastUpdated = DateTime.UtcNow });

            Subject.GetFilesBySeries(1).Should().HaveCount(2);
            Subject.GetFilesBySeason(1, 2).Should().ContainSingle().Which.RelativePath.Should().Be("b.nfo");
            Subject.GetFilesByEpisodeFile(12).Should().ContainSingle().Which.SeriesId.Should().Be(2);
            Subject.FindByPath(2, "a.nfo").SeriesId.Should().Be(2);
        }

        [Test]
        public void should_delete_for_series_season_and_episode_file()
        {
            Subject.Insert(new MetadataFile { SeriesId = 1, SeasonNumber = 1, EpisodeFileId = 10, RelativePath = "a.nfo", Extension = ".nfo", Consumer = "x", LastUpdated = DateTime.UtcNow });
            Subject.Insert(new MetadataFile { SeriesId = 1, SeasonNumber = 2, EpisodeFileId = 11, RelativePath = "b.nfo", Extension = ".nfo", Consumer = "x", LastUpdated = DateTime.UtcNow });
            Subject.Insert(new MetadataFile { SeriesId = 3, SeasonNumber = 1, EpisodeFileId = 12, RelativePath = "c.nfo", Extension = ".nfo", Consumer = "x", LastUpdated = DateTime.UtcNow });

            Subject.DeleteForEpisodeFile(10);
            Subject.GetFilesBySeries(1).Should().ContainSingle().Which.RelativePath.Should().Be("b.nfo");

            Subject.DeleteForSeason(1, 2);
            Subject.GetFilesBySeries(1).Should().BeEmpty();

            Subject.DeleteForSeriesIds(new List<int> { 3 });
            Subject.All().Should().BeEmpty();
        }
    }

    [TestFixture]
    public class TvSubtitleFileRepositoryFixture : DbTest<SubtitleFileRepository, SubtitleFile>
    {
        [Test]
        public void should_round_trip_a_subtitle_file_with_its_language_and_tags()
        {
            Subject.Insert(new SubtitleFile
            {
                SeriesId = 7,
                SeasonNumber = 1,
                EpisodeFileId = 31,
                RelativePath = "Season 01/ep.en.forced.srt",
                Extension = ".srt",
                Language = Language.English,
                LanguageTags = new List<string> { "forced" },
                Title = "Forced",
                Copy = 2,
                Added = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow
            });

            var stored = Subject.GetFilesByEpisodeFile(31).Single();

            stored.Language.Should().Be(Language.English);
            stored.LanguageTags.Should().Equal("forced");
            stored.Title.Should().Be("Forced");
            stored.Copy.Should().Be(2);
        }
    }

    [TestFixture]
    public class TvOtherExtraFileRepositoryFixture : DbTest<OtherExtraFileRepository, OtherExtraFile>
    {
        [Test]
        public void should_round_trip_an_extra_file()
        {
            Subject.Insert(new OtherExtraFile
            {
                SeriesId = 7,
                EpisodeFileId = 31,
                RelativePath = "Season 01/ep.jpg",
                Extension = ".jpg",
                Added = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow
            });

            var stored = Subject.GetFilesBySeries(7).Single();

            stored.EpisodeFileId.Should().Be(31);
            stored.SeasonNumber.Should().BeNull();
            stored.Extension.Should().Be(".jpg");
        }
    }

    [TestFixture]
    public class TvMetadataRepositoryFixture : DbTest<MetadataRepository, MetadataDefinition>
    {
        [Test]
        public void should_round_trip_a_metadata_consumer_definition()
        {
            Subject.Insert(new MetadataDefinition
            {
                Name = "Kodi (XBMC) / Emby",
                Implementation = "XbmcMetadata",
                ConfigContract = "XbmcMetadataSettings",
                Enable = true,
                Settings = new XbmcMetadataSettings { SeriesMetadata = true }
            });

            var stored = Subject.All().Single();

            stored.Name.Should().Be("Kodi (XBMC) / Emby");
            stored.Implementation.Should().Be("XbmcMetadata");
            stored.Enable.Should().BeTrue();
        }
    }
}
