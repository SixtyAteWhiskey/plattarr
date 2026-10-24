using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Blocklisting;
using NzbDrone.Core.Music.DecisionEngine.Specifications;
using NzbDrone.Core.Music.DecisionEngine.Specifications.Search;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Search;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.DecisionEngineTests
{
    [TestFixture]
    public class MusicBlocklistSpecificationFixture : CoreTest<BlocklistSpecification>
    {
        private RemoteAlbum _remoteAlbum;

        [SetUp]
        public void Setup()
        {
            _remoteAlbum = new RemoteAlbum
            {
                Artist = new Artist { Id = 3 },
                Albums = new List<Album> { new Album { Id = 5 } },
                Release = new ReleaseInfo { Title = "Artist - Album [FLAC]" }
            };
        }

        [Test]
        public void should_accept_a_release_that_is_not_blocklisted()
        {
            Mocker.GetMock<IBlocklistService>()
                  .Setup(s => s.Blocklisted(3, _remoteAlbum.Release))
                  .Returns(false);

            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_a_blocklisted_release_with_the_blocklisted_reason()
        {
            Mocker.GetMock<IBlocklistService>()
                  .Setup(s => s.Blocklisted(3, _remoteAlbum.Release))
                  .Returns(true);

            var decision = Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation());

            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(DownloadRejectionReason.Blocklisted);
        }
    }

    [TestFixture]
    public class AlbumRequestedSpecificationFixture : CoreTest<AlbumRequestedSpecification>
    {
        private RemoteAlbum _remoteAlbum;

        [SetUp]
        public void Setup()
        {
            _remoteAlbum = new RemoteAlbum
            {
                Albums = new List<Album> { new Album { Id = 5 }, new Album { Id = 6 } },
                ParsedAlbumInfo = new ParsedAlbumInfo { AlbumTitle = "Album" }
            };
        }

        [Test]
        public void should_accept_when_there_is_no_search()
        {
            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_when_one_of_the_albums_was_requested()
        {
            var search = new AlbumSearchRequest { Albums = new List<Album> { new Album { Id = 6 } } };

            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation(false, search)).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_with_the_not_requested_reason_when_none_of_the_albums_was_requested()
        {
            var search = new AlbumSearchRequest { Albums = new List<Album> { new Album { Id = 9 } } };

            var decision = Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation(false, search));

            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(DownloadRejectionReason.AlbumNotRequested);
        }
    }

    [TestFixture]
    public class SingleAlbumSearchMatchSpecificationFixture : CoreTest<SingleAlbumSearchMatchSpecification>
    {
        private RemoteAlbum _remoteAlbum;

        [SetUp]
        public void Setup()
        {
            _remoteAlbum = new RemoteAlbum
            {
                ParsedAlbumInfo = new ParsedAlbumInfo { ArtistName = "Artist", AlbumTitle = "Some Album" }
            };
        }

        [Test]
        public void should_accept_when_the_search_is_for_an_artist()
        {
            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation(false, new ArtistSearchRequest())).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_when_the_titles_match_after_cleaning()
        {
            var search = new AlbumSearchRequest { AlbumTitle = "Some  Album!" };

            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation(false, search)).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_with_the_wrong_album_reason_when_the_title_differs()
        {
            var search = new AlbumSearchRequest { AlbumTitle = "Another Album" };

            var decision = Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation(false, search));

            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(DownloadRejectionReason.WrongAlbum);
        }

        [Test]
        public void should_reject_a_release_without_an_album_title()
        {
            _remoteAlbum.ParsedAlbumInfo.AlbumTitle = string.Empty;

            var decision = Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation(false, new AlbumSearchRequest { AlbumTitle = string.Empty }));

            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(DownloadRejectionReason.FullArtistPack);
        }
    }
}
