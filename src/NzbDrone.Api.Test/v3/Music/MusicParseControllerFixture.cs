using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Aggregation;
using NzbDrone.Core.Music.CustomFormats;
using NzbDrone.Core.Music.Parser;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Search;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Music.Parse;

namespace NzbDrone.Api.Test.v3.Music
{
    [TestFixture]
    public class MusicParseControllerFixture : TestBase<MusicParseController>
    {
        private const string Title = "The Black Keys - Brothers (2010) [FLAC]";

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void should_return_nothing_for_an_empty_title(string title)
        {
            Subject.Parse(title).Should().BeNull();
        }

        [Test]
        public void should_return_only_the_title_when_it_does_not_parse()
        {
            var result = Subject.Parse("not a release");

            result.Title.Should().Be("not a release");
            result.ParsedAlbumInfo.Should().BeNull();
            result.Artist.Should().BeNull();
        }

        [Test]
        public void should_return_the_parsed_info_when_no_artist_is_known()
        {
            Mocker.GetMock<IParsingService>().Setup(s => s.Map(It.IsAny<ParsedAlbumInfo>(), It.IsAny<MusicSearchRequest>())).Returns((RemoteAlbum)null);

            var result = Subject.Parse(Title);

            result.ParsedAlbumInfo.ArtistName.Should().Be("The Black Keys");
            result.Artist.Should().BeNull();
            result.Albums.Should().BeNull();
        }

        [Test]
        public void should_map_the_artist_albums_and_custom_format_score_when_the_release_matches()
        {
            var artist = new Artist
            {
                Id = 12,
                Metadata = new ArtistMetadata { Name = "The Black Keys", Images = new List<NzbDrone.Core.MediaCover.MediaCover>() },
                Tags = new HashSet<int>()
            };

            var remote = new RemoteAlbum
            {
                Artist = artist,
                Albums = new List<Album>
                {
                    new Album { Id = 101, Title = "Brothers", Images = new List<NzbDrone.Core.MediaCover.MediaCover>(), SecondaryTypes = new List<SecondaryAlbumType>(), Genres = new List<string>() }
                },
                ParsedAlbumInfo = new ParsedAlbumInfo { ArtistName = "The Black Keys", AlbumTitle = "Brothers" }
            };

            Mocker.GetMock<IParsingService>().Setup(s => s.Map(It.IsAny<ParsedAlbumInfo>(), It.IsAny<MusicSearchRequest>())).Returns(remote);
            Mocker.GetMock<IMusicCustomFormatCalculationService>()
                  .Setup(c => c.ParseCustomFormat(remote, 0))
                  .Returns(new List<CustomFormat> { new CustomFormat("Test") { Id = 7 } });

            var result = Subject.Parse(Title);

            result.Artist.ArtistName.Should().Be("The Black Keys");
            result.Albums.Should().ContainSingle(a => a.Id == 101);
            result.ParsedAlbumInfo.AlbumTitle.Should().Be("Brothers");
            result.CustomFormats.Should().ContainSingle(f => f.Name == "Test");
            Mocker.GetMock<IRemoteAlbumAggregationService>().Verify(s => s.Augment(remote), Times.Once());
        }
    }
}
