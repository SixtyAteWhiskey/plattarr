using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Tv.History;
using NzbDrone.Core.Tv.Search;

namespace Radarr.Api.V3.Hygiene
{
    public class HygieneReplacementGrab
    {
        public string DownloadId { get; set; }
        public int CoveredTargetCount { get; set; }
        public int ImportedTargetCount { get; set; }
        public int FailedTargetCount { get; set; }
    }

    public class HygieneReplacementOutcome
    {
        public string SearchState { get; set; }
        public string Evidence { get; set; }
        public int? TargetCount { get; set; }
        public int? ReportsGrabbed { get; set; }
        public List<HygieneReplacementGrab> Grabs { get; set; } = new();
    }

    // Read-only projection. Command completion, a subsequent grab, and import are separate
    // observations; an old command without a durable link is reported as inferred.
    public class HygieneReplacementOutcomeBuilder
    {
        private readonly IHistoryService _tvHistory;
        private readonly IReadOnlyCollection<CommandModel> _commands;
        private readonly Dictionary<int, List<EpisodeHistory>> _episodeHistory = new();

        public HygieneReplacementOutcomeBuilder(IHistoryService tvHistory, IReadOnlyCollection<CommandModel> commands)
        {
            _tvHistory = tvHistory;
            _commands = commands;
        }

        public HygieneReplacementOutcome Build(HygieneAction action)
        {
            var outcome = new HygieneReplacementOutcome
            {
                SearchState = action.Checkpoint == "replacement-skipped" ? "skipped" :
                    action.Checkpoint == "replacement-requested" ? "requested" : "not-requested",
                Evidence = "unverified"
            };

            var targets = ParseTargets(action.ReplacementTargetIds);
            if (action.MediaKind == (int)MediaKind.Tv && targets.Count == 0)
            {
                targets = _tvHistory.Find(action.DownloadId, EpisodeHistoryEventType.DownloadFailed)
                    .Where(row => row.Date >= action.CreatedAt.AddMinutes(-1) &&
                                  row.Date <= action.UpdatedAt.AddMinutes(1))
                    .Select(row => row.ItemId).Where(id => id > 0).Distinct().OrderBy(id => id).ToList();
            }

            if (targets.Count > 0)
            {
                outcome.TargetCount = targets.Count;
            }

            if (action.ReplacementSearchStatus == "skipped" || action.Checkpoint == "replacement-skipped")
            {
                outcome.SearchState = "skipped";
                outcome.Evidence = "recorded";
                return outcome;
            }

            var command = FindCommand(action, targets);
            var linked = action.ReplacementSearchCommandId.HasValue && command?.Id == action.ReplacementSearchCommandId;
            var commandStatus = action.ReplacementSearchStatus ?? command?.Status.ToString().ToLowerInvariant();
            var grabCount = action.ReplacementGrabCount ?? command?.Body?.GrabbedCount;

            if (commandStatus == "completed")
            {
                outcome.SearchState = grabCount == 0 ? "completed-no-grab" : "completed";
            }
            else if (commandStatus is "failed" or "aborted" or "cancelled" or "orphaned")
            {
                outcome.SearchState = "search-failed";
            }

            outcome.Evidence = action.ReplacementSearchStatus != null ? "recorded" :
                linked ? "linked-command" : command != null ? "inferred-command" : "unverified";
            outcome.ReportsGrabbed = grabCount;

            if (action.MediaKind != (int)MediaKind.Tv || targets.Count == 0)
            {
                return outcome;
            }

            var start = command?.StartedAt ?? command?.QueuedAt ?? action.CreatedAt;
            var end = (command?.EndedAt ?? action.ReplacementSearchEndedAt)?.AddSeconds(10);
            var rows = targets.SelectMany(EpisodeRows)
                .Where(row => row.Date >= start && row.ItemId > 0)
                .ToList();
            var grabs = rows.Where(row => row.EventType == EpisodeHistoryEventType.Grabbed &&
                                          (!end.HasValue || row.Date <= end.Value) &&
                                          !string.IsNullOrWhiteSpace(row.DownloadId) &&
                                          row.DownloadId != action.DownloadId)
                .GroupBy(row => row.DownloadId);

            foreach (var group in grabs)
            {
                var covered = group.Select(row => row.ItemId).Distinct().ToHashSet();
                var later = rows.Where(row => row.DownloadId == group.Key && row.Date >= group.Min(grab => grab.Date)).ToList();
                outcome.Grabs.Add(new HygieneReplacementGrab
                {
                    DownloadId = group.Key,
                    CoveredTargetCount = covered.Count,
                    ImportedTargetCount = later.Where(row => row.EventType == EpisodeHistoryEventType.DownloadFolderImported && covered.Contains(row.ItemId))
                        .Select(row => row.ItemId).Distinct().Count(),
                    FailedTargetCount = later.Where(row => row.EventType == EpisodeHistoryEventType.DownloadFailed && covered.Contains(row.ItemId))
                        .Select(row => row.ItemId).Distinct().Count()
                });
            }

            if (outcome.SearchState == "completed" && outcome.ReportsGrabbed == null && outcome.Grabs.Count == 0)
            {
                outcome.SearchState = "completed-no-matching-grab";
            }

            return outcome;
        }

        private CommandModel FindCommand(HygieneAction action, List<int> targets)
        {
            if (action.ReplacementSearchCommandId.HasValue)
            {
                return _commands.FirstOrDefault(command => command.Id == action.ReplacementSearchCommandId.Value);
            }

            // Older journal rows have no command ID. Infer only an exact TV episode-set match
            // in a short window, and only when there is a single candidate.
            if (action.MediaKind != (int)MediaKind.Tv || targets.Count == 0)
            {
                return null;
            }

            var candidates = _commands.Where(command =>
                    command.Body is EpisodeSearchCommand search &&
                    search.EpisodeIds != null &&
                    search.EpisodeIds.OrderBy(id => id).SequenceEqual(targets) &&
                    command.QueuedAt >= action.CreatedAt &&
                    command.QueuedAt <= action.UpdatedAt.AddMinutes(1))
                .Take(2).ToList();

            return candidates.Count == 1 ? candidates[0] : null;
        }

        private static List<int> ParseTargets(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? new List<int>() :
                value.Split(',').Select(part => int.TryParse(part, out var id) ? id : 0)
                    .Where(id => id > 0).Distinct().OrderBy(id => id).ToList();
        }

        private List<EpisodeHistory> EpisodeRows(int id)
        {
            if (!_episodeHistory.TryGetValue(id, out var rows))
            {
                rows = _tvHistory.FindByEpisodeId(id) ?? new List<EpisodeHistory>();
                _episodeHistory[id] = rows;
            }

            return rows;
        }
    }
}
