using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download
{
    [TestFixture]
    public class DownloadProcessingServiceFixture : CoreTest<DownloadProcessingService>
    {
        private TrackedDownload _download;
        private List<HygieneRule> _rules;

        [SetUp]
        public void Setup()
        {
            _download = new TrackedDownload
            {
                Kind = MediaKind.Movies,
                DownloadClient = 4,
                State = TrackedDownloadState.FailedPending,
                IsTrackable = true,
                Protocol = DownloadProtocol.Torrent,
                DownloadItem = new DownloadClientItem { DownloadId = "fixture-failed", Status = DownloadItemStatus.Failed },
                RemoteRelease = new RemoteMovie { Release = new ReleaseInfo { IndexerPrivacy = IndexerPrivacy.Public } }
            };
            _rules = new List<HygieneRule>
            {
                new HygieneRule { Id = 1, ReasonCode = "client-failed", Enabled = true, StrikeLimit = 3 }
            };
            Mocker.GetMock<ITrackedDownloadService>().Setup(x => x.GetTrackedDownloads())
                .Returns(new List<TrackedDownload> { _download });
            Mocker.GetMock<IHygieneRuleRepository>().Setup(x => x.All()).Returns(_rules);
            Mocker.SetConstant<IHygieneRuleService>(new HygieneRuleService(Mocker.GetMock<IHygieneRuleRepository>().Object, TestLogger));
        }

        private void Execute(bool gate)
        {
            Mocker.GetMock<IConfigService>().SetupGet(x => x.HygieneActionsEnabled).Returns(gate);
            Subject.Execute(new ProcessMonitoredDownloadsCommand());
        }

        [Test]
        public void active_hygiene_rule_should_own_failure_processing_before_its_threshold()
        {
            Execute(true);
            Mocker.GetMock<IFailedDownloadService>().Verify(x => x.ProcessFailed(It.IsAny<TrackedDownload>()), Times.Never());
        }

        [Test]
        public void monitoring_with_the_global_gate_off_should_preserve_native_failure_handling()
        {
            Execute(false);
            Mocker.GetMock<IFailedDownloadService>().Verify(x => x.ProcessFailed(_download), Times.Once());
        }

        [Test]
        public void a_missing_or_disabled_rule_should_preserve_native_failure_handling()
        {
            _rules[0].Enabled = false;
            Execute(true);
            _rules.Clear();
            Execute(true);
            Mocker.GetMock<IFailedDownloadService>().Verify(x => x.ProcessFailed(_download), Times.Exactly(2));
        }

        [TestCase(2, 0)]
        [TestCase(0, 9)]
        public void a_rule_for_another_media_kind_or_client_should_not_take_over(int kind, int client)
        {
            _rules[0].MediaKind = kind;
            _rules[0].DownloadClientId = client;
            Execute(true);
            Mocker.GetMock<IFailedDownloadService>().Verify(x => x.ProcessFailed(_download), Times.Once());
        }

        [Test]
        public void a_disabled_specific_rule_should_override_an_enabled_broad_rule()
        {
            _rules.Add(new HygieneRule { Id = 2, ReasonCode = "client-failed", Enabled = false, DownloadClientId = 4 });
            Execute(true);
            Mocker.GetMock<IFailedDownloadService>().Verify(x => x.ProcessFailed(_download), Times.Once());
        }

        [Test]
        public void a_private_only_rule_should_not_take_over_public_downloads()
        {
            _rules[0].TorrentPrivacyScope = "private";
            Execute(true);
            Mocker.GetMock<IFailedDownloadService>().Verify(x => x.ProcessFailed(_download), Times.Once());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void an_outage_should_hold_native_failure_handling_regardless_of_the_hygiene_gate(bool gate)
        {
            _download.DownloadItem.DownloadClientNetworkUnavailable = true;
            Execute(gate);
            Mocker.GetMock<IFailedDownloadService>().Verify(x => x.ProcessFailed(It.IsAny<TrackedDownload>()), Times.Never());

            _download.DownloadItem.DownloadClientNetworkUnavailable = false;
            Execute(false);
            Mocker.GetMock<IFailedDownloadService>().Verify(x => x.ProcessFailed(_download), Times.Once());
        }
    }
}
