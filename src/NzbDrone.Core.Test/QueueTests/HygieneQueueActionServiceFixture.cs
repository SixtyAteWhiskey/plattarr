using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;
using QueueItem = NzbDrone.Core.Queue.Queue;

namespace NzbDrone.Core.Test.QueueTests
{
    [TestFixture]
    public class HygieneQueueActionServiceFixture : CoreTest
    {
        [TestCase(MediaKind.Movies)]
        [TestCase(MediaKind.Tv)]
        [TestCase(MediaKind.Music)]
        public void eligible_media_kind_is_removed_without_deleting_source_data_and_journaled(MediaKind kind)
        {
            var context = new ActionContext(kind, true);

            context.Service.Process(new[] { context.QueueItem });

            context.DownloadClient.Verify(x => x.RemoveItem(It.IsAny<DownloadClientItem>(), false), Times.Once);
            context.FailedDownloads.Verify(x => x.MarkAsFailed(context.TrackedDownload, false), Times.Once);
            context.TrackedDownloads.Verify(x => x.StopTracking("test-download"), Times.Once);
            context.Actions.Should().ContainSingle().Which.Status.Should().Be("completed");
            context.Actions[0].Checkpoint.Should().Be("replacement-requested");
            context.Actions[0].RuleId.Should().Be(9);
        }

        [Test]
        public void global_gate_prevents_all_executor_work()
        {
            var context = new ActionContext(MediaKind.Movies, false);

            context.Service.Process(new[] { context.QueueItem });

            context.Decisions.Verify(x => x.Evaluate(It.IsAny<IEnumerable<QueueItem>>()), Times.Never);
            context.DownloadClient.Verify(x => x.RemoveItem(It.IsAny<DownloadClientItem>(), It.IsAny<bool>()), Times.Never);
            context.Actions.Should().BeEmpty();
        }

        [Test]
        public void failed_client_removal_stays_journaled_and_retries_at_the_last_checkpoint()
        {
            var context = new ActionContext(MediaKind.Tv, true);
            var failFirstAttempt = true;
            context.DownloadClient
                .Setup(x => x.RemoveItem(It.IsAny<DownloadClientItem>(), false))
                .Callback(() =>
                {
                    if (failFirstAttempt)
                    {
                        failFirstAttempt = false;
                        throw new InvalidOperationException("simulated client outage");
                    }
                });

            context.Service.Process(new[] { context.QueueItem });
            ExceptionVerification.ExpectedWarns(1);

            context.Actions.Should().ContainSingle().Which.Status.Should().Be("failed");
            context.Actions[0].Checkpoint.Should().Be("prepared");
            context.Actions[0].LastError.Should().Be(nameof(InvalidOperationException));
            context.FailedDownloads.Verify(x => x.MarkAsFailed(It.IsAny<TrackedDownload>(), It.IsAny<bool>()), Times.Never);

            context.Service.Process(new[] { context.QueueItem });

            context.DownloadClient.Verify(x => x.RemoveItem(It.IsAny<DownloadClientItem>(), false), Times.Exactly(2));
            context.FailedDownloads.Verify(x => x.MarkAsFailed(context.TrackedDownload, false), Times.Once);
            context.Actions[0].Status.Should().Be("completed");
        }

        [Test]
        public void later_queue_refresh_resumes_after_client_removal_without_removing_the_job_twice()
        {
            var context = new ActionContext(MediaKind.Music, true);
            var failFirstAttempt = true;
            context.FailedDownloads
                .Setup(x => x.MarkAsFailed(context.TrackedDownload, false))
                .Callback(() =>
                {
                    if (failFirstAttempt)
                    {
                        failFirstAttempt = false;
                        throw new InvalidOperationException("simulated event outage");
                    }
                });

            context.Service.Process(new[] { context.QueueItem });
            ExceptionVerification.ExpectedWarns(1);

            context.Actions.Should().ContainSingle().Which.Status.Should().Be("failed");
            context.Actions[0].Checkpoint.Should().Be("client-removed");

            context.Service.Process(Array.Empty<QueueItem>());

            context.DownloadClient.Verify(x => x.RemoveItem(It.IsAny<DownloadClientItem>(), false), Times.Once);
            context.FailedDownloads.Verify(x => x.MarkAsFailed(context.TrackedDownload, false), Times.Exactly(2));
            context.TrackedDownloads.Verify(x => x.StopTracking("test-download"), Times.Once);
            context.Actions[0].Status.Should().Be("completed");
        }

