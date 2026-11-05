using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Queue;

namespace NzbDrone.Core.Test.QueueTests
{
    [TestFixture]
    public class HygieneContentDecisionServiceFixture
    {
        private Mock<IConfigService> _config;
        private Mock<IProvideDownloadClient> _clients;
        private Mock<IHygieneListSource> _sources;
        private Mock<IHygieneActionRepository> _actions;
        private Mock<IDownloadHistoryService> _history;
        private Mock<IDownloadClient> _client;
        private HygieneContentDecisionService _service;

        [SetUp]
        public void SetUp()
        {
            _config = new Mock<IConfigService>();
            _clients = new Mock<IProvideDownloadClient>();
            _sources = new Mock<IHygieneListSource>();
            _actions = new Mock<IHygieneActionRepository>();
            _history = new Mock<IDownloadHistoryService>();
            _client = new Mock<IDownloadClient>();
            _config.SetupGet(x => x.HygieneContentInspectionEnabled).Returns(true);
            _config.SetupGet(x => x.HygieneContentActionsEnabled).Returns(true);
            _config.SetupGet(x => x.HygieneActionsEnabled).Returns(true);
            _config.SetupGet(x => x.HygieneContentListType).Returns("blocklist");
            _sources.Setup(x => x.Load(It.IsAny<string>(), It.IsAny<string>())).Returns("*.exe");
            _client.As<IHygieneFileClient>().Setup(x => x.GetHygieneFileNames("hash"))
                .Returns(new List<string> { "Movie/movie.mkv", "Movie/setup.exe" });
            _client.As<IHygieneFileClient>().SetupGet(x => x.CanSkipHygieneFiles).Returns(true);
            _clients.Setup(x => x.Get(4)).Returns(_client.Object);
            _service = new HygieneContentDecisionService(_config.Object, _clients.Object,
                _sources.Object, _actions.Object, _history.Object);
        }

        [Test]
        public void mixed_public_torrent_is_eligible_to_skip_one_file()
        {
            var assessment = _service.Evaluate(new[] { Item(IndexerPrivacy.Public) }).Should().ContainSingle().Which;

            assessment.Status.Should().Be("skip-blocked-files");
            assessment.WouldAct.Should().BeTrue();
            assessment.BlockedFiles.Should().ContainSingle().Which.Should().Be("Movie/setup.exe");
        }

        [Test]
        public void private_torrent_is_held_even_with_content_actions_enabled()
        {
            var assessment = _service.Evaluate(new[] { Item(IndexerPrivacy.Private) }).Should().ContainSingle().Which;

            assessment.WouldAct.Should().BeFalse();
            assessment.ActionExplanation.Should().Contain("Private or unverified");
        }

        [Test]
        public void unavailable_patterns_hold_without_reading_client()
        {
            _sources.Setup(x => x.Load(It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new System.IO.IOException());

            var assessment = _service.Evaluate(new[] { Item(IndexerPrivacy.Public) }).Should().ContainSingle().Which;

            assessment.Status.Should().Be("patterns-unavailable");
            assessment.WouldAct.Should().BeFalse();
            _clients.Verify(x => x.Get(It.IsAny<int>()), Times.Never);
        }

        private static NzbDrone.Core.Queue.Queue Item(IndexerPrivacy privacy)
        {
            return new NzbDrone.Core.Queue.Queue
            {
                Id = 10,
                DownloadClientId = 4,
                DownloadId = "hash",
                Protocol = DownloadProtocol.Torrent,
                RemoteRelease = new RemoteMovie { Release = new ReleaseInfo { IndexerPrivacy = privacy } }
            };
        }
    }
}
