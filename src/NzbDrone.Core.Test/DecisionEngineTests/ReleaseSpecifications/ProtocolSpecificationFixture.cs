using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine.ReleaseSpecifications;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.DecisionEngineTests.ReleaseSpecifications
{
    [TestFixture]
    public class ProtocolSpecificationFixture : CoreTest<ProtocolSpecification>
    {
        private RemoteMovie _remoteMovie;
        private DelayProfile _delayProfile;

        [SetUp]
        public void Setup()
        {
            _remoteMovie = new RemoteMovie();
            _remoteMovie.Release = new ReleaseInfo();
            _remoteMovie.Movie = new Movie();

            _delayProfile = new DelayProfile();

            Mocker.GetMock<IDelayProfileService>()
                  .Setup(s => s.BestForTags(It.IsAny<HashSet<int>>()))
                  .Returns(_delayProfile);
        }

        private void GivenProtocol(DownloadProtocol downloadProtocol)
        {
            _remoteMovie.Release.DownloadProtocol = downloadProtocol;
        }

        [Test]
        public void should_be_true_if_usenet_and_usenet_is_enabled()
        {
            GivenProtocol(DownloadProtocol.Usenet);
            _delayProfile.EnableUsenet = true;

            Subject.IsSatisfiedBy(_remoteMovie, new()).Accepted.Should().Be(true);
        }

        [Test]
        public void should_be_true_if_torrent_and_torrent_is_enabled()
        {
            GivenProtocol(DownloadProtocol.Torrent);
            _delayProfile.EnableTorrent = true;

            Subject.IsSatisfiedBy(_remoteMovie, new()).Accepted.Should().Be(true);
        }

        [Test]
        public void should_be_false_if_usenet_and_usenet_is_disabled()
        {
            GivenProtocol(DownloadProtocol.Usenet);
            _delayProfile.EnableUsenet = false;

            Subject.IsSatisfiedBy(_remoteMovie, new()).Accepted.Should().Be(false);
        }

        [Test]
        public void should_be_false_if_torrent_and_torrent_is_disabled()
        {
            GivenProtocol(DownloadProtocol.Torrent);
            _delayProfile.EnableTorrent = false;

            Subject.IsSatisfiedBy(_remoteMovie, new()).Accepted.Should().Be(false);
        }

        [Test]
        public void should_name_the_movie_when_protocol_is_disabled_for_a_movie()
        {
            GivenProtocol(DownloadProtocol.Torrent);
            _delayProfile.EnableTorrent = false;

            Subject.IsSatisfiedBy(_remoteMovie, new()).Message.Should().Be("Torrent is not enabled for this movie");
        }

        [Test]
        public void should_use_the_parent_tags_and_wording_of_a_non_movie_release()
        {
            var release = new TvRemoteRelease { Release = new ReleaseInfo { DownloadProtocol = DownloadProtocol.Usenet } };
            release.Series.Tags = new HashSet<int> { 7 };
            _delayProfile.EnableUsenet = false;

            var result = Subject.IsSatisfiedBy(release, new());

            result.Accepted.Should().BeFalse();
            result.Message.Should().Be("Usenet is not enabled for this series");
            Mocker.GetMock<IDelayProfileService>().Verify(s => s.BestForTags(release.Series.Tags), Times.Once());
        }
    }
}
