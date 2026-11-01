using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using QueueItem = NzbDrone.Core.Queue.Queue;

namespace NzbDrone.Core.Hygiene
{
    public class HygieneAction : NzbDrone.Core.Datastore.ModelBase
    {
        public int DownloadClientId { get; set; }
        public string DownloadId { get; set; }
        public int MediaKind { get; set; }
        public string ReasonCode { get; set; }
        public int RuleId { get; set; }
        public bool DeleteFilesOnFailure { get; set; }
        public bool SearchForReplacement { get; set; } = true;
        public string Title { get; set; }
        public string Status { get; set; }
        public string Checkpoint { get; set; }
        public string RecoveryPath { get; set; }
        public string LastError { get; set; }
        public int RetryCount { get; set; }
        public DateTime? NextAttemptAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int? ReplacementSearchCommandId { get; set; }
        public string ReplacementTargetIds { get; set; }
        public string ReplacementSearchStatus { get; set; }
        public int? ReplacementGrabCount { get; set; }
        public DateTime? ReplacementSearchEndedAt { get; set; }
    }

    public interface IHygieneQueueActionService
    {
        void Process(IEnumerable<QueueItem> queue);
        List<HygieneAction> Recent(int limit = 100);
    }

    public class HygieneQueueActionService : IHygieneQueueActionService, IHandle<CommandExecutedEvent>
    {
        private sealed class ActionStageException : Exception
        {
            public string Stage { get; }

            public ActionStageException(string stage, Exception innerException)
                : base(stage, innerException)
            {
                Stage = stage;
            }
        }

        private const string Processing = "processing";
        private const string Failed = "failed";
        private const string Paused = "paused";
        private const string Completed = "completed";
        private const string Prepared = "prepared";
        private const string ClientRemoved = "client-removed";
        private const string FailureRecorded = "failure-recorded";
        private const string ReplacementRequested = "replacement-requested";
        private const string ReplacementSkipped = "replacement-skipped";
        private const string Recovery = "Review the Hygiene journal. Confirm the client job and the matching media blocklist entry, then use the normal queue or search controls to finish recovery.";

        private readonly object _processLock = new();
        private readonly IHygieneQueueDecisionService _decisions;
        private readonly IHygieneActionRepository _actions;
        private readonly IConfigService _config;
        private readonly ITrackedDownloadService _trackedDownloads;
        private readonly IProvideDownloadClient _downloadClients;
        private readonly IFailedDownloadService _failedDownloads;
        private readonly IManageCommandQueue _commandQueue;
        private readonly IDownloadHistoryService _history;
        private readonly Logger _logger;

        public HygieneQueueActionService(
            IHygieneQueueDecisionService decisions,
            IHygieneActionRepository actions,
            IConfigService config,
            ITrackedDownloadService trackedDownloads,
            IProvideDownloadClient downloadClients,
            IFailedDownloadService failedDownloads,
            IManageCommandQueue commandQueue,
            IDownloadHistoryService history,
            Logger logger)
        {
            _decisions = decisions;
            _actions = actions;
            _config = config;
            _trackedDownloads = trackedDownloads;
            _downloadClients = downloadClients;
            _failedDownloads = failedDownloads;
            _commandQueue = commandQueue;
            _history = history;
            _logger = logger;
        }

        public void Process(IEnumerable<QueueItem> queue)
        {
            lock (_processLock)
            {
                var queueItems = queue.ToList();

                // Finish an earlier journal entry before considering the same queue item again.
                // This prevents a failed substep from being retried twice during one refresh.
                var resumed = ResumeInterruptedActions(queueItems);

                if (!_config.HygieneActionsEnabled)
                {
                    return;
                }

                foreach (var assessment in _decisions.Evaluate(queueItems).Where(x => x.WouldAct))
                {
                    Execute(assessment, queueItems, resumed);
                }
            }
        }

        public List<HygieneAction> Recent(int limit = 100)
        {
            return _actions.FindRecent(Math.Clamp(limit, 1, 500));
        }

        public void Handle(CommandExecutedEvent message)
        {
            var command = message.Command;
            foreach (var action in _actions.FindByReplacementSearchCommandId(command.Id))
            {
                RecordSearchResult(action, command);
            }
        }

        private void RecordSearchResult(HygieneAction action, CommandModel command)
        {
            if (command == null || command.Status is not (CommandStatus.Completed or CommandStatus.Failed or CommandStatus.Aborted or CommandStatus.Cancelled or CommandStatus.Orphaned))
            {
                return;
            }

            action.ReplacementSearchStatus = command.Status.ToString().ToLowerInvariant();
            action.ReplacementGrabCount = command.Status == CommandStatus.Completed ? command.Body?.GrabbedCount : null;
            action.ReplacementSearchEndedAt = command.EndedAt;
            _actions.SetFields(action, x => x.ReplacementSearchStatus, x => x.ReplacementGrabCount, x => x.ReplacementSearchEndedAt);
        }

