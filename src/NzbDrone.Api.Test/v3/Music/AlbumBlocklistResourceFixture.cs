using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Blocklisting;
using NzbDrone.Core.Music.CustomFormats;
using NzbDrone.Core.Qualities;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Music.Blocklist;

namespace NzbDrone.Api.Test.v3.Music
{
    [TestFixture]
    public class AlbumBlocklistResourceFixture : TestBase
    {
        private Artist _artist;
        private Blocklist _blocklist;
        private Mock<IMusicCustomFormatCalculationService> _formatCalculator;

        [SetUp]
        public void Setup()
        {
            _artist = new Artist
            {
                Id = 12,
                Metadata = new ArtistMetadata { Name = "The Black Keys", Images = new List<NzbDrone.Core.MediaCover.MediaCover>() },
                Tags = new HashSet<int>()
            };

            _blocklist = new Blocklist
            {
                Id = 5,
                MediaKind = MediaKind.Music,
                ParentId = 12,
                ItemIds = new List<int> { 101, 102 },
                Artist = _artist,
                SourceTitle = "The Black Keys - Brothers [FLAC]",
                Quality = new AudioQualityModel(AudioQuality.FLAC),
                Date = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                Protocol = DownloadProtocol.Torrent,
                Indexer = "Indexer",
                Message = "Failed"
            };

            _formatCalculator = new Mock<IMusicCustomFormatCalculationService>();
            _formatCalculator.Setup(c => c.ParseCustomFormat(It.IsAny<Blocklist>(), It.IsAny<Artist>()))
                             .Returns(new List<CustomFormat> { new CustomFormat("Test") { Id = 7 } });
        }

        [Test]
        public void should_map_the_artist_and_albums_from_the_shared_blocklist_columns()
        {
            var resource = _blocklist.MapToResource(_formatCalculator.Object);

            resource.Id.Should().Be(5);
            resource.ArtistId.Should().Be(12);
            resource.AlbumIds.Should().Equal(101, 102);
            resource.Artist.ArtistName.Should().Be("The Black Keys");
        }

        [Test]
        public void should_map_the_release_fields_and_the_custom_formats()
        {
            var resource = _blocklist.MapToResource(_formatCalculator.Object);

            resource.SourceTitle.Should().Be(_blocklist.SourceTitle);
            resource.Quality.Quality.Should().Be(AudioQuality.FLAC);
            resource.Protocol.Should().Be(DownloadProtocol.Torrent);
            resource.Indexer.Should().Be("Indexer");
            resource.Message.Should().Be("Failed");
            resource.CustomFormats.Should().ContainSingle(f => f.Name == "Test");
        }

        [Test]
        public void should_return_null_for_a_missing_row()
        {
            ((Blocklist)null).MapToResource(_formatCalculator.Object).Should().BeNull();
        }
    }
}
