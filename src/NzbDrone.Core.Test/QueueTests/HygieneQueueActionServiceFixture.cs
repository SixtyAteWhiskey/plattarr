using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Blocklisting;
using NzbDrone.Core.Tv.Downloads;
using NzbDrone.Core.Tv.History;
using NzbDrone.Core.Tv.Search;
using NzbDrone.Test.Common;
using QueueItem = NzbDrone.Core.Queue.Queue;

namespace NzbDrone.Core.Test.QueueTests
{
    [TestFixture]
    public class HygieneQueueActionServiceFixture : CoreTest
    {
        [Test]
        public void preview_candidates_match_new_actions_for_the_same_queue_snapshot()
        {
            var context = new ActionContext(MediaKind.Movies, true);
            context.QueueItem.RemoteRelease = new NzbDrone.Core.Movies.Parser.Model.RemoteMovie();
            var belowLimit = new QueueItem
            {
                Id = 13,
                DownloadClientId = 4,
                DownloadId = "below-limit",
                Status = QueueStatus.Failed,
                RemoteRelease = new NzbDrone.Core.Movies.Parser.Model.RemoteMovie()
            };
            var privateTorrent = new QueueItem
            {
                Id = 14,
                DownloadClientId = 4,
                DownloadId = "private-torrent",
                Status = QueueStatus.Failed,
                Protocol = NzbDrone.Core.Indexers.DownloadProtocol.Torrent,
                RemoteRelease = new NzbDrone.Core.Movies.Parser.Model.RemoteMovie
                {
                    Release = new NzbDrone.Core.Parser.Model.ReleaseInfo
                    {
                        IndexerPrivacy = NzbDrone.Core.Indexers.IndexerPrivacy.Private
                    }
                }
            };
            var queue = new[] { context.QueueItem, belowLimit, privateTorrent };
            var states = new Mock<IHygieneQueueStateRepository>();
            states.Setup(x => x.FindByIdentities(It.IsAny<IEnumerable<(int, string)>>()))
                .Returns((IEnumerable<(int, string)> identities) => new[]
                {
                    new HygieneQueueState { DownloadClientId = 4, DownloadId = "test-download", ReasonCode = "client-failed", Strikes = 3 },
                    new HygieneQueueState { DownloadClientId = 4, DownloadId = "below-limit", ReasonCode = "client-failed", Strikes = 2 },
                    new HygieneQueueState { DownloadClientId = 4, DownloadId = "private-torrent", ReasonCode = "client-failed", Strikes = 3 }
                }.Where(state => identities.Contains((state.DownloadClientId, state.DownloadId))).ToList());
            var rule = new HygieneRule { Id = 9, ReasonCode = "client-failed", Enabled = true, StrikeLimit = 3, StrikeIntervalMinutes = 5 };
            var rules = new Mock<IHygieneRuleRepository>();
            rules.Setup(x => x.All()).Returns(() => new List<HygieneRule> { rule });
            var config = new Mock<IConfigService>();
            config.SetupGet(x => x.HygieneActionsEnabled).Returns(() => context.GateEnabled);
            var decisions = new HygieneQueueDecisionService(
                new HygieneQueueEvaluator(), states.Object,
                new HygieneRuleService(rules.Object, TestLogger), config.Object);
            context.Decisions.Setup(x => x.Evaluate(It.IsAny<IEnumerable<QueueItem>>()))
                .Returns((IEnumerable<QueueItem> items) => decisions.Evaluate(items));

            var preview = decisions.Evaluate(queue);

            preview.Where(x => x.WouldAct).Select(x => x.QueueId).Should().Equal(context.QueueItem.Id);
            preview.Single(x => x.QueueId == belowLimit.Id).Eligible.Should().BeFalse();
            preview.Single(x => x.QueueId == privateTorrent.Id).Eligible.Should().BeTrue();
            preview.Single(x => x.QueueId == privateTorrent.Id).WouldAct.Should().BeFalse();

            context.Service.Process(queue);

            context.Actions.Should().ContainSingle().Which.DownloadId.Should().Be(context.QueueItem.DownloadId);
            context.Actions[0].Status.Should().Be("completed");
            context.DownloadClient.Verify(x => x.RemoveItem(It.IsAny<DownloadClientItem>(), false), Times.Once);
        }

