using System;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Newznab;
using NzbDrone.Core.Indexers.Torznab;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerTests
{
    // Ported from Radarr's legacy SeedConfigProviderFixture when grabs moved to Prowlarr's
    // SeedConfigProvider (porting step 7).
    [TestFixture]
    public class SeedConfigProviderFixture : CoreTest<SeedConfigProvider>
    {
        private void GivenIndexer(IIndexerSettings settings)
        {
            Mocker.GetMock<IIndexerFactory>()
                  .Setup(v => v.Get(1))
                  .Returns(new IndexerDefinition { Id = 1, Settings = settings });
        }

        [Test]
        public void should_not_return_config_for_release_without_indexer()
        {
            var result = Subject.GetSeedConfiguration(new ReleaseInfo { DownloadProtocol = DownloadProtocol.Torrent, IndexerId = 0 });

            result.Should().BeNull();
        }

        [Test]
        public void should_not_return_config_for_usenet_release()
        {
            GivenIndexer(new TorznabSettings());

            var result = Subject.GetSeedConfiguration(new ReleaseInfo { DownloadProtocol = DownloadProtocol.Usenet, IndexerId = 1 });

            result.Should().BeNull();
        }

        [Test]
        public void should_not_return_config_for_missing_indexer()
        {
            Mocker.GetMock<IIndexerFactory>()
                  .Setup(v => v.Get(1))
                  .Throws(new ModelNotFoundException(typeof(IndexerDefinition), 1));

            var result = Subject.GetSeedConfiguration(new ReleaseInfo { DownloadProtocol = DownloadProtocol.Torrent, IndexerId = 1 });

            result.Should().BeNull();
        }

        [Test]
        public void should_not_return_config_for_usenet_indexer()
        {
            GivenIndexer(new NewznabSettings());

            var result = Subject.GetSeedConfiguration(new ReleaseInfo { DownloadProtocol = DownloadProtocol.Torrent, IndexerId = 1 });

            result.Should().BeNull();
        }

        [Test]
        public void should_return_seed_ratio_and_time()
        {
            var settings = new TorznabSettings();
            settings.TorrentBaseSettings.SeedRatio = 1.5;
            settings.TorrentBaseSettings.SeedTime = 10;
            GivenIndexer(settings);

            var result = Subject.GetSeedConfiguration(new ReleaseInfo { DownloadProtocol = DownloadProtocol.Torrent, IndexerId = 1 });

            result.Should().NotBeNull();
            result.Ratio.Should().Be(1.5);
            result.SeedTime.Should().Be(TimeSpan.FromMinutes(10));
        }
    }
}
