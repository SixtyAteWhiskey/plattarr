using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Modules;
using QueueItem = NzbDrone.Core.Queue.Queue;

namespace NzbDrone.Core.Hygiene
{
    public interface IHygieneImportRescueService
    {
        void Process(IReadOnlyCollection<QueueItem> queue);
        List<HygieneImportRescue> Recent(int limit, int offset);
    }

    public interface IHygieneImportRescueAdapter
    {
        MediaKind Kind { get; }
        HygieneImportRescueCandidate Prepare(RemoteRelease release, string downloadId);
        bool ImportConfirmed(string downloadId, DateTime requestedAt, IReadOnlyCollection<int> targetIds);
    }

    public class HygieneImportRescueCandidate
    {
        public int[] TargetIds { get; init; }
        public string Fingerprint { get; init; }
        public Func<CommandModel> Push { get; init; }

        public static string ComputeFingerprint(IEnumerable<string> paths)
        {
            var ordered = paths.OrderBy(path => path, StringComparer.Ordinal);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", ordered))));
        }
    }

    public class HygieneImportRescueService : IHygieneImportRescueService
    {
        private static readonly string[] RescueReasons =
        {
            "matched to series by ID",
            "matched to movie by ID",
            "Unable to determine if file is a sample"
        };

        private readonly IConfigService _config;
        private readonly IHygieneRuleService _rules;
        private readonly IHygieneImportRescueRepository _rescues;
        private readonly IReadOnlyDictionary<MediaKind, IHygieneImportRescueAdapter> _adapters;
        private readonly IManageCommandQueue _commands;
        private readonly IDownloadHistoryService _grabHistory;
        private readonly Logger _logger;

        public HygieneImportRescueService(
            IConfigService config,
            IHygieneRuleService rules,
            IHygieneImportRescueRepository rescues,
            IEnumerable<IHygieneImportRescueAdapter> adapters,
            IManageCommandQueue commands,
            IDownloadHistoryService grabHistory,
            Logger logger)
        {
            _config = config;
            _rules = rules;
            _rescues = rescues;
            _adapters = adapters.ToDictionary(x => x.Kind);
            _commands = commands;
            _grabHistory = grabHistory;
            _logger = logger;
        }

        public List<HygieneImportRescue> Recent(int limit, int offset)
        {
            return _rescues.FindRecent(Math.Clamp(limit, 1, 500), Math.Max(offset, 0));
        }

        public void Process(IReadOnlyCollection<QueueItem> queue)
        {
            if (!_config.HygieneActionsEnabled)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var rules = _rules.All();
            foreach (var item in queue)
            {
                item.HygieneImportRescueStatus = null;
                if (item.TrackedDownloadState != TrackedDownloadState.ImportBlocked ||
                    item.RemoteRelease?.Kind is not (MediaKind.Movies or MediaKind.Tv) ||
                    item.DownloadClientId <= 0 || string.IsNullOrWhiteSpace(item.DownloadId))
                {
                    continue;
                }

                var rule = _rules.Match(rules, "import-blocked", (int)item.RemoteRelease.Kind, item.DownloadClientId);
                if (rule?.Enabled != true || !rule.ForceImportEnabled ||
                    HygieneRuleSafeguards.Ignored(item, rule.IgnoredDownloads) ||
                    (item.Protocol == DownloadProtocol.Torrent &&
                     item.RemoteRelease.Release?.IndexerPrivacy != IndexerPrivacy.Public) ||
                    !HasOnlyRescueReasons(item))
                {
                    continue;
                }

                try
                {
                    ProcessOne(item, rule, now);
                }
                catch (Exception ex)
                {
                    // A probe can contain paths and client data. Never persist its text.
                    item.HygieneImportRescueStatus = "probe-unavailable";
                    _logger.Warn(
                        "Hygiene import rescue probe failed for client {0}; error type {1}",
                        item.DownloadClientId,
                        ex.GetType().Name);
                }
            }

            foreach (var rescue in _rescues.FindPending().Where(x =>
                         queue.All(item => item.DownloadClientId != x.DownloadClientId || item.DownloadId != x.DownloadId)))
            {
                RefreshRequested(rescue, now);
            }
        }