        [Test]
        public void rule_disabled_after_preview_prevents_a_new_action()
        {
            var context = new ActionContext(MediaKind.Movies, true);
            context.QueueItem.RemoteRelease = new NzbDrone.Core.Movies.Parser.Model.RemoteMovie();
            var states = new Mock<IHygieneQueueStateRepository>();
            states.Setup(x => x.FindByIdentities(It.IsAny<IEnumerable<(int, string)>>()))
                .Returns(new[] { new HygieneQueueState { DownloadClientId = 4, DownloadId = "test-download", ReasonCode = "client-failed", Strikes = 3 } });
            var rule = new HygieneRule { Id = 9, ReasonCode = "client-failed", Enabled = true, StrikeLimit = 3, StrikeIntervalMinutes = 5 };
            var rules = new Mock<IHygieneRuleRepository>();
            rules.Setup(x => x.All()).Returns(() => new List<HygieneRule> { rule });
            var config = new Mock<IConfigService>();
            config.SetupGet(x => x.HygieneActionsEnabled).Returns(() => context.GateEnabled);
            var decisions = new HygieneQueueDecisionService(
                new HygieneQueueEvaluator(), states.Object,
                new HygieneRuleService(rules.Object, TestLogger), config.Object);
            context.Decisions.Setup(x => x.Evaluate(It.IsAny<IEnumerable<QueueItem>>()))
                .Returns((IEnumerable<QueueItem> items) => decisions.Evaluate(items));

            decisions.Evaluate(new[] { context.QueueItem }).Should().ContainSingle().Which.WouldAct.Should().BeTrue();
            rule.Enabled = false;

            context.Service.Process(new[] { context.QueueItem });

            context.Actions.Should().BeEmpty();
            context.DownloadClient.Verify(x => x.RemoveItem(It.IsAny<DownloadClientItem>(), It.IsAny<bool>()), Times.Never);
        }

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
            context.Actions[0].DeleteFilesOnFailure.Should().BeFalse();
        }

        [TestCase(MediaKind.Movies)]
        [TestCase(MediaKind.Tv)]
        [TestCase(MediaKind.Music)]
        public void opted_in_rule_removes_client_job_and_source_files(MediaKind kind)
        {
            var context = new ActionContext(kind, true, "download-stalled", deleteFilesOnFailure: true);

            context.Service.Process(new[] { context.QueueItem });

            context.DownloadClient.Verify(x => x.RemoveItem(It.IsAny<DownloadClientItem>(), true), Times.Once);
            context.DownloadClient.Verify(x => x.RemoveItem(It.IsAny<DownloadClientItem>(), false), Times.Never);
            context.Actions.Should().ContainSingle().Which.DeleteFilesOnFailure.Should().BeTrue();
            context.Actions[0].Status.Should().Be("completed");
        }

        [TestCase("download-stalled")]
        [TestCase("download-slow")]
        [TestCase("download-unstable")]
        public void eligible_stalled_or_slow_torrent_is_removed_without_deleting_files(string reasonCode)
        {
            var context = new ActionContext(MediaKind.Movies, true, reasonCode);

            context.Service.Process(new[] { context.QueueItem });

            context.DownloadClient.Verify(x => x.RemoveItem(It.IsAny<DownloadClientItem>(), false), Times.Once);
            context.FailedDownloads.Verify(x => x.MarkAsFailed(context.TrackedDownload, false), Times.Once);
            context.TrackedDownloads.Verify(x => x.StopTracking("test-download"), Times.Once);
            context.Actions.Should().ContainSingle().Which.Status.Should().Be("completed");
            context.Actions[0].ReasonCode.Should().Be(reasonCode);
            context.Actions[0].Checkpoint.Should().Be("replacement-requested");
        }

        [Test]
        public void stalled_tv_torrent_is_marked_failed_without_suppressing_redownload()
        {
            var context = new ActionContext(MediaKind.Tv, true, "download-stalled");
            TrackedDownload failedDownload = null;
            bool? skipRedownload = null;
            context.FailedDownloads
                .Setup(x => x.MarkAsFailed(It.IsAny<TrackedDownload>(), It.IsAny<bool>()))
                .Callback<TrackedDownload, bool>((download, skip) =>
                {
                    failedDownload = download;
                    skipRedownload = skip;
                });

            context.Service.Process(new[] { context.QueueItem });

            context.DownloadClient.Verify(x => x.RemoveItem(It.IsAny<DownloadClientItem>(), false), Times.Once);
            failedDownload.Should().BeSameAs(context.TrackedDownload);
            failedDownload.Kind.Should().Be(MediaKind.Tv);
            skipRedownload.Should().BeFalse();
            context.Actions.Should().ContainSingle().Which.Status.Should().Be("completed");
            context.Actions[0].Checkpoint.Should().Be("replacement-requested");
        }

