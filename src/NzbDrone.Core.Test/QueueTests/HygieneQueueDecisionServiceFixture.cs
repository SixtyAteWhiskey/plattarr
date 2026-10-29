using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Queue;
using QueueItem = NzbDrone.Core.Queue.Queue;

namespace NzbDrone.Core.Test.QueueTests
{
    [TestFixture]
    public class HygieneQueueDecisionServiceFixture
    {
        [TestCase(true)]
        [TestCase(false)]
        public void cumulative_rule_acts_after_twelve_alternating_slow_or_stalled_strikes(bool stalled)
        {
            var item = new QueueItem
            {
                DownloadClientId = 7,
                DownloadId = "alternating-test-hash",
                Status = QueueStatus.Downloading,
                SizeLeft = 1000,
                DownloadClientStalled = stalled,
                DownloadRateBytesPerSecond = 1024,
                Protocol = NzbDrone.Core.Indexers.DownloadProtocol.Torrent,
                RemoteRelease = new NzbDrone.Core.Movies.Parser.Model.RemoteMovie
                {
                    Release = new NzbDrone.Core.Parser.Model.ReleaseInfo
                    {
                        IndexerPrivacy = NzbDrone.Core.Indexers.IndexerPrivacy.Public
                    }
                }
            };
            var states = new Mock<IHygieneQueueStateRepository>();
            states.Setup(x => x.All()).Returns(new[]
            {
                new HygieneQueueState
                {
                    DownloadClientId = 7,
                    DownloadId = item.DownloadId,
                    ReasonCode = stalled ? "download-slow" : "download-stalled",
                    Strikes = 1,
                    UnstableRuleId = 8,
                    UnstableStrikes = 12
                }
            });
            var rules = new Mock<IHygieneRuleRepository>();
            rules.Setup(x => x.All()).Returns(new List<HygieneRule>
            {
                new() { Id = 8, ReasonCode = "download-unstable", Enabled = true, StrikeLimit = 12, StrikeIntervalMinutes = 5, SearchForReplacement = false },
                new() { Id = 9, ReasonCode = stalled ? "download-stalled" : "download-slow", Enabled = true, StrikeLimit = 3, StrikeIntervalMinutes = 5, MinimumDownloadRateKiBps = stalled ? 0 : 100 }
            });
            var config = new Mock<IConfigService>();
            config.SetupGet(x => x.HygieneActionsEnabled).Returns(true);
            var decisions = new HygieneQueueDecisionService(new HygieneQueueEvaluator(), states.Object, new HygieneRuleService(rules.Object, NLog.LogManager.GetCurrentClassLogger()), config.Object);

            var assessment = decisions.Evaluate(new[] { item }).Should().ContainSingle().Subject;

            assessment.ReasonCode.Should().Be("download-unstable");
            assessment.RuleId.Should().Be(8);
            assessment.Strikes.Should().Be(12);
            assessment.UnstableStrikeLimit.Should().Be(12);
            assessment.Eligible.Should().BeTrue();
            assessment.WouldAct.Should().BeTrue();
            assessment.SearchForReplacement.Should().BeFalse();
        }

        [Test]
        public void cumulative_limit_does_not_act_after_the_download_recovers()
        {
            var item = new QueueItem
            {
                DownloadClientId = 7,
                DownloadId = "recovered-test-hash",
                Status = QueueStatus.Downloading,
                SizeLeft = 1000,
                DownloadRateBytesPerSecond = 200 * 1024,
                Protocol = NzbDrone.Core.Indexers.DownloadProtocol.Torrent
            };
            var states = new Mock<IHygieneQueueStateRepository>();
            states.Setup(x => x.All()).Returns(new[]
            {
                new HygieneQueueState { DownloadClientId = 7, DownloadId = item.DownloadId, UnstableRuleId = 8, UnstableStrikes = 12 }
            });
            var rules = new Mock<IHygieneRuleRepository>();
            rules.Setup(x => x.All()).Returns(new List<HygieneRule>
            {
                new() { Id = 8, ReasonCode = "download-unstable", Enabled = true, StrikeLimit = 12, StrikeIntervalMinutes = 5 },
                new() { Id = 9, ReasonCode = "download-slow", Enabled = true, StrikeLimit = 3, StrikeIntervalMinutes = 5, MinimumDownloadRateKiBps = 100 }
            });
            var config = new Mock<IConfigService>();
            config.SetupGet(x => x.HygieneActionsEnabled).Returns(true);
            var decisions = new HygieneQueueDecisionService(new HygieneQueueEvaluator(), states.Object, new HygieneRuleService(rules.Object, NLog.LogManager.GetCurrentClassLogger()), config.Object);

            var assessment = decisions.Evaluate(new[] { item }).Should().ContainSingle().Subject;

            assessment.ReasonCode.Should().Be("healthy");
            assessment.UnstableStrikes.Should().Be(12);
            assessment.UnstableStrikeLimit.Should().Be(12);
            assessment.Eligible.Should().BeFalse();
            assessment.WouldAct.Should().BeFalse();
        }

