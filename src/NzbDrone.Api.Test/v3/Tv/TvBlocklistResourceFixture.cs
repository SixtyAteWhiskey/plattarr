using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.CustomFormats;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Tv.Blocklist;

namespace NzbDrone.Api.Test.v3.Tv
{
    [TestFixture]
    public class TvBlocklistResourceFixture : TestBase
    {
        private Series _series;
        private NzbDrone.Core.Tv.Blocklisting.Blocklist _blocklist;
        private Mock<ITvCustomFormatCalculationService> _formatCalculator;

        [SetUp]
        public void Setup()
        {
            _series = new Series { Id = 12, Title = "Series" };

            _blocklist = new NzbDrone.Core.Tv.Blocklisting.Blocklist
            {
                Id = 5,
                MediaKind = MediaKind.Tv,
                ParentId = 12,
                ItemIds = new List<int> { 101, 102 },
                Series = _series,
                SourceTitle = "Series.S01E01E02.1080p.WEB-DL.x264-GROUP",
                Quality = new QualityModel(Quality.WEBDL1080p),
                Date = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                Protocol = DownloadProtocol.Torrent,
                Indexer = "Indexer",
                Message = "Failed"
            };

            _formatCalculator = new Mock<ITvCustomFormatCalculationService>();
            _formatCalculator.Setup(c => c.ParseCustomFormat(It.IsAny<NzbDrone.Core.Tv.Blocklisting.Blocklist>(), It.IsAny<Series>()))
                             .Returns(new List<CustomFormat> { new CustomFormat("Test") { Id = 7 } });
        }

        [Test]
        public void should_map_the_series_and_episodes_from_the_shared_blocklist_columns()
        {
            var resource = _blocklist.MapToResource(_formatCalculator.Object);

            resource.Id.Should().Be(5);
            resource.SeriesId.Should().Be(12);
            resource.EpisodeIds.Should().Equal(101, 102);
            resource.Series.Title.Should().Be("Series");
        }

        [Test]
        public void should_map_the_release_fields_and_the_custom_formats()
        {
            var resource = _blocklist.MapToResource(_formatCalculator.Object);

            resource.SourceTitle.Should().Be(_blocklist.SourceTitle);
            resource.Quality.Quality.Should().Be(Quality.WEBDL1080p);
            resource.Protocol.Should().Be(DownloadProtocol.Torrent);
            resource.Indexer.Should().Be("Indexer");
            resource.Message.Should().Be("Failed");
            resource.CustomFormats.Should().ContainSingle(f => f.Name == "Test");
        }

        [Test]
        public void should_return_null_for_a_missing_row()
        {
            ((NzbDrone.Core.Tv.Blocklisting.Blocklist)null).MapToResource(_formatCalculator.Object).Should().BeNull();
        }
    }
}