        [TestCase(MediaKind.Movies)]
        [TestCase(MediaKind.Tv)]
        [TestCase(MediaKind.Music)]
        public void rule_can_block_replacement_search_without_skipping_failure_recording(MediaKind kind)
        {
            var context = new ActionContext(kind, true, "download-stalled", searchForReplacement: false);

            context.Service.Process(new[] { context.QueueItem });

            context.DownloadClient.Verify(x => x.RemoveItem(It.IsAny<DownloadClientItem>(), false), Times.Once);
            context.FailedDownloads.Verify(x => x.MarkAsFailed(context.TrackedDownload, true), Times.Once);
            context.TrackedDownloads.Verify(x => x.StopTracking("test-download"), Times.Once);
            context.Actions.Should().ContainSingle();
            context.Actions[0].Status.Should().Be("completed");
            context.Actions[0].Checkpoint.Should().Be("replacement-skipped");
            context.Actions[0].SearchForReplacement.Should().BeFalse();
            context.Actions[0].ReplacementSearchStatus.Should().Be("skipped");
            context.Actions[0].ReplacementSearchCommandId.Should().BeNull();
        }

        [Test]
        public void linked_search_completion_records_zero_grabs_separately_from_action_completion()
        {
            var context = new ActionContext(MediaKind.Tv, true);
            context.FailedDownloads
                .Setup(x => x.MarkAsFailed(context.TrackedDownload, false))
                .Returns(new DownloadFailedEvent
                {
                    Kind = MediaKind.Tv,
                    ItemIds = new List<int> { 7, 1, 2 },
                    ReplacementSearchCommandId = 83
                });
            var command = new CommandModel
            {
                Id = 83,
                Status = CommandStatus.Completed,
                EndedAt = DateTime.UtcNow,
                Body = new EpisodeSearchCommand(new List<int> { 1, 2, 7 }) { GrabbedCount = 0 }
            };
            context.CommandQueue.Setup(x => x.Get(83)).Returns(command);

            context.Service.Process(new[] { context.QueueItem });

            context.Actions.Should().ContainSingle();
            context.Actions[0].Status.Should().Be("completed");
            context.Actions[0].ReplacementTargetIds.Should().Be("1,2,7");
            context.Actions[0].ReplacementSearchCommandId.Should().Be(83);
            context.Actions[0].ReplacementSearchStatus.Should().Be("completed");
            context.Actions[0].ReplacementGrabCount.Should().Be(0);
        }

