using System;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download
{
    // The provider finds the indexer a torrent was grabbed from and returns that indexer's seed
    // settings. It reads no media type.
    [TestFixture]
    public class DownloadSeedConfigProviderFixture : CoreTest<DownloadSeedConfigProvider>
    {
        private TorrentSeedConfiguration _indexerConfig;

        [SetUp]
        public void Setup()
        {
            _indexerConfig = new TorrentSeedConfiguration { Ratio = 2.0, SeedTime = TimeSpan.FromHours(6) };

            Mocker.GetMock<ISeedConfigProvider>()
                .Setup(s => s.GetSeedConfiguration(4))
                .Returns(_indexerConfig);
        }

        private void GivenGrab(string title, int indexerId = 4)
        {
            Mocker.GetMock<IDownloadHistoryService>()
                .Setup(h => h.GetLatestGrab("HASH"))
                .Returns(new DownloadHistory
                {
                    IndexerId = indexerId,
                    Release = title == null ? null : new ReleaseInfo { Title = title }
                });
        }

        [Test]
        public void should_return_the_seed_configuration_of_the_indexer_that_was_grabbed_from()
        {
            GivenGrab("Movie.2020.1080p.WEB-DL-GROUP");

            Subject.GetSeedConfiguration("hash").Should().BeSameAs(_indexerConfig);
        }

        [Test]
        public void should_return_the_seed_configuration_when_the_title_is_not_a_movie_title()
        {
            // The old code returned nothing for a title the movie parser could not read. The
            // indexer is known from the grab, so its settings apply to any media type.
            GivenGrab("Show.S01E01.720p.HDTV-GROUP");

            Subject.GetSeedConfiguration("HASH").Should().BeSameAs(_indexerConfig);
        }

        [Test]
        public void should_return_nothing_for_an_empty_info_hash()
        {
            Subject.GetSeedConfiguration(null).Should().BeNull();
            Subject.GetSeedConfiguration(" ").Should().BeNull();
        }

        [Test]
        public void should_return_nothing_when_the_download_was_never_grabbed()
        {
            Mocker.GetMock<IDownloadHistoryService>()
                .Setup(h => h.GetLatestGrab("HASH"))
                .Returns((DownloadHistory)null);

            Subject.GetSeedConfiguration("HASH").Should().BeNull();
        }

        [Test]
        public void should_return_nothing_when_the_grab_kept_no_release()
        {
            GivenGrab(null);

            Subject.GetSeedConfiguration("HASH").Should().BeNull();
        }
    }
}