        private void Execute(HygieneQueueAssessment assessment, IReadOnlyCollection<QueueItem> queue, ISet<int> alreadyResumed)
        {
            if (assessment.Kind == null || assessment.RuleId == null)
            {
                return;
            }

            var queueItem = queue.SingleOrDefault(x => x.Id == assessment.QueueId);
            if (queueItem == null || string.IsNullOrWhiteSpace(queueItem.DownloadId))
            {
                return;
            }

            var existing = _actions.FindLatest(queueItem.DownloadClientId, queueItem.DownloadId, assessment.ReasonCode);
            var repeatGrab = false;

            if (existing?.Status == Completed)
            {
                repeatGrab = HygieneActionOccurrence.WasGrabbedAgain(existing, _history.GetLatestGrab(queueItem.DownloadId));
                if (!repeatGrab)
                {
                    return;
                }

                existing = null;
            }

            if (existing != null && alreadyResumed.Contains(existing.Id))
            {
                return;
            }

            if (IsRetryDelayed(existing))
            {
                return;
            }

            var currentAssessment = CurrentEligibleAssessment(queueItem, assessment.ReasonCode, assessment.RuleId, assessment.Kind);
            if (currentAssessment == null)
            {
                return;
            }

            var action = existing ?? _actions.Insert(new HygieneAction
            {
                DownloadClientId = queueItem.DownloadClientId,
                DownloadId = queueItem.DownloadId,
                MediaKind = (int)assessment.Kind.Value,
                ReasonCode = assessment.ReasonCode,
                RuleId = assessment.RuleId ?? 0,
                DeleteFilesOnFailure = currentAssessment.DeleteFilesOnFailure,
                SearchForReplacement = currentAssessment.SearchForReplacement && !repeatGrab,
                Title = assessment.Title,
                Status = Processing,
                Checkpoint = Prepared,
                RecoveryPath = repeatGrab
                    ? "The same torrent hash was grabbed again after an earlier cleanup. Another replacement search was skipped to prevent a repeat loop. Review the indexer's blocked-hash setting before searching again."
                    : Recovery,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            action.Status = Processing;
            action.LastError = null;
            action.UpdatedAt = DateTime.UtcNow;
            _actions.SetFields(action, x => x.Status, x => x.LastError, x => x.UpdatedAt);

            try
            {
                var tracked = _trackedDownloads.Find(queueItem.DownloadId);
                if (!MatchesTrackedIdentity(tracked, queueItem.DownloadClientId, queueItem.DownloadId))
                {
                    throw new InvalidOperationException("The tracked download client identity changed.");
                }

                Continue(action, tracked, assessment.Kind, queueItem);
            }
            catch (Exception ex)
            {
                RecordFailure(action, ex);
            }
        }

        private HashSet<int> ResumeInterruptedActions(IReadOnlyCollection<QueueItem> queue)
        {
            var resumed = new HashSet<int>();

            foreach (var action in _actions.FindIncomplete())
            {
                if (!_config.HygieneActionsEnabled)
                {
                    Pause(action);
                    continue;
                }

                if (IsRetryDelayed(action))
                {
                    continue;
                }

                QueueItem queueItem = null;
                if (action.Checkpoint == Prepared)
                {
                    queueItem = queue.FirstOrDefault(x => x.DownloadClientId == action.DownloadClientId && x.DownloadId == action.DownloadId);
                    if (!CanStart(queueItem, action.ReasonCode, action.RuleId, (MediaKind)action.MediaKind))
                    {
                        Pause(action);
                        continue;
                    }
                }

                var tracked = _trackedDownloads.Find(action.DownloadId);
                if (tracked != null && !MatchesTrackedIdentity(tracked, action.DownloadClientId, action.DownloadId))
                {
                    if (action.Checkpoint is ReplacementRequested or ReplacementSkipped)
                    {
                        action.Status = Completed;
                        action.LastError = null;
                        action.UpdatedAt = DateTime.UtcNow;
                        _actions.SetFields(action, x => x.Status, x => x.LastError, x => x.UpdatedAt);
                    }
                    else
                    {
                        RecordFailure(action, new InvalidOperationException("The tracked download client identity changed."));
                    }

                    continue;
                }

                if (tracked == null)
                {
                    if (action.Checkpoint is ReplacementRequested or ReplacementSkipped)
                    {
                        action.Status = Completed;
                        action.LastError = null;
                        action.UpdatedAt = DateTime.UtcNow;
                        _actions.SetFields(action, x => x.Status, x => x.LastError, x => x.UpdatedAt);
                    }

                    continue;
                }

                try
                {
                    resumed.Add(action.Id);
                    action.Status = Processing;
                    action.LastError = null;
                    action.UpdatedAt = DateTime.UtcNow;
                    _actions.SetFields(action, x => x.Status, x => x.LastError, x => x.UpdatedAt);
                    Continue(action, tracked, (MediaKind)action.MediaKind, queueItem);
                }
                catch (Exception ex)
                {
                    RecordFailure(action, ex);
                }
            }

            return resumed;
        }

        private void Continue(HygieneAction action, TrackedDownload tracked, MediaKind? kind, QueueItem queueItem)
        {
            if (!GateIsOpen(action))
            {
                return;
            }

            var isStalledOrSlowReason = action.ReasonCode is "download-stalled" or "download-slow" or "download-unstable" or "download-metadata";
            var isStalledOrSlowTorrent = isStalledOrSlowReason &&
                                         tracked?.Protocol == NzbDrone.Core.Indexers.DownloadProtocol.Torrent;
            var isWarningTorrent = action.ReasonCode == "download-warning" &&
                                   tracked?.Protocol == NzbDrone.Core.Indexers.DownloadProtocol.Torrent;

            if (tracked == null || tracked.Kind != kind || tracked.State == TrackedDownloadState.Ignored ||
                tracked.DownloadItem == null || (isStalledOrSlowReason && !isStalledOrSlowTorrent) ||
                (!tracked.DownloadItem.CanBeRemoved && !isStalledOrSlowTorrent && !isWarningTorrent))
            {
                throw new InvalidOperationException("The tracked download is no longer eligible for removal.");
            }

            if (tracked.Protocol == NzbDrone.Core.Indexers.DownloadProtocol.Torrent &&
                tracked.RemoteRelease?.Release?.IndexerPrivacy != NzbDrone.Core.Indexers.IndexerPrivacy.Public)
            {
                throw new InvalidOperationException("Private or unverified torrent releases require manual review.");
            }

            var downloadClient = _downloadClients.Get(tracked.DownloadClient);
            if (action.Checkpoint == Prepared)
            {
                if (!GateIsOpen(action))
                {
                    return;
                }

                var currentAssessment = CurrentEligibleAssessment(queueItem, action.ReasonCode, action.RuleId, kind);
                if (currentAssessment == null)
                {
                    Pause(action);
                    return;
                }

                // An edited rule may turn deletion off before a retry. Never turn it on
                // for an action that was originally prepared to keep source files.
                if (action.DeleteFilesOnFailure && !currentAssessment.DeleteFilesOnFailure)
                {
                    action.DeleteFilesOnFailure = false;
                    _actions.SetFields(action, x => x.DeleteFilesOnFailure);
                }

                if (action.SearchForReplacement && !currentAssessment.SearchForReplacement)
                {
                    action.SearchForReplacement = false;
                    _actions.SetFields(action, x => x.SearchForReplacement);
                }

                IEnumerable<DownloadClientItem> clientItems;
                try
                {
                    clientItems = downloadClient.GetItems();
                }
                catch (Exception ex)
                {
                    throw new ActionStageException("client-list", ex);
                }

                var clientItem = clientItems.FirstOrDefault(x => x.DownloadId == tracked.DownloadItem.DownloadId);

                // A job may disappear, or metadata may complete, after the last
                // queue refresh. Do not remove without a current client item.
                if ((action.ReasonCode == "download-metadata" && clientItem?.DownloadClientDownloadingMetadata != true) ||
                    (action.ReasonCode == "download-warning" &&
                     (clientItem == null || (!isWarningTorrent && !clientItem.CanBeRemoved))))
                {
                    Pause(action);
                    return;
                }

                if (clientItem != null)
                {
                    try
                    {
                        downloadClient.RemoveItem(clientItem, action.DeleteFilesOnFailure);
                    }
                    catch (Exception ex)
                    {
                        throw new ActionStageException("client-removal", ex);
                    }

                    tracked.DownloadItem.Removed = true;
                }

                SetCheckpoint(action, ClientRemoved);
            }

            if (action.Checkpoint == ClientRemoved)
            {
                if (!GateIsOpen(action))
                {
                    return;
                }

                var failure = _failedDownloads.MarkAsFailed(tracked, !action.SearchForReplacement);
                if (failure != null)
                {
                    action.ReplacementTargetIds = string.Join(",", failure.ItemIds.Distinct().OrderBy(id => id));
                    action.ReplacementSearchCommandId = failure.ReplacementSearchCommandId;
                    _actions.SetFields(action, x => x.ReplacementTargetIds, x => x.ReplacementSearchCommandId);

                    if (failure.ReplacementSearchCommandId.HasValue)
                    {
                        // A fast command may finish before the action stores the link.
                        RecordSearchResult(action, _commandQueue.Get(failure.ReplacementSearchCommandId.Value));
                    }
                }

                if (!action.SearchForReplacement)
                {
                    action.ReplacementSearchStatus = "skipped";
                    _actions.SetFields(action, x => x.ReplacementSearchStatus);
                }

                SetCheckpoint(action, FailureRecorded);
            }

            if (action.Checkpoint == FailureRecorded)
            {
                if (!GateIsOpen(action))
                {
                    return;
                }

                SetCheckpoint(action, action.SearchForReplacement ? ReplacementRequested : ReplacementSkipped);
            }

            if (action.Checkpoint is ReplacementRequested or ReplacementSkipped)
            {
                if (!GateIsOpen(action))
                {
                    return;
                }

                _trackedDownloads.StopTracking(tracked.DownloadItem.DownloadId);
                action.Status = Completed;
                action.UpdatedAt = DateTime.UtcNow;
                _actions.SetFields(action, x => x.Status, x => x.UpdatedAt);
                _logger.Info("Hygiene recovered journal action {0} in {1} (reason: {2})", action.Id, kind, action.ReasonCode);
            }
        }

        private void RecordFailure(HygieneAction action, Exception ex)
        {
            action.Status = Failed;
            action.RetryCount++;
            action.NextAttemptAt = DateTime.UtcNow.AddMinutes(Math.Min(60, Math.Pow(2, action.RetryCount - 1)));

            // Download client errors may contain private paths or credentials.
            // Keep exception text out of logs and the journal.
            action.LastError = ex is ActionStageException stageException
                ? $"{stageException.InnerException?.GetType().Name ?? nameof(Exception)} ({stageException.Stage})"
                : ex.GetType().Name;
            action.UpdatedAt = DateTime.UtcNow;
            _actions.SetFields(action, x => x.Status, x => x.RetryCount, x => x.NextAttemptAt, x => x.LastError, x => x.UpdatedAt);
            _logger.Warn(
                "Hygiene action {0} failed for {1} ({2}, download client {3}) at checkpoint {4}; error type {5}. See the Hygiene action journal for recovery guidance.",
                action.Id,
                (MediaKind)action.MediaKind,
                action.ReasonCode,
                action.DownloadClientId,
                action.Checkpoint,
                action.LastError);
        }

        private void SetCheckpoint(HygieneAction action, string checkpoint)
        {
            action.Checkpoint = checkpoint;
            action.RetryCount = 0;
            action.NextAttemptAt = null;
            action.UpdatedAt = DateTime.UtcNow;
            _actions.SetFields(action, x => x.Checkpoint, x => x.RetryCount, x => x.NextAttemptAt, x => x.UpdatedAt);
        }

        private static bool IsRetryDelayed(HygieneAction action)
        {
            return action?.Status == Failed && action.NextAttemptAt > DateTime.UtcNow;
        }

        private bool GateIsOpen(HygieneAction action)
        {
            if (_config.HygieneActionsEnabled)
            {
                return true;
            }

            Pause(action);
            return false;
        }

        private bool CanStart(QueueItem queueItem, string reasonCode, int? ruleId, MediaKind? kind)
        {
            return CurrentEligibleAssessment(queueItem, reasonCode, ruleId, kind) != null;
        }

        private HygieneQueueAssessment CurrentEligibleAssessment(QueueItem queueItem, string reasonCode, int? ruleId, MediaKind? kind)
        {
            if (queueItem == null || queueItem.DownloadClientId <= 0 || string.IsNullOrWhiteSpace(queueItem.DownloadId))
            {
                return null;
            }

            var current = _decisions.Evaluate(new[] { queueItem }).SingleOrDefault();
            return current?.WouldAct == true && current.ReasonCode == reasonCode && current.RuleId == ruleId && current.Kind == kind
                ? current
                : null;
        }

        private void Pause(HygieneAction action)
        {
            action.Status = Paused;
            action.LastError = null;
            action.UpdatedAt = DateTime.UtcNow;
            _actions.SetFields(action, x => x.Status, x => x.LastError, x => x.UpdatedAt);
        }

        private static bool MatchesTrackedIdentity(TrackedDownload tracked, int downloadClientId, string downloadId)
        {
            return tracked != null &&
                   tracked.DownloadClient == downloadClientId &&
                   tracked.DownloadItem != null &&
                   tracked.DownloadItem.DownloadId == downloadId;
        }
    }
}