        [Test]
        public void later_search_completion_records_grab_count_without_changing_cleanup_status()
        {
            var context = new ActionContext(MediaKind.Tv, true);
            context.FailedDownloads
                .Setup(x => x.MarkAsFailed(context.TrackedDownload, false))
                .Returns(new DownloadFailedEvent
                {
                    Kind = MediaKind.Tv,
                    ItemIds = new List<int> { 1, 2, 3, 4, 5, 6, 7 },
                    ReplacementSearchCommandId = 83
                });
            context.CommandQueue.Setup(x => x.Get(83)).Returns(new CommandModel
            {
                Id = 83,
                Status = CommandStatus.Queued,
                Body = new EpisodeSearchCommand(new List<int> { 1, 2, 3, 4, 5, 6, 7 })
            });
            context.Service.Process(new[] { context.QueueItem });

            var completed = new CommandModel
            {
                Id = 83,
                Status = CommandStatus.Completed,
                EndedAt = DateTime.UtcNow,
                Body = new EpisodeSearchCommand(new List<int> { 1, 2, 3, 4, 5, 6, 7 }) { GrabbedCount = 1 }
            };
            ((HygieneQueueActionService)context.Service).Handle(new CommandExecutedEvent(completed));

            context.Actions[0].Status.Should().Be("completed");
            context.Actions[0].ReplacementSearchStatus.Should().Be("completed");
            context.Actions[0].ReplacementGrabCount.Should().Be(1);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void stalled_tv_torrent_records_failure_and_applies_rule_search_choice(bool searchForReplacement)
        {
            var grabbedHistory = new EpisodeHistory
            {
                Id = 3,
                MediaKind = MediaKind.Tv,
                EventType = EpisodeHistoryEventType.Grabbed,
                ParentId = 42,
                ItemId = 314,
                DownloadId = "test-download",
                SourceTitle = "Synthetic.Show.S01E01.720p-GROUP",
                Quality = new NzbDrone.Core.Qualities.QualityModel(NzbDrone.Core.Qualities.Quality.HDTV720p),
                Languages = new List<NzbDrone.Core.Languages.Language>(),
                Date = DateTime.UtcNow
            };
            grabbedHistory.Data[EpisodeHistory.RELEASE_SOURCE] = NzbDrone.Core.Parser.Model.ReleaseSourceType.Rss.ToString();
            grabbedHistory.Data["publishedDate"] = "2026-10-01T12:00:00Z";
            grabbedHistory.Data["protocol"] = ((int)NzbDrone.Core.Indexers.DownloadProtocol.Torrent).ToString();
            grabbedHistory.Data["size"] = "1073741824";
            grabbedHistory.Data[EpisodeHistory.INDEXER] = "synthetic-indexer";

            var history = new List<EpisodeHistory>();
            var historyRepository = new Mock<IHistoryRepository>();
            historyRepository.Setup(x => x.Insert(It.IsAny<EpisodeHistory>()))
                .Callback<EpisodeHistory>(history.Add)
                .Returns<EpisodeHistory>(item => item);
            var historyService = new NzbDrone.Core.Tv.History.HistoryService(historyRepository.Object, TestLogger);

            var blocklist = new List<NzbDrone.Core.Tv.Blocklisting.Blocklist>();
            var blocklistRepository = new Mock<IBlocklistRepository>();
            blocklistRepository.Setup(x => x.Insert(It.IsAny<NzbDrone.Core.Tv.Blocklisting.Blocklist>()))
                .Callback<NzbDrone.Core.Tv.Blocklisting.Blocklist>(blocklist.Add)
                .Returns<NzbDrone.Core.Tv.Blocklisting.Blocklist>(item => item);
            var blocklistService = new NzbDrone.Core.Tv.Blocklisting.BlocklistService(blocklistRepository.Object);

            EpisodeSearchCommand replacementSearch = null;
            var commandQueue = new Mock<IManageCommandQueue>();
            commandQueue.Setup(x => x.Push(It.IsAny<EpisodeSearchCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()))
                .Callback<EpisodeSearchCommand, CommandPriority, CommandTrigger>((command, _, _) => replacementSearch = command)
                .Returns((CommandModel)null);
            var searcher = new TvRedownloadSearcher(new Mock<IEpisodeService>().Object, commandQueue.Object, TestLogger);
            var config = new Mock<IConfigService>();
            config.SetupGet(x => x.AutoRedownloadFailed).Returns(true);
            config.SetupGet(x => x.AutoRedownloadFailedFromInteractiveSearch).Returns(true);
            var redownload = new RedownloadFailedDownloadService(config.Object, new[] { (IRedownloadSearcher)searcher }, TestLogger);

            var historyReader = new Mock<IHistoryService>();
            historyReader.Setup(x => x.Find("test-download", EpisodeHistoryEventType.Grabbed))
                .Returns(new List<EpisodeHistory> { grabbedHistory });
            var tvSource = new TvFailedDownloadSource(historyReader.Object);
            var eventAggregator = new Mock<IEventAggregator>();
            eventAggregator.Setup(x => x.PublishEvent(It.IsAny<DownloadFailedEvent>()))
                .Callback<DownloadFailedEvent>(failure =>
                {
                    historyService.Handle(failure);
                    blocklistService.Handle(failure);
                    redownload.Handle(failure);
                });
            var failedDownloadService = new FailedDownloadService(new[] { (IFailedDownloadSource)tvSource }, eventAggregator.Object);

            var context = new ActionContext(MediaKind.Tv, true, "download-stalled", failedDownloadService, searchForReplacement: searchForReplacement);
            context.Service.Process(new[] { context.QueueItem });

            context.DownloadClient.Verify(x => x.RemoveItem(It.IsAny<DownloadClientItem>(), false), Times.Once);
            history.Should().ContainSingle().Which.EventType.Should().Be(EpisodeHistoryEventType.DownloadFailed);
            history[0].ParentId.Should().Be(42);
            history[0].ItemId.Should().Be(314);
            history[0].DownloadId.Should().Be("test-download");
            blocklist.Should().ContainSingle().Which.TorrentInfoHash.Should().Be("test-download");
            blocklist[0].ParentId.Should().Be(42);
            if (searchForReplacement)
            {
                replacementSearch.Should().NotBeNull();
                replacementSearch.EpisodeIds.Should().Equal(314);
            }
            else
            {
                replacementSearch.Should().BeNull();
            }

            context.Actions.Should().ContainSingle().Which.Status.Should().Be("completed");
            context.Actions[0].Checkpoint.Should().Be(searchForReplacement ? "replacement-requested" : "replacement-skipped");
        }

        [Test]
        public void global_gate_prevents_all_executor_work()
        {
            var context = new ActionContext(MediaKind.Movies, false, deleteFilesOnFailure: true);

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
            context.Actions[0].LastError.Should().Be("InvalidOperationException (client-removal)");
            context.Actions[0].RetryCount.Should().Be(1);
            context.Actions[0].NextAttemptAt.Should().BeAfter(DateTime.UtcNow);
            context.FailedDownloads.Verify(x => x.MarkAsFailed(It.IsAny<TrackedDownload>(), It.IsAny<bool>()), Times.Never);

            context.Service.Process(new[] { context.QueueItem });
            context.DownloadClient.Verify(x => x.RemoveItem(It.IsAny<DownloadClientItem>(), false), Times.Once);

            context.Actions[0].NextAttemptAt = DateTime.UtcNow.AddSeconds(-1);
            context.Service.Process(new[] { context.QueueItem });

            context.DownloadClient.Verify(x => x.RemoveItem(It.IsAny<DownloadClientItem>(), false), Times.Exactly(2));
            context.FailedDownloads.Verify(x => x.MarkAsFailed(context.TrackedDownload, false), Times.Once);
            context.Actions[0].Status.Should().Be("completed");
        }

        [Test]
        public void disabling_file_deletion_before_a_prepared_retry_keeps_source_files()
        {
            var context = new ActionContext(MediaKind.Tv, true, deleteFilesOnFailure: true);
            var failFirstAttempt = true;
            context.DownloadClient
                .Setup(x => x.RemoveItem(It.IsAny<DownloadClientItem>(), true))
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
            context.Actions[0].Checkpoint.Should().Be("prepared");
            context.Actions[0].DeleteFilesOnFailure.Should().BeTrue();

            context.Assessment.DeleteFilesOnFailure = false;
            context.Actions[0].NextAttemptAt = DateTime.UtcNow.AddSeconds(-1);
            context.Service.Process(new[] { context.QueueItem });

            context.DownloadClient.Verify(x => x.RemoveItem(It.IsAny<DownloadClientItem>(), true), Times.Once);
            context.DownloadClient.Verify(x => x.RemoveItem(It.IsAny<DownloadClientItem>(), false), Times.Once);
            context.Actions[0].DeleteFilesOnFailure.Should().BeFalse();
            context.Actions[0].Status.Should().Be("completed");
        }

        [Test]
        public void disabling_search_before_a_prepared_retry_suppresses_replacement()
        {
            var context = new ActionContext(MediaKind.Tv, true, "download-stalled");
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
            context.Actions[0].Checkpoint.Should().Be("prepared");
            context.Actions[0].SearchForReplacement.Should().BeTrue();

            context.Assessment.SearchForReplacement = false;
            context.Actions[0].NextAttemptAt = DateTime.UtcNow.AddSeconds(-1);
            context.Service.Process(new[] { context.QueueItem });

            context.FailedDownloads.Verify(x => x.MarkAsFailed(context.TrackedDownload, true), Times.Once);
            context.Actions[0].SearchForReplacement.Should().BeFalse();
            context.Actions[0].Checkpoint.Should().Be("replacement-skipped");
            context.Actions[0].Status.Should().Be("completed");
        }

        [Test]
        public void enabling_search_does_not_escalate_an_existing_prepared_action()
        {
            var context = new ActionContext(MediaKind.Tv, true, "download-stalled", searchForReplacement: false);
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
            context.Actions[0].Checkpoint.Should().Be("prepared");
            context.Actions[0].SearchForReplacement.Should().BeFalse();

            context.Assessment.SearchForReplacement = true;
            context.Actions[0].NextAttemptAt = DateTime.UtcNow.AddSeconds(-1);
            context.Service.Process(new[] { context.QueueItem });

            context.FailedDownloads.Verify(x => x.MarkAsFailed(context.TrackedDownload, true), Times.Once);
            context.Actions[0].SearchForReplacement.Should().BeFalse();
            context.Actions[0].Checkpoint.Should().Be("replacement-skipped");
        }

        [Test]
        public void enabling_file_deletion_does_not_escalate_an_existing_prepared_action()
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
            context.Actions[0].Checkpoint.Should().Be("prepared");
            context.Actions[0].DeleteFilesOnFailure.Should().BeFalse();

            context.Assessment.DeleteFilesOnFailure = true;
            context.Actions[0].NextAttemptAt = DateTime.UtcNow.AddSeconds(-1);
            context.Service.Process(new[] { context.QueueItem });

            context.DownloadClient.Verify(x => x.RemoveItem(It.IsAny<DownloadClientItem>(), false), Times.Exactly(2));
            context.DownloadClient.Verify(x => x.RemoveItem(It.IsAny<DownloadClientItem>(), true), Times.Never);
            context.Actions[0].DeleteFilesOnFailure.Should().BeFalse();
            context.Actions[0].Status.Should().Be("completed");
        }

