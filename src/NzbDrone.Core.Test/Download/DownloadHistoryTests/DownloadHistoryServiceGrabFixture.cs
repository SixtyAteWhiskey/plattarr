using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.DecisionEngineTests.ReleaseSpecifications;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download.DownloadHistoryTests
{
    // The download history spans media types: a grab records the kind and parent of the release it carries.
    [TestFixture]
    public class DownloadHistoryServiceGrabFixture : CoreTest<DownloadHistoryService>
    {
        private DownloadHistory _inserted;

        [SetUp]
        public void Setup()
        {
            _inserted = null;

            Mocker.GetMock<IDownloadHistoryRepository>()
                .Setup(r => r.Insert(It.IsAny<DownloadHistory>()))
                .Callback<DownloadHistory>(h => _inserted = h)
                .Returns<DownloadHistory>(h => h);
        }

        private static ReleaseInfo Release()
        {
            return new ReleaseInfo
            {
                Title = "Some.Release.1080p",
                Indexer = "Indexer",
                IndexerId = 4,
                DownloadProtocol = DownloadProtocol.Torrent
            };
        }

        [Test]
        public void should_record_a_grab_of_another_media_type_with_its_kind_and_parent()
        {
            var tv = new TvRemoteRelease { Series = new TvParent { Id = 42, Title = "A Series" }, Release = Release(), CustomFormatScore = 15 };

            Subject.Handle(new ReleaseGrabbedEvent(tv) { DownloadId = "HASH", DownloadClientId = 3, DownloadClient = "qBittorrent", DownloadClientName = "qb-main" });

            _inserted.Should().NotBeNull();
            _inserted.MediaKind.Should().Be(MediaKind.Tv);
            _inserted.ParentId.Should().Be(42);
            _inserted.EventType.Should().Be(DownloadHistoryEventType.DownloadGrabbed);
            _inserted.DownloadId.Should().Be("HASH");
            _inserted.DownloadClientId.Should().Be(3);
            _inserted.IndexerId.Should().Be(4);
            _inserted.Protocol.Should().Be(DownloadProtocol.Torrent);
            _inserted.Data["CustomFormatScore"].Should().Be("15");
        }

        [Test]
        public void should_record_a_movie_grab_as_a_movie()
        {
            var movie = new RemoteMovie { Movie = new Movie { Id = 9, Tags = new HashSet<int>() }, Release = Release() };

            Subject.Handle(new ReleaseGrabbedEvent(movie) { DownloadId = "HASH", DownloadClientId = 3 });

            _inserted.MediaKind.Should().Be(MediaKind.Movies);
            _inserted.ParentId.Should().Be(9);
        }

        [Test]
        public void should_not_record_a_grab_without_a_download_id()
        {
            var tv = new TvRemoteRelease { Series = new TvParent { Id = 42 }, Release = Release() };

            Subject.Handle(new ReleaseGrabbedEvent(tv) { DownloadId = null });

            _inserted.Should().BeNull();
        }
    }
}
