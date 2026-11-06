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
        public string ContentActionMode { get; set; }
        public string QueueActionMode { get; set; }
        public string ClientRemovalStatus { get; set; }
        public string SourceDeletionStatus { get; set; }
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
        List<HygieneAction> Recent(int limit = 100, int offset = 0);
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
        private readonly IHygieneContentDecisionService _contentDecisions;
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
            IHygieneContentDecisionService contentDecisions,
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
            _contentDecisions = contentDecisions;
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

                var queueActions = _decisions.Evaluate(queueItems).Where(x => x.WouldAct).ToList();
                foreach (var assessment in queueActions)
                {
                    Execute(assessment, queueItems, resumed);
                }

                if (!_config.HygieneContentActionsEnabled)
                {
                    return;
                }

                foreach (var assessment in _contentDecisions.Evaluate(queueItems)
                             .Where(x => x.WouldAct && queueActions.All(y => y.QueueId != x.QueueId)))
                {
                    ExecuteContent(assessment, queueItems, resumed);
                }
            }
        }

        public List<HygieneAction> Recent(int limit = 100, int offset = 0)
        {
            return _actions.FindRecent(Math.Clamp(limit, 1, 500), Math.Max(offset, 0));
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
                DeleteFilesOnFailure = currentAssessment.ActionMode != "category" && currentAssessment.DeleteFilesOnFailure,
                SearchForReplacement = currentAssessment.ActionMode != "category" && currentAssessment.SearchForReplacement && !repeatGrab,
                QueueActionMode = currentAssessment.ActionMode,
                ReplacementSearchStatus = currentAssessment.ActionMode == "category" ? "skipped" : null,
                Title = assessment.Title,
                Status = Processing,
                Checkpoint = Prepared,
                RecoveryPath = currentAssessment.ActionMode == "category"
                    ? "Confirm the client job's category. If it is not in the configured post-import category, review the rule and client settings before retrying."
                    : repeatGrab
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

        private void ExecuteContent(HygieneContentAssessment assessment, IReadOnlyCollection<QueueItem> queue, ISet<int> alreadyResumed)
        {
            var queueItem = queue.SingleOrDefault(x => x.Id == assessment.QueueId);
            if (queueItem?.RemoteRelease?.Kind == null)
            {
                return;
            }

            var existing = _actions.FindLatest(queueItem.DownloadClientId, queueItem.DownloadId, "content-blocked");
            var repeatGrab = existing?.Status == Completed &&
                             HygieneActionOccurrence.WasGrabbedAgain(existing, _history.GetLatestGrab(queueItem.DownloadId));
            if ((existing?.Status == Completed && !repeatGrab) ||
                (existing != null && alreadyResumed.Contains(existing.Id)) ||
                IsRetryDelayed(existing))
            {
                return;
            }

            var current = CurrentEligibleContent(queueItem, assessment.Status);
            if (current == null)
            {
                return;
            }

            var action = existing != null && !repeatGrab ? existing : _actions.Insert(new HygieneAction
            {
                DownloadClientId = queueItem.DownloadClientId,
                DownloadId = queueItem.DownloadId,
                MediaKind = (int)queueItem.RemoteRelease.Kind,
                ReasonCode = "content-blocked",
                RuleId = 0,
                ContentActionMode = current.WouldRemoveDownload ? "remove" : "skip",
                DeleteFilesOnFailure = current.WouldRemoveDownload && _config.HygieneContentDeleteSourceFiles,
                SearchForReplacement = current.WouldRemoveDownload && !repeatGrab,
                ReplacementSearchStatus = current.WouldRemoveDownload ? null : "skipped",
                Title = queueItem.Title,
                Status = Processing,
                Checkpoint = Prepared,
                RecoveryPath = current.WouldRemoveDownload ? Recovery :
                    "Review the client file priorities and the Hygiene content list before retrying.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            try
            {
                var tracked = _trackedDownloads.Find(queueItem.DownloadId);
                if (!MatchesTrackedIdentity(tracked, queueItem.DownloadClientId, queueItem.DownloadId))
                {
                    throw new InvalidOperationException("The tracked download client identity changed.");
                }

                Continue(action, tracked, queueItem.RemoteRelease.Kind, queueItem);
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
                    if (action.ContentActionMode == null
                        ? !CanStart(queueItem, action.ReasonCode, action.RuleId, (MediaKind)action.MediaKind)
                        : CurrentEligibleContent(queueItem, action.ContentActionMode == "remove" ? "remove-download" : "skip-blocked-files") == null)
                    {
                        Pause(action);
                        continue;
                    }
                }

                var tracked = _trackedDownloads.Find(action.DownloadId);
                if (tracked != null && !MatchesTrackedIdentity(tracked, action.DownloadClientId, action.DownloadId))
                {
                    if (action.Checkpoint is ReplacementRequested or ReplacementSkipped or "files-skipped" or "category-changed")
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
                    if (action.Checkpoint is ReplacementRequested or ReplacementSkipped or "files-skipped" or "category-changed")
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

            if (action.Checkpoint is "files-skipped" or "category-changed")
            {
                if (action.Checkpoint == "category-changed" && tracked?.DownloadItem != null)
                {
                    _trackedDownloads.StopTracking(tracked.DownloadItem.DownloadId);
                }

                action.Status = Completed;
                action.UpdatedAt = DateTime.UtcNow;
                _actions.SetFields(action, x => x.Status, x => x.UpdatedAt);
                return;
            }

            var isStalledOrSlowReason = action.ReasonCode is "download-stalled" or "download-slow" or "download-unstable" or "download-metadata";
            var isStalledOrSlowTorrent = isStalledOrSlowReason &&
                                         tracked?.Protocol == NzbDrone.Core.Indexers.DownloadProtocol.Torrent;
            var isWarningTorrent = action.ReasonCode == "download-warning" &&
                                   tracked?.Protocol == NzbDrone.Core.Indexers.DownloadProtocol.Torrent;

            if (tracked == null || tracked.Kind != kind || tracked.State == TrackedDownloadState.Ignored ||
                tracked.DownloadItem == null || (isStalledOrSlowReason && !isStalledOrSlowTorrent) ||
                (!tracked.DownloadItem.CanBeRemoved && !isStalledOrSlowTorrent && !isWarningTorrent &&
                 action.ContentActionMode == null && action.QueueActionMode != "category"))
            {
                throw new InvalidOperationException("The tracked download is no longer eligible for removal.");
            }

            var privacy = tracked.RemoteRelease?.Release?.IndexerPrivacy;
            if (tracked.Protocol == NzbDrone.Core.Indexers.DownloadProtocol.Torrent &&
                action.Checkpoint == Prepared && queueItem?.RemoteRelease?.Release?.IndexerPrivacy != privacy)
            {
                Pause(action);
                return;
            }

            if (tracked.Protocol == NzbDrone.Core.Indexers.DownloadProtocol.Torrent &&
                (privacy == null ||
                 (privacy != NzbDrone.Core.Indexers.IndexerPrivacy.Public &&
                  (action.ContentActionMode != null || action.QueueActionMode == null))))
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

                var currentAssessment = action.ContentActionMode == null
                    ? CurrentEligibleAssessment(queueItem, action.ReasonCode, action.RuleId, kind)
                    : null;
                var contentAssessment = action.ContentActionMode == null
                    ? null
                    : CurrentEligibleContent(queueItem, action.ContentActionMode == "remove" ? "remove-download" : "skip-blocked-files");
                if ((currentAssessment == null && contentAssessment == null) ||
                    (currentAssessment != null && currentAssessment.ActionMode != (action.QueueActionMode ?? "remove")))
                {
                    Pause(action);
                    return;
                }

                // An edited rule may turn deletion off before a retry. Never turn it on
                // for an action that was originally prepared to keep source files.
                if (action.DeleteFilesOnFailure &&
                    (action.ContentActionMode == null ? !currentAssessment.DeleteFilesOnFailure : !_config.HygieneContentDeleteSourceFiles))
                {
                    action.DeleteFilesOnFailure = false;
                    _actions.SetFields(action, x => x.DeleteFilesOnFailure);
                }

                if (action.SearchForReplacement && action.ContentActionMode == null && !currentAssessment.SearchForReplacement)
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

                if (currentAssessment != null && clientItem != null && HygieneRuleSafeguards.Ignored(
                    new QueueItem
                    {
                        DownloadId = clientItem.DownloadId,
                        DownloadCategory = clientItem.Category,
                        DownloadTags = clientItem.Tags,
                        DownloadTracker = clientItem.Tracker,
                        DownloadClient = clientItem.DownloadClientInfo?.Name
                    },
                    currentAssessment.IgnoredDownloads))
                {
                    Pause(action);
                    return;
                }

                // A job may disappear, or metadata may complete, after the last
                // queue refresh. Do not remove without a current client item.
                if ((action.ReasonCode == "download-metadata" && clientItem?.DownloadClientDownloadingMetadata != true) ||
                    (action.ReasonCode == "download-warning" &&
                     (clientItem == null || (!isWarningTorrent && !clientItem.CanBeRemoved))))
                {
                    Pause(action);
                    return;
                }

                if (action.QueueActionMode == "category")
                {
                    if (downloadClient is not IHygieneCategoryClient categoryClient)
                    {
                        Pause(action);
                        return;
                    }

                    try
                    {
                        if (categoryClient.ChangeToPostImportCategory(action.DownloadId, kind.Value) != "observed-category")
                        {
                            throw new InvalidOperationException("The post-import category was not verified.");
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new ActionStageException("category-change", ex);
                    }

                    SetCheckpoint(action, "category-changed");
                    _trackedDownloads.StopTracking(tracked.DownloadItem.DownloadId);
                    action.Status = Completed;
                    action.UpdatedAt = DateTime.UtcNow;
                    _actions.SetFields(action, x => x.Status, x => x.UpdatedAt);
                    return;
                }

                if (action.ContentActionMode == "skip")
                {
                    if (clientItem == null || downloadClient is not IHygieneFileClient fileClient)
                    {
                        Pause(action);
                        return;
                    }

                    try
                    {
                        var result = fileClient.SkipHygieneFiles(
                            action.DownloadId,
                            contentAssessment.BlockedFiles,
                            contentAssessment.AllowedFiles);
                        if (result != "observed-skipped")
                        {
                            throw new InvalidOperationException("Blocked file priorities were not verified.");
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new ActionStageException("file-skip", ex);
                    }

                    SetCheckpoint(action, "files-skipped");
                    action.Status = Completed;
                    action.UpdatedAt = DateTime.UtcNow;
                    _actions.SetFields(action, x => x.Status, x => x.UpdatedAt);
                    return;
                }

                if (clientItem != null)
                {
                    try
                    {
                        if (downloadClient is IReportDownloadRemovalEvidence reportingClient)
                        {
                            var evidence = reportingClient.RemoveItemWithEvidence(clientItem, action.DeleteFilesOnFailure);
                            action.ClientRemovalStatus = evidence.ClientStatus is "observed-absent" or "still-listed"
                                ? evidence.ClientStatus
                                : "unverified";
                            action.SourceDeletionStatus = !action.DeleteFilesOnFailure
                                ? "not-requested"
                                : evidence.SourceStatus is "observed-absent" or "observed-present"
                                    ? evidence.SourceStatus
                                    : "unverified";
                        }
                        else
                        {
                            downloadClient.RemoveItem(clientItem, action.DeleteFilesOnFailure);
                            action.ClientRemovalStatus = "request-returned";
                            action.SourceDeletionStatus = action.DeleteFilesOnFailure ? "unverified" : "not-requested";
                        }

                        _actions.SetFields(action, x => x.ClientRemovalStatus, x => x.SourceDeletionStatus);

                        if (action.ClientRemovalStatus == "still-listed")
                        {
                            throw new InvalidOperationException("The download client still lists the job after removal.");
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new ActionStageException("client-removal", ex);
                    }

                    tracked.DownloadItem.Removed = true;
                }
                else
                {
                    action.ClientRemovalStatus = "not-listed-before-request";
                    action.SourceDeletionStatus = action.DeleteFilesOnFailure ? "unverified" : "not-requested";
                    _actions.SetFields(action, x => x.ClientRemovalStatus, x => x.SourceDeletionStatus);
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

        private HygieneContentAssessment CurrentEligibleContent(QueueItem queueItem, string expectedStatus)
        {
            if (queueItem == null || !_config.HygieneContentActionsEnabled)
            {
                return null;
            }

            var current = _contentDecisions.Evaluate(new[] { queueItem }).SingleOrDefault();
            return current?.WouldAct == true && current.Status == expectedStatus ? current : null;
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