        [Test]
        public void failed_client_listing_records_safe_failure_stage_without_exception_details()
        {
            var context = new ActionContext(MediaKind.Tv, true);
            context.DownloadClient
                .Setup(x => x.GetItems())
                .Throws(new DownloadClientException("private path /not-for-logs"));

            context.Service.Process(new[] { context.QueueItem });
            ExceptionVerification.ExpectedWarns(1);

            context.Actions.Should().ContainSingle().Which.Status.Should().Be("failed");
            context.Actions[0].Checkpoint.Should().Be("prepared");
            context.Actions[0].LastError.Should().Be("DownloadClientException (client-list)");
            context.Actions[0].LastError.Should().NotContain("private path");
        }

        [Test]
        public void failed_client_removal_records_safe_failure_stage_without_exception_details()
        {
            var context = new ActionContext(MediaKind.Tv, true);
            context.DownloadClient
                .Setup(x => x.RemoveItem(It.IsAny<DownloadClientItem>(), false))
                .Throws(new DownloadClientException("private path /not-for-logs"));

            context.Service.Process(new[] { context.QueueItem });
            ExceptionVerification.ExpectedWarns(1);

            context.Actions.Should().ContainSingle().Which.Status.Should().Be("failed");
            context.Actions[0].Checkpoint.Should().Be("prepared");
            context.Actions[0].LastError.Should().Be("DownloadClientException (client-removal)");
            context.Actions[0].LastError.Should().NotContain("private path");
        }

