using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv.DecisionEngine;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Tv.Pending;
using NzbDrone.Core.Tv.Search;

namespace NzbDrone.Core.Test.TvTests.SearchTests
{
    [TestFixture]
    public class TvRssReleaseHandlerFixture : CoreTest<TvRssReleaseHandler>
    {
        private List<DownloadDecision> _decisions;
        private ProcessedDecisions _processed;

        [SetUp]
        public void Setup()
        {
            _decisions = new List<DownloadDecision>();
            _processed = new ProcessedDecisions(new List<DownloadDecision>(), new List<DownloadDecision>(), new List<DownloadDecision>());

            Mocker.GetMock<IPendingReleaseService>()
                  .Setup(s => s.GetPending())
                  .Returns(new List<ReleaseInfo>());

            Mocker.GetMock<IMakeDownloadDecision>()
                  .Setup(s => s.GetRssDecision(It.IsAny<List<ReleaseInfo>>(), false))
                  .Returns(_decisions);

            Mocker.GetMock<IProcessDownloadDecisions>()
                  .Setup(s => s.ProcessDecisions(_decisions))
                  .ReturnsAsync(_processed);
        }

        [Test]
        public void should_handle_tv()
        {
            Subject.Kind.Should().Be(MediaKind.Tv);
        }

        [Test]
        public async Task should_decide_on_fetched_releases_followed_by_pending_releases()
        {
            var fetched = new ReleaseInfo { Title = "fetched" };
            var pending = new ReleaseInfo { Title = "pending" };

            Mocker.GetMock<IPendingReleaseService>()
                  .Setup(s => s.GetPending())
                  .Returns(new List<ReleaseInfo> { pending });

            await Subject.Process(new List<ReleaseInfo> { fetched });

            Mocker.GetMock<IMakeDownloadDecision>()
                  .Verify(v => v.GetRssDecision(It.Is<List<ReleaseInfo>>(l => l.Select(r => r.Title).SequenceEqual(new[] { "fetched", "pending" })), false), Times.Once());
        }

        [Test]
        public async Task should_process_the_decisions()
        {
            await Subject.Process(new List<ReleaseInfo>());

            Mocker.GetMock<IProcessDownloadDecisions>().Verify(v => v.ProcessDecisions(_decisions), Times.Once());
        }

        [Test]
        public async Task should_publish_the_tv_rss_sync_complete_event_with_the_processed_decisions()
        {
            await Subject.Process(new List<ReleaseInfo>());

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.Is<RssSyncCompleteEvent>(e => e.ProcessedDecisions == _processed)), Times.Once());
        }

        [Test]
        public async Task should_complete_when_decisions_are_pending()
        {
            _processed.Pending.Add(new DownloadDecision(new RemoteEpisode()));

            await Subject.Process(new List<ReleaseInfo>());

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<RssSyncCompleteEvent>()), Times.Once());
        }
    }
}