        [Test]
        public void interrupted_action_resumes_even_if_it_is_not_in_the_current_queue()
        {
            var context = new ActionContext(MediaKind.Movies, true);
            var failFirstAttempt = true;
            context.FailedDownloads
                .Setup(x => x.MarkAsFailed(context.TrackedDownload, false))
                .Callback(() =>
                {
                    if (failFirstAttempt)
                    {
                        failFirstAttempt = false;
                        throw new InvalidOperationException("simulated event outage");
                    }
                });

            context.Service.Process(new[] { context.QueueItem });
            ExceptionVerification.ExpectedWarns(1);
            context.Actions[0].Checkpoint.Should().Be("client-removed");

            context.Service.Process(Array.Empty<QueueItem>());

            context.Actions[0].Status.Should().Be("completed");
            context.DownloadClient.Verify(x => x.RemoveItem(It.IsAny<DownloadClientItem>(), false), Times.Once);
            context.FailedDownloads.Verify(x => x.MarkAsFailed(context.TrackedDownload, false), Times.Exactly(2));
        }

        [Test]
        public void gate_closing_mid_action_pauses_at_the_next_checkpoint_and_can_resume()
        {
            var context = new ActionContext(MediaKind.Tv, true);
            context.FailedDownloads
                .Setup(x => x.MarkAsFailed(context.TrackedDownload, false))
                .Callback(() => context.GateEnabled = false);

            context.Service.Process(new[] { context.QueueItem });

            context.Actions.Should().ContainSingle().Which.Status.Should().Be("paused");
            context.Actions[0].Checkpoint.Should().Be("failure-recorded");
            context.TrackedDownloads.Verify(x => x.StopTracking(It.IsAny<string>()), Times.Never);

            context.GateEnabled = true;
            context.Service.Process(Array.Empty<QueueItem>());

            context.Actions[0].Status.Should().Be("completed");
            context.TrackedDownloads.Verify(x => x.StopTracking("test-download"), Times.Once);
        }

        [Test]
        public void gate_off_pauses_an_interrupted_action_on_the_next_queue_refresh()
        {
            var context = new ActionContext(MediaKind.Tv, true);
            context.FailedDownloads
                .Setup(x => x.MarkAsFailed(context.TrackedDownload, false))
                .Throws(new InvalidOperationException("simulated event outage"));

            context.Service.Process(new[] { context.QueueItem });
            ExceptionVerification.ExpectedWarns(1);
            context.Actions[0].Checkpoint.Should().Be("client-removed");

            context.GateEnabled = false;
            context.Service.Process(Array.Empty<QueueItem>());

            context.Actions[0].Status.Should().Be("paused");
            context.Actions[0].Checkpoint.Should().Be("client-removed");
            context.FailedDownloads.Verify(x => x.MarkAsFailed(context.TrackedDownload, false), Times.Once);
        }