        [Test]
        public void repeated_failures_use_exponential_retry_backoff_capped_at_one_hour()
        {
            var context = new ActionContext(MediaKind.Tv, true);
            context.DownloadClient
                .Setup(x => x.GetItems())
                .Throws(new DownloadClientException("client unavailable"));

            for (var attempt = 0; attempt < 7; attempt++)
            {
                context.Service.Process(new[] { context.QueueItem });
                context.Actions[0].NextAttemptAt = DateTime.UtcNow.AddSeconds(-1);
            }

            context.Service.Process(new[] { context.QueueItem });
            ExceptionVerification.ExpectedWarns(8);

            context.Actions[0].RetryCount.Should().Be(8);
            context.Actions[0].NextAttemptAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(59));
            context.Actions[0].NextAttemptAt.Should().BeBefore(DateTime.UtcNow.AddMinutes(61));
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

            context.Actions[0].NextAttemptAt = DateTime.UtcNow.AddSeconds(-1);
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

            context.Actions[0].NextAttemptAt = DateTime.UtcNow.AddSeconds(-1);
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
            public Mock<IManageCommandQueue> CommandQueue { get; } = new();
            public Mock<IHygieneQueueDecisionService> Decisions { get; } = new();
            public HygieneQueueAssessment Assessment { get; }
            public IHygieneQueueActionService Service { get; }

