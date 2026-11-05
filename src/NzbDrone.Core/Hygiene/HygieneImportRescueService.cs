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
using NzbDrone.Core.Movies.History;
using NzbDrone.Core.Movies.MediaFiles.MovieImport.Manual;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Tv.History;
using NzbDrone.Core.Tv.Parser.Model;
using MovieImportMode = NzbDrone.Core.Movies.MediaFiles.MovieImport.ImportMode;
using MovieRejectionReason = NzbDrone.Core.Movies.MediaFiles.MovieImport.ImportRejectionReason;
using TvImportMode = NzbDrone.Core.Tv.MediaFiles.EpisodeImport.ImportMode;
using TvManualImportCommand = NzbDrone.Core.Tv.MediaFiles.EpisodeImport.Manual.TvManualImportCommand;
using TvManualImportFile = NzbDrone.Core.Tv.MediaFiles.EpisodeImport.Manual.ManualImportFile;
using TvManualImportService = NzbDrone.Core.Tv.MediaFiles.EpisodeImport.Manual.IManualImportService;
using TvRejectionReason = NzbDrone.Core.Tv.MediaFiles.EpisodeImport.ImportRejectionReason;
using QueueItem = NzbDrone.Core.Queue.Queue;

namespace NzbDrone.Core.Hygiene
{
    public interface IHygieneImportRescueService
    {
        void Process(IReadOnlyCollection<QueueItem> queue);
        List<HygieneImportRescue> Recent(int limit, int offset);
    }

    public class HygieneImportRescueService : IHygieneImportRescueService
    {
        private sealed class Candidate
        {
            public int[] TargetIds { get; init; }
            public string Fingerprint { get; init; }
            public Func<CommandModel> Push { get; init; }
        }

        private static readonly string[] RescueReasons =
        {
            "matched to series by ID",
            "matched to movie by ID",
            "Unable to determine if file is a sample"
        };

        private readonly IConfigService _config;
        private readonly IHygieneRuleService _rules;
        private readonly IHygieneImportRescueRepository _rescues;
        private readonly IManualImportService _movieImport;
        private readonly TvManualImportService _tvImport;
        private readonly IManageCommandQueue _commands;
        private readonly NzbDrone.Core.Movies.History.IHistoryService _movieHistory;
        private readonly NzbDrone.Core.Tv.History.IHistoryService _tvHistory;
        private readonly IDownloadHistoryService _grabHistory;
        private readonly Logger _logger;

        public HygieneImportRescueService(IConfigService config, IHygieneRuleService rules,
            IHygieneImportRescueRepository rescues, IManualImportService movieImport,
            TvManualImportService tvImport, IManageCommandQueue commands,
            NzbDrone.Core.Movies.History.IHistoryService movieHistory,
            NzbDrone.Core.Tv.History.IHistoryService tvHistory,
            IDownloadHistoryService grabHistory, Logger logger)
        {
            _config = config;
            _rules = rules;
            _rescues = rescues;
            _movieImport = movieImport;
            _tvImport = tvImport;
            _commands = commands;
            _movieHistory = movieHistory;
            _tvHistory = tvHistory;
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
                    item.Protocol == DownloadProtocol.Torrent &&
                    item.RemoteRelease.Release?.IndexerPrivacy != IndexerPrivacy.Public ||
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
                    _logger.Warn("Hygiene import rescue probe failed for client {0}; error type {1}",
                        item.DownloadClientId, ex.GetType().Name);
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
                rescue?.Status == "failed" && rescue.NextAttemptAt > now)
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
                _rescues.SetFields(rescue, x => x.RuleId, x => x.TargetIds,
                    x => x.CandidateFingerprint, x => x.Status, x => x.UpdatedAt);
                item.HygieneImportRescueStatus = "settling";
                return;
            }

            rescue.Status = "prepared";
            rescue.Attempts++;
            rescue.CommandId = null;
            rescue.RequestedAt = null;
            rescue.NextAttemptAt = null;
            rescue.UpdatedAt = now;
            _rescues.SetFields(rescue, x => x.Status, x => x.Attempts, x => x.CommandId,
                x => x.RequestedAt, x => x.NextAttemptAt, x => x.UpdatedAt);
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
                _logger.Warn("Hygiene import rescue request is ambiguous for client {0}; error type {1}",
                    item.DownloadClientId, ex.GetType().Name);
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

