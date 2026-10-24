using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Queue;
using MoviePending = NzbDrone.Core.Movies.Pending;
using MusicParser = NzbDrone.Core.Music.Parser.Model;
using MusicPending = NzbDrone.Core.Music.Pending;
using TvParser = NzbDrone.Core.Tv.Parser.Model;
using TvPending = NzbDrone.Core.Tv.Pending;

namespace NzbDrone.Api.Test.v3.Queue
{
    [TestFixture]
    public class PendingQueueServiceFixture : TestBase<PendingQueueService>
    {
        private NzbDrone.Core.Queue.Queue _movieItem;
        private NzbDrone.Core.Queue.Queue _tvItem;
        private NzbDrone.Core.Queue.Queue _musicItem;

        [SetUp]
        public void Setup()
        {
            _movieItem = new NzbDrone.Core.Queue.Queue { Id = 11, RemoteRelease = new RemoteMovie() };
            _tvItem = new NzbDrone.Core.Queue.Queue { Id = 22, RemoteRelease = new TvParser.RemoteEpisode() };
            _musicItem = new NzbDrone.Core.Queue.Queue { Id = 33, RemoteRelease = new MusicParser.RemoteAlbum() };

            Mocker.GetMock<MoviePending.IPendingReleaseService>()
                  .Setup(s => s.GetPendingQueue())
                  .Returns(new List<NzbDrone.Core.Queue.Queue> { _movieItem });

            Mocker.GetMock<TvPending.IPendingReleaseService>()
                  .Setup(s => s.GetPendingQueue())
                  .Returns(new List<NzbDrone.Core.Queue.Queue> { _tvItem });

            Mocker.GetMock<MusicPending.IPendingReleaseService>()
                  .Setup(s => s.GetPendingQueue())
                  .Returns(new List<NzbDrone.Core.Queue.Queue> { _musicItem });
        }

        [Test]
        public void should_list_the_pending_queue_of_every_module()
        {
            Subject.GetPendingQueue().Should().BeEquivalentTo(new[] { _movieItem, _tvItem, _musicItem });
        }

        [Test]
        public void should_find_a_pending_item_of_any_module()
        {
            Mocker.GetMock<MoviePending.IPendingReleaseService>().Setup(s => s.FindPendingQueueItem(11)).Returns(_movieItem);
            Mocker.GetMock<TvPending.IPendingReleaseService>().Setup(s => s.FindPendingQueueItem(22)).Returns(_tvItem);
            Mocker.GetMock<MusicPending.IPendingReleaseService>().Setup(s => s.FindPendingQueueItem(33)).Returns(_musicItem);

            Subject.FindPendingQueueItem(11).Should().BeSameAs(_movieItem);
            Subject.FindPendingQueueItem(22).Should().BeSameAs(_tvItem);
            Subject.FindPendingQueueItem(33).Should().BeSameAs(_musicItem);
            Subject.FindPendingQueueItem(44).Should().BeNull();
        }

        [Test]
        public void should_remove_a_tv_item_through_the_tv_module_only()
        {
            Subject.RemovePendingQueueItem(_tvItem);

            Mocker.GetMock<TvPending.IPendingReleaseService>().Verify(s => s.RemovePendingQueueItems(22), Times.Once());
            Mocker.GetMock<MoviePending.IPendingReleaseService>().Verify(s => s.RemovePendingQueueItems(It.IsAny<int>()), Times.Never());
            Mocker.GetMock<MusicPending.IPendingReleaseService>().Verify(s => s.RemovePendingQueueItems(It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_remove_a_movie_item_through_the_movie_module_only()
        {
            Subject.RemovePendingQueueItem(_movieItem);

            Mocker.GetMock<MoviePending.IPendingReleaseService>().Verify(s => s.RemovePendingQueueItems(11), Times.Once());
            Mocker.GetMock<TvPending.IPendingReleaseService>().Verify(s => s.RemovePendingQueueItems(It.IsAny<int>()), Times.Never());
            Mocker.GetMock<MusicPending.IPendingReleaseService>().Verify(s => s.RemovePendingQueueItems(It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_remove_a_music_item_through_the_music_module_only()
        {
            Subject.RemovePendingQueueItem(_musicItem);

            Mocker.GetMock<MusicPending.IPendingReleaseService>().Verify(s => s.RemovePendingQueueItems(33), Times.Once());
            Mocker.GetMock<MoviePending.IPendingReleaseService>().Verify(s => s.RemovePendingQueueItems(It.IsAny<int>()), Times.Never());
            Mocker.GetMock<TvPending.IPendingReleaseService>().Verify(s => s.RemovePendingQueueItems(It.IsAny<int>()), Times.Never());
        }
    }
}
