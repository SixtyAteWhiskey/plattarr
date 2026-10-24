using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Parser;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Search;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.SearchTests
{
    [TestFixture]
    public class MusicReleaseMatcherFixture : CoreTest<MusicReleaseMatcher>
    {
        private const string Title = "Coldplay-A Head Full Of Dreams-CD-FLAC-2015-PERFECT";

        private ReleaseInfo _release;
        private Artist _artist;
        private Album _album;

        [SetUp]
        public void SetUp()
        {
            _release = new ReleaseInfo { Title = Title };
            _artist = new Artist { Id = 1, Metadata = new ArtistMetadata { Name = "Coldplay" } };
            _album = new Album { Id = 2, ArtistId = 1, Title = "A Head Full Of Dreams" };
        }

        private void GivenMapping(RemoteAlbum remote)
        {
            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.Map(It.IsAny<ParsedAlbumInfo>(), It.IsAny<MusicSearchRequest>()))
                  .Returns(remote);
        }

        [Test]
        public void should_match_music()
        {
            Subject.Kind.Should().Be(MediaKind.Music);
        }

        [Test]
        public void should_map_the_parsed_title_and_attach_the_release()
        {
            var remote = new RemoteAlbum { Artist = _artist, Albums = new List<Album> { _album } };
            GivenMapping(remote);

            var result = Subject.Map(_release, null);

            result.Should().BeSameAs(remote);
            result.Release.Should().BeSameAs(_release);

            Mocker.GetMock<IParsingService>()
                  .Verify(s => s.Map(It.Is<ParsedAlbumInfo>(p => p.ArtistName == "Coldplay"), null), Times.Once());
        }

        [Test]
        public void should_return_null_when_the_title_has_no_artist_name()
        {
            _release.Title = "nonsense";

            Subject.Map(_release, null).Should().BeNull();

            Mocker.GetMock<IParsingService>()
                  .Verify(s => s.Map(It.IsAny<ParsedAlbumInfo>(), It.IsAny<MusicSearchRequest>()), Times.Never());
        }

        [Test]
        public void should_return_an_unmatched_release_when_the_artist_is_unknown()
        {
            GivenMapping(new RemoteAlbum { Artist = null, Albums = new List<Album>() });

            var result = Subject.Map(_release, null);

            result.Artist.Should().BeNull();
            result.Release.Should().BeSameAs(_release);

            Mocker.GetMock<IParsingService>()
                  .Verify(s => s.Map(It.IsAny<ParsedAlbumInfo>(), It.IsAny<MusicSearchRequest>()), Times.Once());
        }

        [Test]
        public void should_parse_again_with_the_search_criteria_when_the_first_map_finds_no_artist()
        {
            var request = new AlbumSearchRequest { Artist = _artist, Albums = new List<Album> { _album }, AlbumTitle = _album.Title };
            var unmatched = new RemoteAlbum { Artist = null, Albums = new List<Album>() };
            var matched = new RemoteAlbum { Artist = _artist, Albums = new List<Album> { _album } };

            Mocker.GetMock<IParsingService>()
                  .SetupSequence(s => s.Map(It.IsAny<ParsedAlbumInfo>(), request))
                  .Returns(unmatched)
                  .Returns(matched);

            var result = Subject.Map(_release, request);

            result.Should().BeSameAs(matched);
            result.Release.Should().BeSameAs(_release);

            Mocker.GetMock<IParsingService>()
                  .Verify(s => s.Map(It.IsAny<ParsedAlbumInfo>(), request), Times.Exactly(2));
        }

        [Test]
        public void should_not_parse_again_without_a_search()
        {
            GivenMapping(new RemoteAlbum { Artist = null, Albums = new List<Album>() });

            Subject.Map(_release, null);

            Mocker.GetMock<IParsingService>()
                  .Verify(s => s.Map(It.IsAny<ParsedAlbumInfo>(), It.IsAny<MusicSearchRequest>()), Times.Once());
        }

        [Test]
        public void should_parse_the_title()
        {
            Subject.Parse(_release, null).ArtistName.Should().Be("Coldplay");
        }

        [Test]
        public void should_read_a_title_the_parser_cannot_read_against_the_search()
        {
            _release.Title = "nonsense";

            Subject.Parse(_release, null).Should().BeNull();
        }

        [Test]
        public void should_map_through_the_module_contract_with_a_music_request()
        {
            var remote = new RemoteAlbum { Artist = _artist, Albums = new List<Album> { _album } };
            GivenMapping(remote);

            ((IReleaseMatcher)Subject).Map(_release, new AlbumSearchRequest { Artist = _artist, Albums = new List<Album> { _album } })
                                      .Should().BeSameAs(remote);
        }
    }
}