            if (rescue.MediaKind == (int)MediaKind.Movies)
            {
                return _movieHistory.FindByDownloadId(rescue.DownloadId)
                    .Any(x => x.EventType == MovieHistoryEventType.DownloadFolderImported &&
                              x.Date > rescue.RequestedAt && targets.Contains(x.ParentId));
            }

            if (rescue.MediaKind == (int)MediaKind.Tv)
            {
                var imported = _tvHistory.FindByDownloadId(rescue.DownloadId)
                    .Where(x => x.EventType == EpisodeHistoryEventType.DownloadFolderImported &&
                                x.Date > rescue.RequestedAt)
                    .Select(x => x.ItemId).ToHashSet();
                return targets.IsSubsetOf(imported);
            }

            return false;
        }

        private bool ImportsAreRunning()
        {
            return _commands.GetStarted().Any(command =>
                command.Name?.Contains("Import", StringComparison.OrdinalIgnoreCase) == true ||
                command.Name?.Contains("Scan", StringComparison.OrdinalIgnoreCase) == true);
        }

        private Candidate Prepare(QueueItem item)
        {
            if (item.RemoteRelease is RemoteMovie movie && movie.Movie?.Id > 0)
            {
                var files = _movieImport.GetMediaFiles(null, item.DownloadId, movie.Movie.Id, false);
                if (files == null || files.Count == 0 || files.Any(file =>
                        string.IsNullOrWhiteSpace(file.Path) || file.Movie?.Id != movie.Movie.Id ||
                        file.Rejections == null || file.Rejections.Any(rejection => rejection.Reason != MovieRejectionReason.SampleIndeterminate)))
                {
                    return null;
                }

                var command = new ManualImportCommand
                {
                    ImportMode = MovieImportMode.Auto,
                    Files = files.Select(file => new ManualImportFile
                    {
                        Path = file.Path,
                        FolderName = file.FolderName,
                        MovieId = movie.Movie.Id,
                        DownloadId = item.DownloadId,
                        Quality = file.Quality,
                        Languages = file.Languages,
                        ReleaseGroup = file.ReleaseGroup,
                        IndexerFlags = file.IndexerFlags
                    }).ToList()
                };
                return new Candidate
                {
                    TargetIds = new[] { movie.Movie.Id },
                    Fingerprint = Fingerprint(files.Select(file => file.Path)),
                    Push = () => _commands.Push(command)
                };
            }

            if (item.RemoteRelease is RemoteEpisode episode && episode.Series?.Id > 0 &&
                episode.Episodes is { Count: > 0 })
            {
                var expected = episode.Episodes.Select(x => x.Id).ToHashSet();
                var files = _tvImport.GetMediaFiles(null, item.DownloadId, episode.Series.Id, false);
                if (files == null || files.Count == 0 || files.Any(file =>
                        string.IsNullOrWhiteSpace(file.Path) || file.Series?.Id != episode.Series.Id ||
                        file.Episodes == null || file.Episodes.Count == 0 ||
                        file.Episodes.Any(x => !expected.Contains(x.Id)) ||
                        file.Rejections == null || file.Rejections.Any(rejection => rejection.Reason != TvRejectionReason.SampleIndeterminate)))
                {
                    return null;
                }

                var targets = files.SelectMany(file => file.Episodes).Select(x => x.Id).Distinct().OrderBy(x => x).ToArray();
                var command = new TvManualImportCommand
                {
                    ImportMode = TvImportMode.Auto,
                    Files = files.Select(file => new TvManualImportFile
                    {
                        Path = file.Path,
                        FolderName = file.FolderName,
                        SeriesId = episode.Series.Id,
                        EpisodeIds = file.Episodes.Select(x => x.Id).ToList(),
                        DownloadId = item.DownloadId,
                        Quality = file.Quality,
                        Languages = file.Languages,
                        ReleaseGroup = file.ReleaseGroup,
                        IndexerFlags = file.IndexerFlags,
                        ReleaseType = file.ReleaseType
                    }).ToList()
                };
                return new Candidate
                {
                    TargetIds = targets,
                    Fingerprint = Fingerprint(files.Select(file => file.Path + "|" +
                        string.Join(",", file.Episodes.Select(x => x.Id).OrderBy(x => x)))),
                    Push = () => _commands.Push(command)
                };
            }

            return null;
        }

        private static string Fingerprint(IEnumerable<string> paths)
        {
            var ordered = paths.OrderBy(path => path, StringComparer.Ordinal);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", ordered))));
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
