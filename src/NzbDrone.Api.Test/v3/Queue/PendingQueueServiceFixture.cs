using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Queue;
using MoviePending = NzbDrone.Core.Movies.Pending;
using TvParser = NzbDrone.Core.Tv.Parser.Model;
using TvPending = NzbDrone.Core.Tv.Pending;

namespace NzbDrone.Api.Test.v3.Queue
{
    [TestFixture]
    public class PendingQueueServiceFixture : TestBase<PendingQueueService>
    {
        private NzbDrone.Core.Queue.Queue _movieItem;
        private NzbDrone.Core.Queue.Queue _tvItem;

        [SetUp]
        public void Setup()
        {
            _movieItem = new NzbDrone.Core.Queue.Queue { Id = 11, RemoteRelease = new RemoteMovie() };
            _tvItem = new NzbDrone.Core.Queue.Queue { Id = 22, RemoteRelease = new TvParser.RemoteEpisode() };

            Mocker.GetMock<MoviePending.IPendingReleaseService>()
                  .Setup(s => s.GetPendingQueue())
                  .Returns(new List<NzbDrone.Core.Queue.Queue> { _movieItem });

            Mocker.GetMock<TvPending.IPendingReleaseService>()
                  .Setup(s => s.GetPendingQueue())
                  .Returns(new List<NzbDrone.Core.Queue.Queue> { _tvItem });
        }

        [Test]
        public void should_list_the_pending_queue_of_both_modules()
        {
            Subject.GetPendingQueue().Should().BeEquivalentTo(new[] { _movieItem, _tvItem });
        }

        [Test]
        public void should_find_a_pending_item_of_either_module()
        {
            Mocker.GetMock<MoviePending.IPendingReleaseService>().Setup(s => s.FindPendingQueueItem(11)).Returns(_movieItem);
            Mocker.GetMock<TvPending.IPendingReleaseService>().Setup(s => s.FindPendingQueueItem(22)).Returns(_tvItem);

            Subject.FindPendingQueueItem(11).Should().BeSameAs(_movieItem);
            Subject.FindPendingQueueItem(22).Should().BeSameAs(_tvItem);
            Subject.FindPendingQueueItem(33).Should().BeNull();
        }

        [Test]
        public void should_remove_a_tv_item_through_the_tv_module_only()
        {
            Subject.RemovePendingQueueItem(_tvItem);

            Mocker.GetMock<TvPending.IPendingReleaseService>().Verify(s => s.RemovePendingQueueItems(22), Times.Once());
            Mocker.GetMock<MoviePending.IPendingReleaseService>().Verify(s => s.RemovePendingQueueItems(It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_remove_a_movie_item_through_the_movie_module_only()
        {
            Subject.RemovePendingQueueItem(_movieItem);

            Mocker.GetMock<MoviePending.IPendingReleaseService>().Verify(s => s.RemovePendingQueueItems(11), Times.Once());
            Mocker.GetMock<TvPending.IPendingReleaseService>().Verify(s => s.RemovePendingQueueItems(It.IsAny<int>()), Times.Never());
        }
    }
}