        [Test]
        public void journal_resume_does_not_continue_against_a_different_download_client()
        {
            var context = new ActionContext(MediaKind.Tv, true);
            var action = new HygieneAction
            {
                Id = 15,
                DownloadClientId = context.QueueItem.DownloadClientId,
                DownloadId = context.QueueItem.DownloadId,
                MediaKind = (int)MediaKind.Tv,
                ReasonCode = "client-failed",
                RuleId = 9,
                Status = "failed",
                Checkpoint = "client-removed",
                RecoveryPath = "manual recovery"
            };
            context.Actions.Add(action);
            var otherClientDownload = new TrackedDownload
            {
                DownloadClient = context.QueueItem.DownloadClientId + 1,
                DownloadItem = new DownloadClientItem { DownloadId = context.QueueItem.DownloadId, CanBeRemoved = true },
                Kind = MediaKind.Tv,
                State = TrackedDownloadState.Downloading
            };
            context.TrackedDownloads.Setup(x => x.Find(context.QueueItem.DownloadId)).Returns(otherClientDownload);

            context.Service.Process(Array.Empty<QueueItem>());
            ExceptionVerification.ExpectedWarns(1);

            action.Status.Should().Be("failed");
            action.Checkpoint.Should().Be("client-removed");
            action.LastError.Should().Be(nameof(InvalidOperationException));
            context.FailedDownloads.Verify(x => x.MarkAsFailed(It.IsAny<TrackedDownload>(), It.IsAny<bool>()), Times.Never);
            context.TrackedDownloads.Verify(x => x.StopTracking(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void action_is_completed_if_tracking_stopped_before_the_final_journal_write()
        {
            var context = new ActionContext(MediaKind.Music, true);
            var action = new HygieneAction
            {
                Id = 11,
                DownloadClientId = context.QueueItem.DownloadClientId,
                DownloadId = context.QueueItem.DownloadId,
                MediaKind = (int)MediaKind.Music,
                ReasonCode = "client-failed",
                RuleId = 9,
                Status = "failed",
                Checkpoint = "replacement-requested",
                RecoveryPath = "manual recovery"
            };
            context.Actions.Add(action);
            context.TrackedDownloads.Setup(x => x.Find("test-download")).Returns((TrackedDownload)null);

            context.Service.Process(Array.Empty<QueueItem>());

            action.Status.Should().Be("completed");
        }

        [Test]
        public void unstarted_journal_entry_pauses_when_its_rule_is_no_longer_eligible()
        {
            var context = new ActionContext(MediaKind.Movies, true);
            var action = new HygieneAction
            {
                Id = 14,
                DownloadClientId = context.QueueItem.DownloadClientId,
                DownloadId = context.QueueItem.DownloadId,
                MediaKind = (int)MediaKind.Movies,
                ReasonCode = "client-failed",
                RuleId = 9,
                Status = "failed",
                Checkpoint = "prepared",
                RecoveryPath = "manual recovery"
            };
            context.Actions.Add(action);
            context.Decisions.Setup(x => x.Evaluate(It.IsAny<IEnumerable<QueueItem>>()))
                .Returns(new List<HygieneQueueAssessment> { new() { ReasonCode = "client-failed", Kind = MediaKind.Movies, RuleId = 9, WouldAct = false } });

            context.Service.Process(new[] { context.QueueItem });

            action.Status.Should().Be("paused");
            action.Checkpoint.Should().Be("prepared");
            context.DownloadClient.Verify(x => x.RemoveItem(It.IsAny<DownloadClientItem>(), It.IsAny<bool>()), Times.Never);
        }

        private sealed class ActionContext
        {
            public List<HygieneAction> Actions { get; } = new();
            public bool GateEnabled { get; set; }
            public QueueItem QueueItem { get; }
            public TrackedDownload TrackedDownload { get; }
            public Mock<IDownloadClient> DownloadClient { get; } = new();
            public Mock<ITrackedDownloadService> TrackedDownloads { get; } = new();
            public Mock<IFailedDownloadService> FailedDownloads { get; } = new();
            public Mock<IHygieneQueueDecisionService> Decisions { get; } = new();
            public IHygieneQueueActionService Service { get; }

            public ActionContext(MediaKind kind, bool gateEnabled)
            {
                GateEnabled = gateEnabled;
                var clientItem = new DownloadClientItem { DownloadId = "test-download", CanBeRemoved = true };
                QueueItem = new QueueItem
                {
                    Id = 12,
                    DownloadClientId = 4,
                    DownloadId = clientItem.DownloadId,
                    Title = "Synthetic recovery proof",
                    Status = QueueStatus.Failed
                };
                TrackedDownload = new TrackedDownload
                {
                    DownloadClient = 4,
                    DownloadItem = clientItem,
                    Kind = kind,
                    State = TrackedDownloadState.Downloading
                };

                var clientItems = new List<DownloadClientItem> { clientItem };
                DownloadClient.Setup(x => x.GetItems()).Returns(clientItems);
                var downloadProvider = new Mock<IProvideDownloadClient>();
                downloadProvider.Setup(x => x.Get(4)).Returns(DownloadClient.Object);
                TrackedDownloads.Setup(x => x.Find(clientItem.DownloadId)).Returns(TrackedDownload);
                var eligibleAssessment = new HygieneQueueAssessment
                {
                    QueueId = QueueItem.Id,
                    DownloadClientId = QueueItem.DownloadClientId,
                    DownloadClient = "synthetic",
                    Kind = kind,
                    ReasonCode = "client-failed",
                    RuleId = 9,
                    Title = QueueItem.Title,
                    Eligible = true,
                    WouldAct = true
                };
                Decisions.Setup(x => x.Evaluate(It.IsAny<IEnumerable<QueueItem>>()))
                    .Returns((IEnumerable<QueueItem> queue) => queue.Any()
                        ? new List<HygieneQueueAssessment> { eligibleAssessment }
                        : new List<HygieneQueueAssessment>());
                var config = new Mock<IConfigService>();
                config.SetupGet(x => x.HygieneActionsEnabled).Returns(() => GateEnabled);
                var actionRepository = new Mock<IHygieneActionRepository>();
                actionRepository.Setup(x => x.All()).Returns(() => Actions);
                actionRepository.Setup(x => x.Insert(It.IsAny<HygieneAction>())).Returns((HygieneAction action) =>
                {
                    action.Id = Actions.Count + 1;
                    Actions.Add(action);
                    return action;
                });
                actionRepository.Setup(x => x.Update(It.IsAny<HygieneAction>())).Returns((HygieneAction action) => action);

                Service = new HygieneQueueActionService(
                    Decisions.Object,
                    actionRepository.Object,
                    config.Object,
                    TrackedDownloads.Object,
                    downloadProvider.Object,
                    FailedDownloads.Object,
                    TestLogger);
            }
        }
    }
}
