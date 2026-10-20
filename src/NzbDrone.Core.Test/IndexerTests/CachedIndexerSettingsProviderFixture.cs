using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Newznab;
using NzbDrone.Core.Indexers.Torznab;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerTests
{
    [TestFixture]
    public class CachedIndexerSettingsProviderFixture : CoreTest<CachedIndexerSettingsProvider>
    {
        [Test]
        public void should_return_null_for_unknown_indexer()
        {
            Subject.GetSettings(3).Should().BeNull();
        }

        [Test]
        public void should_map_fail_downloads_and_omit_torrent_settings_for_usenet()
        {
            Mocker.GetMock<IIndexerFactory>()
                  .Setup(f => f.Find(1))
                  .Returns(new IndexerDefinition
                  {
                      Id = 1,
                      Settings = new NewznabSettings { BaseSettings = new IndexerBaseSettings { FailDownloads = new[] { (int)FailDownloads.Executables } } }
                  });

            var settings = Subject.GetSettings(1);

            settings.FailDownloads.Should().BeEquivalentTo(new[] { FailDownloads.Executables });
            settings.TorrentBaseSettings.Should().BeNull();
        }

        [Test]
        public void should_return_torrent_settings_for_torrent_indexer()
        {
            var torznab = new TorznabSettings();
            torznab.TorrentBaseSettings.MinimumSeeders = 4;
            torznab.TorrentBaseSettings.RejectBlocklistedTorrentHashesWhileGrabbing = true;

            Mocker.GetMock<IIndexerFactory>()
                  .Setup(f => f.Find(2))
                  .Returns(new IndexerDefinition { Id = 2, Settings = torznab });

            var settings = Subject.GetSettings(2);

            settings.FailDownloads.Should().BeEmpty();
            settings.TorrentBaseSettings.MinimumSeeders.Should().Be(4);
            settings.TorrentBaseSettings.RejectBlocklistedTorrentHashesWhileGrabbing.Should().BeTrue();
        }
    }
}