        [Test]
        public void reached_threshold_is_eligible_but_cannot_execute_cleanup()
        {
            var item = new QueueItem
            {
                DownloadClientId = 7,
                DownloadId = "test-hash",
                Status = QueueStatus.Failed
            };
            var states = new Mock<IHygieneQueueStateRepository>();
            states.Setup(x => x.All()).Returns(new[]
            {
                new HygieneQueueState
                {
                    DownloadClientId = 7,
                    DownloadId = "test-hash",
                    ReasonCode = "client-failed",
                    Strikes = 3
                }
            });
            var rules = new Mock<IHygieneRuleRepository>();
            rules.Setup(x => x.All()).Returns(new List<HygieneRule>
            {
                new() { ReasonCode = "client-failed", Enabled = true, StrikeLimit = 3, StrikeIntervalMinutes = 5 }
            });
            var config = new Mock<IConfigService>();
            config.SetupGet(x => x.HygieneActionsEnabled).Returns(false);
            var decisions = new HygieneQueueDecisionService(new HygieneQueueEvaluator(), states.Object, new HygieneRuleService(rules.Object, NLog.LogManager.GetCurrentClassLogger()), config.Object);

            var assessment = decisions.Evaluate(new[] { item }).Should().ContainSingle().Subject;

            assessment.Strikes.Should().Be(3);
            assessment.Eligible.Should().BeTrue();
            assessment.WouldAct.Should().BeFalse();
        }

        [Test]
        public void pending_item_without_a_download_id_has_no_strikes()
        {
            var states = new Mock<IHygieneQueueStateRepository>();
            states.Setup(x => x.All()).Returns(new List<HygieneQueueState>());
            var rules = new Mock<IHygieneRuleRepository>();
            rules.Setup(x => x.All()).Returns(new List<HygieneRule>());
            var config = new Mock<IConfigService>();
            config.SetupGet(x => x.HygieneActionsEnabled).Returns(false);
            var decisions = new HygieneQueueDecisionService(new HygieneQueueEvaluator(), states.Object, new HygieneRuleService(rules.Object, NLog.LogManager.GetCurrentClassLogger()), config.Object);

            var assessment = decisions.Evaluate(new[] { new QueueItem { Status = QueueStatus.Delay } }).Should().ContainSingle().Subject;

            assessment.Strikes.Should().Be(0);
            assessment.Eligible.Should().BeFalse();
            assessment.WouldAct.Should().BeFalse();
        }

        [Test]
        public void private_torrent_release_is_held_even_when_gate_and_rule_are_enabled()
        {
            var item = new QueueItem
            {
                DownloadClientId = 7,
                DownloadId = "private-test-hash",
                Protocol = NzbDrone.Core.Indexers.DownloadProtocol.Torrent,
                Status = QueueStatus.Failed,
                RemoteRelease = new NzbDrone.Core.Movies.Parser.Model.RemoteMovie
                {
                    Release = new NzbDrone.Core.Parser.Model.ReleaseInfo
                    {
                        IndexerPrivacy = NzbDrone.Core.Indexers.IndexerPrivacy.Private
                    }
                }
            };
            var states = new Mock<IHygieneQueueStateRepository>();
            states.Setup(x => x.All()).Returns(new[]
            {
                new HygieneQueueState { DownloadClientId = 7, DownloadId = "private-test-hash", ReasonCode = "client-failed", Strikes = 3 }
            });
            var rule = new HygieneRule { Id = 8, ReasonCode = "client-failed", Enabled = true, StrikeLimit = 3, StrikeIntervalMinutes = 5 };
            var rules = new Mock<IHygieneRuleRepository>();
            rules.Setup(x => x.All()).Returns(new List<HygieneRule> { rule });
            var config = new Mock<IConfigService>();
            config.SetupGet(x => x.HygieneActionsEnabled).Returns(true);
            var decisions = new HygieneQueueDecisionService(new HygieneQueueEvaluator(), states.Object, new HygieneRuleService(rules.Object, NLog.LogManager.GetCurrentClassLogger()), config.Object);

            var assessment = decisions.Evaluate(new[] { item }).Should().ContainSingle().Subject;

            assessment.Eligible.Should().BeTrue();
            assessment.WouldAct.Should().BeFalse();
            assessment.ActionExplanation.Should().Contain("private");
        }

