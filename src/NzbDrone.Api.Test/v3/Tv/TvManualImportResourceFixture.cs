using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Crypto;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.MediaFiles.EpisodeImport;
using NzbDrone.Core.Tv.MediaFiles.EpisodeImport.Manual;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Tv.ManualImport;

namespace NzbDrone.Api.Test.v3.Tv
{
    [TestFixture]
    public class TvManualImportResourceFixture : TestBase
    {
        private ManualImportItem _item;
        private CustomFormat _format;

        [SetUp]
        public void Setup()
        {
            _format = new CustomFormat("Test") { Id = 7 };

            var profile = new QualityProfile
            {
                Name = "Test",
                Items = new List<QualityProfileQualityItem>(),
                FormatItems = new List<ProfileFormatItem> { new ProfileFormatItem { Format = _format, Score = 40 } }
            };

            _item = new ManualImportItem
            {
                Path = "/downloads/Show.S01E01.1080p.WEB-DL-GRP/Show.S01E01.1080p.WEB-DL-GRP.mkv",
                RelativePath = "Show.S01E01.1080p.WEB-DL-GRP.mkv",
                FolderName = "Show.S01E01.1080p.WEB-DL-GRP",
                Name = "Show.S01E01.1080p.WEB-DL-GRP",
                Size = 1234,
                Series = new Series { Id = 3, Title = "Show", QualityProfileId = 1, QualityProfile = new LazyLoaded<QualityProfile>(profile) },
                SeasonNumber = 1,
                Episodes = new List<Episode> { new Episode { Id = 40, SeriesId = 3, SeasonNumber = 1, EpisodeNumber = 1 } },
                ReleaseGroup = "GRP",
                Quality = new QualityModel(Quality.WEBDL1080p),
                Languages = new List<Language> { Language.English },
                DownloadId = "abc",
                CustomFormats = new List<CustomFormat> { _format },
                IndexerFlags = 1,
                ReleaseType = ReleaseType.SingleEpisode,
                Rejections = new List<ImportRejection> { new ImportRejection(ImportRejectionReason.NotQualityUpgrade, "Not an upgrade", RejectionType.Temporary) }
            };
        }

        [Test]
        public void should_map_the_file_series_and_episodes()
        {
            var resource = _item.ToResource();

            resource.Path.Should().Be(_item.Path);
            resource.RelativePath.Should().Be(_item.RelativePath);
            resource.FolderName.Should().Be(_item.FolderName);
            resource.Name.Should().Be(_item.Name);
            resource.Size.Should().Be(1234);
            resource.Series.Title.Should().Be("Show");
            resource.SeasonNumber.Should().Be(1);
            resource.Episodes.Should().ContainSingle().Which.EpisodeNumber.Should().Be(1);
            resource.ReleaseGroup.Should().Be("GRP");
            resource.Quality.Quality.Should().Be(Quality.WEBDL1080p);
            resource.Languages.Should().Equal(Language.English);
            resource.DownloadId.Should().Be("abc");
            resource.IndexerFlags.Should().Be(1);
            resource.ReleaseType.Should().Be(ReleaseType.SingleEpisode);
        }

        [Test]
        public void should_identify_a_file_by_a_hash_of_its_path()
        {
            _item.ToResource().Id.Should().Be(HashConverter.GetHashInt31(_item.Path));

            var other = new ManualImportItem { Path = "/downloads/other.mkv", Rejections = new List<ImportRejection>() };

            other.ToResource().Id.Should().NotBe(_item.ToResource().Id);
        }

        [Test]
        public void should_score_the_custom_formats_with_the_series_quality_profile()
        {
            var resource = _item.ToResource();

            resource.CustomFormatScore.Should().Be(40);
            resource.CustomFormats.Should().ContainSingle(f => f.Name == "Test");
        }

        [Test]
        public void should_not_score_a_file_whose_series_is_unknown()
        {
            _item.Series = null;
            _item.Episodes = null;

            var resource = _item.ToResource();

            resource.Series.Should().BeNull();
            resource.CustomFormatScore.Should().Be(0);
        }

        [Test]
        public void should_map_a_rejection_to_its_message_and_type()
        {
            var rejection = _item.ToResource().Rejections;

            rejection.Should().ContainSingle().Which.Should().BeEquivalentTo(new TvImportRejectionResource { Reason = "Not an upgrade", Type = RejectionType.Temporary });
        }

        [Test]
        public void should_map_nothing_to_null()
        {
            ((ManualImportItem)null).ToResource().Should().BeNull();
            ((ImportRejection)null).ToResource().Should().BeNull();
        }
    }
}