            public ActionContext(MediaKind kind, bool gateEnabled, string reasonCode = "client-failed", IFailedDownloadService failedDownloadService = null, bool deleteFilesOnFailure = false, bool searchForReplacement = true)
            {
                GateEnabled = gateEnabled;
                var isStalledOrSlow = reasonCode is "download-stalled" or "download-slow" or "download-unstable";
                var clientItem = new DownloadClientItem
                {
                    DownloadId = "test-download",
                    CanBeRemoved = !isStalledOrSlow,
                    DownloadClientInfo = new DownloadClientItemClientInfo
                    {
                        Id = 4,
                        Name = "synthetic",
                        Type = "qBittorrent",
                        Protocol = NzbDrone.Core.Indexers.DownloadProtocol.Torrent
                    }
                };
                QueueItem = new QueueItem
                {
                    Id = 12,
                    DownloadClientId = 4,
                    DownloadId = clientItem.DownloadId,
                    Title = "Synthetic recovery proof",
                    Status = isStalledOrSlow ? QueueStatus.Downloading : QueueStatus.Failed,
                    Protocol = isStalledOrSlow ? NzbDrone.Core.Indexers.DownloadProtocol.Torrent : NzbDrone.Core.Indexers.DownloadProtocol.Usenet,
                    RemoteRelease = isStalledOrSlow
                        ? new NzbDrone.Core.Movies.Parser.Model.RemoteMovie
                        {
                            Release = new NzbDrone.Core.Parser.Model.ReleaseInfo
                            {
                                IndexerPrivacy = NzbDrone.Core.Indexers.IndexerPrivacy.Public
                            }
                        }
                        : null
                };
                TrackedDownload = new TrackedDownload
                {
                    DownloadClient = 4,
                    DownloadItem = clientItem,
                    Kind = kind,
                    State = TrackedDownloadState.Downloading,
                    Protocol = isStalledOrSlow ? NzbDrone.Core.Indexers.DownloadProtocol.Torrent : NzbDrone.Core.Indexers.DownloadProtocol.Usenet,
                    RemoteRelease = QueueItem.RemoteRelease
                };

                var clientItems = new List<DownloadClientItem> { clientItem };
                DownloadClient.Setup(x => x.GetItems()).Returns(clientItems);
                var downloadProvider = new Mock<IProvideDownloadClient>();
                downloadProvider.Setup(x => x.Get(4)).Returns(DownloadClient.Object);
                TrackedDownloads.Setup(x => x.Find(clientItem.DownloadId)).Returns(TrackedDownload);
                Assessment = new HygieneQueueAssessment
                {
                    QueueId = QueueItem.Id,
                    DownloadClientId = QueueItem.DownloadClientId,
                    DownloadClient = "synthetic",
                    Kind = kind,
                    ReasonCode = reasonCode,
                    RuleId = 9,
                    Title = QueueItem.Title,
                    Eligible = true,
                    DeleteFilesOnFailure = deleteFilesOnFailure,
                    SearchForReplacement = searchForReplacement,
                    WouldAct = true
                };
                Decisions.Setup(x => x.Evaluate(It.IsAny<IEnumerable<QueueItem>>()))
                    .Returns((IEnumerable<QueueItem> queue) => queue.Any()
                        ? new List<HygieneQueueAssessment> { Assessment }
                        : new List<HygieneQueueAssessment>());
                var config = new Mock<IConfigService>();
                config.SetupGet(x => x.HygieneActionsEnabled).Returns(() => GateEnabled);
                var actionRepository = new Mock<IHygieneActionRepository>();
                actionRepository.Setup(x => x.All()).Returns(() => Actions);
                actionRepository.Setup(x => x.FindIncomplete())
                    .Returns(() => Actions.Where(action => action.Status != "completed").ToList());
                actionRepository.Setup(x => x.FindLatest(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns((int clientId, string downloadId, string reasonCode) => Actions
                        .Where(action => action.DownloadClientId == clientId && action.DownloadId == downloadId && action.ReasonCode == reasonCode)
                        .OrderByDescending(action => action.Id)
                        .FirstOrDefault());
                actionRepository.Setup(x => x.FindByReplacementSearchCommandId(It.IsAny<int>()))
                    .Returns((int commandId) => Actions.Where(action => action.ReplacementSearchCommandId == commandId).ToList());
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
                    failedDownloadService ?? FailedDownloads.Object,
                    CommandQueue.Object,
                    TestLogger);
            }
        }
    }
}