        private void ProcessOne(QueueItem item, HygieneRule rule, DateTime now)
        {
            var rescue = _rescues.FindLatest(item.DownloadClientId, item.DownloadId);
            if (rescue != null && HygieneActionOccurrence.WasGrabbedAgain(
                    new HygieneAction { DownloadClientId = rescue.DownloadClientId, DownloadId = rescue.DownloadId, CreatedAt = rescue.CreatedAt },
                    _grabHistory.GetLatestGrab(item.DownloadId)))
            {
                rescue = null;
            }

            if (rescue?.Status == "requested")
            {
                RefreshRequested(rescue, now);
            }

            if (rescue?.Status is "confirmed" or "prepared" or "ambiguous")
            {
                item.HygieneImportRescueStatus = rescue.Status;
                return;
            }

            if (rescue?.Status == "requested" ||
                (rescue?.Status == "failed" && rescue.NextAttemptAt > now))
            {
                item.HygieneImportRescueStatus = rescue.Status;
                return;
            }

            if (rescue?.Status == "exhausted")
            {
                if (rescue.UpdatedAt.AddHours(6) > now)
                {
                    return;
                }

                rescue = null;
            }

            if (rescue?.Status == "failed" && rescue.Attempts >= rule.ForceImportMaxTries)
            {
                rescue.Status = "exhausted";
                rescue.UpdatedAt = now;
                _rescues.SetFields(rescue, x => x.Status, x => x.UpdatedAt);
                return;
            }

            if (ImportsAreRunning())
            {
                item.HygieneImportRescueStatus = "waiting-for-imports";
                return;
            }

            var candidate = Prepare(item);
            if (candidate == null)
            {
                // No safe set of files exists. Return to the ordinary strike path now.
                if (rescue?.Status == "settling")
                {
                    rescue.Status = "ineligible";
                    rescue.CandidateFingerprint = null;
                    rescue.UpdatedAt = now;
                    _rescues.SetFields(rescue, x => x.Status, x => x.CandidateFingerprint, x => x.UpdatedAt);
                }

                return;
            }

            var currentRule = _rules.Match(_rules.All(), "import-blocked", (int)item.RemoteRelease.Kind, item.DownloadClientId);
            if (!_config.HygieneActionsEnabled || currentRule?.Id != rule.Id ||
                currentRule.Enabled != true || !currentRule.ForceImportEnabled ||
                HygieneRuleSafeguards.Ignored(item, currentRule.IgnoredDownloads) ||
                rescue?.Attempts >= currentRule.ForceImportMaxTries)
            {
                return;
            }

            if (rescue == null)
            {
                _rescues.Insert(new HygieneImportRescue
                {
                    DownloadClientId = item.DownloadClientId,
                    DownloadId = item.DownloadId,
                    MediaKind = (int)item.RemoteRelease.Kind,
                    RuleId = rule.Id,
                    TargetIds = string.Join(",", candidate.TargetIds),
                    CandidateFingerprint = candidate.Fingerprint,
                    Status = "settling",
                    Attempts = 0,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                item.HygieneImportRescueStatus = "settling";
                return;
            }

            if (rescue.RuleId != rule.Id || rescue.TargetIds != string.Join(",", candidate.TargetIds) ||
                rescue.CandidateFingerprint != candidate.Fingerprint)
            {
                rescue.RuleId = rule.Id;
                rescue.TargetIds = string.Join(",", candidate.TargetIds);
                rescue.CandidateFingerprint = candidate.Fingerprint;
                rescue.Status = "settling";
                rescue.UpdatedAt = now;
                _rescues.SetFields(
                    rescue,
                    x => x.RuleId,
                    x => x.TargetIds,
                    x => x.CandidateFingerprint,
                    x => x.Status,
                    x => x.UpdatedAt);
                item.HygieneImportRescueStatus = "settling";
                return;
            }

            rescue.Status = "prepared";
            rescue.Attempts++;
            rescue.CommandId = null;
            rescue.RequestedAt = null;
            rescue.NextAttemptAt = null;
            rescue.UpdatedAt = now;
            _rescues.SetFields(
                rescue,
                x => x.Status,
                x => x.Attempts,
                x => x.CommandId,
                x => x.RequestedAt,
                x => x.NextAttemptAt,
                x => x.UpdatedAt);
            item.HygieneImportRescueStatus = "ambiguous";

            try
            {
                var command = candidate.Push();
                if (command == null || command.Id <= 0)
                {
                    throw new InvalidOperationException("Import command was not accepted.");
                }

                rescue.CommandId = command.Id;
                rescue.RequestedAt = command.QueuedAt == default ? DateTime.UtcNow : command.QueuedAt;
                rescue.Status = "requested";
                rescue.UpdatedAt = DateTime.UtcNow;
                _rescues.SetFields(rescue, x => x.CommandId, x => x.RequestedAt, x => x.Status, x => x.UpdatedAt);
                item.HygieneImportRescueStatus = "requested";
            }
            catch (Exception ex)
            {
                // A command may have been queued before the error. No blind retry.
                rescue.Status = "ambiguous";
                rescue.UpdatedAt = DateTime.UtcNow;
                _rescues.SetFields(rescue, x => x.Status, x => x.UpdatedAt);
                _logger.Warn(
                    "Hygiene import rescue request is ambiguous for client {0}; error type {1}",
                    item.DownloadClientId,
                    ex.GetType().Name);
            }
        }

        private void RefreshRequested(HygieneImportRescue rescue, DateTime now)
        {
            if (rescue.RequestedAt == null || rescue.CommandId == null)
            {
                rescue.Status = "ambiguous";
            }
            else if (ImportConfirmed(rescue))
            {
                rescue.Status = "confirmed";
            }
            else
            {
                var command = _commands.Get(rescue.CommandId.Value);
                if (command == null)
                {
                    rescue.Status = "ambiguous";
                }
                else if (command.Status is CommandStatus.Failed or CommandStatus.Aborted or CommandStatus.Cancelled or CommandStatus.Orphaned)
                {
                    rescue.Status = "failed";
                    rescue.NextAttemptAt = now.AddMinutes(5);
                }
                else if (command.Status == CommandStatus.Completed && rescue.RequestedAt.Value.AddHours(6) <= now)
                {
                    rescue.Status = "failed";
                    rescue.NextAttemptAt = now;
                }
            }

            rescue.UpdatedAt = now;
            _rescues.SetFields(rescue, x => x.Status, x => x.NextAttemptAt, x => x.UpdatedAt);
        }

        private bool ImportConfirmed(HygieneImportRescue rescue)
        {
            var targets = rescue.TargetIds?.Split(',').Select(x => int.TryParse(x, out var id) ? id : 0)
                .Where(x => x > 0).Distinct().ToHashSet();
            if (targets == null || targets.Count == 0 || rescue.RequestedAt == null)
            {
                return false;
            }

            return _adapters.TryGetValue((MediaKind)rescue.MediaKind, out var adapter) &&
                   adapter.ImportConfirmed(rescue.DownloadId, rescue.RequestedAt.Value, targets);
        }

        private bool ImportsAreRunning()
        {
            return _commands.GetStarted().Any(command =>
                command.Name?.Contains("Import", StringComparison.OrdinalIgnoreCase) == true ||
                command.Name?.Contains("Scan", StringComparison.OrdinalIgnoreCase) == true);
        }

        private HygieneImportRescueCandidate Prepare(QueueItem item)
        {
            return _adapters.TryGetValue(item.RemoteRelease.Kind, out var adapter) ?
                adapter.Prepare(item.RemoteRelease, item.DownloadId) : null;
        }

        private static bool HasOnlyRescueReasons(QueueItem item)
        {
            var reasons = item.StatusMessages?.SelectMany(x => x.Messages ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            return reasons is { Count: > 0 } && reasons.All(reason =>
                RescueReasons.Any(allowed => reason.Contains(allowed, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
