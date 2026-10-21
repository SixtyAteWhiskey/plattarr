using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine.ReleaseSpecifications;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.DecisionEngineTests.ReleaseSpecifications
{
    [TestFixture]
    public class TorrentSeedingSpecificationFixture : TestBase<TorrentSeedingSpecification>
    {
        private Movie _movie;
        private RemoteMovie _remoteMovie;
        private IndexerTorrentBaseSettings _torrentSettings;

        [SetUp]
        public void Setup()
        {
            _movie = Builder<Movie>.CreateNew().With(s => s.Id = 1).Build();

            _remoteMovie = new RemoteMovie
            {
                Movie = _movie,
                Release = new TorrentInfo
                {
                    IndexerId = 1,
                    Title = "Series.Title.S01.720p.BluRay.X264-RlsGrp",
                    Seeders = 0,

                    // IndexerSettings = new TorrentRssIndexerSettings {MinimumSeeders = 5}
                }
            };

            _torrentSettings = new IndexerTorrentBaseSettings { MinimumSeeders = 5 };

            Mocker.GetMock<ICachedIndexerSettingsProvider>()
                .Setup(v => v.GetSettings(1))
                .Returns(() => new CachedIndexerSettings { TorrentBaseSettings = _torrentSettings });
        }

        private void GivenReleaseSeeders(int? seeders)
        {
            (_remoteMovie.Release as TorrentInfo).Seeders = seeders;
        }

        [Test]
        public void should_return_true_if_not_torrent()
        {
            _remoteMovie.Release = new ReleaseInfo
            {
                IndexerId = 1,
                Title = "Series.Title.S01.720p.BluRay.X264-RlsGrp"
            };

            Subject.IsSatisfiedBy(_remoteMovie, new()).Accepted.Should().BeTrue();
        }

        // These tests are not needed anymore, since indexer settings are saved on the release itself!
        [Test]
        public void should_return_true_if_indexer_not_specified()
        {
            _remoteMovie.Release.IndexerId = 0;

            Subject.IsSatisfiedBy(_remoteMovie, new()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_true_if_indexer_no_longer_exists()
        {
            Mocker.GetMock<ICachedIndexerSettingsProvider>()
                  .Setup(v => v.GetSettings(It.IsAny<int>()))
                  .Returns((CachedIndexerSettings)null);

            Subject.IsSatisfiedBy(_remoteMovie, new()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_use_one_seeder_when_minimum_is_not_set()
        {
            _torrentSettings.MinimumSeeders = null;

            GivenReleaseSeeders(0);
            Subject.IsSatisfiedBy(_remoteMovie, new()).Accepted.Should().BeFalse();

            GivenReleaseSeeders(1);
            Subject.IsSatisfiedBy(_remoteMovie, new()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_true_if_seeds_unknown()
        {
            GivenReleaseSeeders(null);

            Subject.IsSatisfiedBy(_remoteMovie, new()).Accepted.Should().BeTrue();
        }

        [TestCase(5)]
        [TestCase(6)]
        public void should_return_true_if_seeds_above_or_equal_to_limit(int seeders)
        {
            GivenReleaseSeeders(seeders);

            Subject.IsSatisfiedBy(_remoteMovie, new()).Accepted.Should().BeTrue();
        }

        [TestCase(0)]
        [TestCase(4)]
        public void should_return_false_if_seeds_belove_limit(int seeders)
        {
            GivenReleaseSeeders(seeders);

            Subject.IsSatisfiedBy(_remoteMovie, new()).Accepted.Should().BeFalse();
        }
    }
}