        [TestCase("download-stalled", true, true, true, false, true)]
        [TestCase("download-slow", false, true, true, false, true)]
        [TestCase("download-stalled", true, true, true, true, true)]
        [TestCase("download-stalled", true, true, true, false, false)]
        [TestCase("download-stalled", true, false, true, false, true)]
        [TestCase("download-slow", false, false, true, false, true)]
        [TestCase("download-stalled", true, true, false, false, true)]
        [TestCase("download-slow", false, true, false, false, true)]
        public void stalled_and_slow_downloads_act_only_with_an_eligible_rule_and_open_gate(string reasonCode, bool stalled, bool gateEnabled, bool torrent, bool deleteFilesOnFailure, bool searchForReplacement)
        {
            NzbDrone.Core.Modules.RemoteRelease remoteRelease = null;

            if (torrent)
            {
                remoteRelease = new NzbDrone.Core.Movies.Parser.Model.RemoteMovie
                {
                    Release = new NzbDrone.Core.Parser.Model.ReleaseInfo
                    {
                        IndexerPrivacy = NzbDrone.Core.Indexers.IndexerPrivacy.Public
                    }
                };
            }

            var item = new QueueItem
            {
                DownloadClientId = 7,
                DownloadId = "monitor-only-test-hash",
                Status = QueueStatus.Downloading,
                SizeLeft = 1000,
                DownloadClientStalled = stalled,
                DownloadRateBytesPerSecond = 1024,
                Protocol = torrent ? NzbDrone.Core.Indexers.DownloadProtocol.Torrent : NzbDrone.Core.Indexers.DownloadProtocol.Usenet,
                RemoteRelease = remoteRelease
            };
            var states = new Mock<IHygieneQueueStateRepository>();
            states.Setup(x => x.All()).Returns(new[]
            {
                new HygieneQueueState { DownloadClientId = 7, DownloadId = item.DownloadId, ReasonCode = reasonCode, Strikes = 3 }
            });
            var rules = new Mock<IHygieneRuleRepository>();
            rules.Setup(x => x.All()).Returns(new List<HygieneRule>
            {
                new()
                {
                    Id = 9,
                    ReasonCode = reasonCode,
                    Enabled = true,
                    DeleteFilesOnFailure = deleteFilesOnFailure,
                    SearchForReplacement = searchForReplacement,
                    StrikeLimit = 3,
                    StrikeIntervalMinutes = 5,
                    MinimumDownloadRateKiBps = stalled ? 0 : 100
                }
            });
            var config = new Mock<IConfigService>();
            config.SetupGet(x => x.HygieneActionsEnabled).Returns(gateEnabled);
            var decisions = new HygieneQueueDecisionService(new HygieneQueueEvaluator(), states.Object, new HygieneRuleService(rules.Object, NLog.LogManager.GetCurrentClassLogger()), config.Object);

            var assessment = decisions.Evaluate(new[] { item }).Should().ContainSingle().Subject;

            assessment.ReasonCode.Should().Be(reasonCode);
            assessment.RuleId.Should().Be(9);
            assessment.Eligible.Should().BeTrue();
            assessment.DeleteFilesOnFailure.Should().Be(deleteFilesOnFailure);
            assessment.SearchForReplacement.Should().Be(searchForReplacement);
            assessment.WouldAct.Should().Be(gateEnabled && torrent);
            assessment.ActionExplanation.Should().Contain(
                !torrent ? "only for torrent downloads" :
                gateEnabled ? deleteFilesOnFailure ? "delete its source files" : "keep its source files" : "global Hygiene gate");
            if (gateEnabled && torrent)
            {
                assessment.ActionExplanation.Should().Contain(searchForReplacement ? "request a replacement search" : "skip the replacement search");
            }
        }
    }
}
