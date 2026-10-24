using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.DecisionEngine.Specifications;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.DecisionEngineTests
{
    [TestFixture]
    public class DiscographySpecificationFixture : CoreTest<DiscographySpecification>
    {
        private RemoteAlbum _remoteAlbum;

        [SetUp]
        public void Setup()
        {
            var artist = Builder<Artist>.CreateNew().With(s => s.Id = 1234).Build();
            _remoteAlbum = new RemoteAlbum
            {
                ParsedAlbumInfo = new ParsedAlbumInfo
                {
                    Discography = true
                },
                Albums = Builder<Album>.CreateListOfSize(3)
                                           .All()
                                           .With(e => e.ReleaseDate = DateTime.UtcNow.AddDays(-8))
                                           .With(s => s.ArtistId = artist.Id)
                                           .BuildList(),
                Artist = artist,
                Release = new ReleaseInfo
                {
                    Title = "Artist.Discography.1978.2005.FLAC-RlsGrp"
                }
            };

            Mocker.GetMock<IAlbumService>().Setup(s => s.AlbumsBetweenDates(It.IsAny<DateTime>(), It.IsAny<DateTime>(), false))
                                             .Returns(new List<Album>());
        }

        [Test]
        public void should_return_true_if_is_not_a_discography()
        {
            _remoteAlbum.ParsedAlbumInfo.Discography = false;
            _remoteAlbum.Albums.Last().ReleaseDate = DateTime.UtcNow.AddDays(+2);
            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_true_if_all_albums_have_released()
        {
            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_true_if_all_albums_will_have_released_in_the_next_24_hours()
        {
            _remoteAlbum.Albums.Last().ReleaseDate = DateTime.UtcNow.AddHours(23);

            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_false_if_one_album_has_not_released()
        {
            _remoteAlbum.Albums.Last().ReleaseDate = DateTime.UtcNow.AddDays(+2);
            var rejection = Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation());
            rejection.Accepted.Should().BeFalse();
            rejection.Reason.Should().Be(DownloadRejectionReason.DiscographyNotReleased);
        }

        [Test]
        public void should_return_false_if_an_album_does_not_have_an_release_date()
        {
            _remoteAlbum.Albums.Last().ReleaseDate = null;
            var rejection = Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation());
            rejection.Accepted.Should().BeFalse();
            rejection.Reason.Should().Be(DownloadRejectionReason.DiscographyNotReleased);
        }
    }
}
