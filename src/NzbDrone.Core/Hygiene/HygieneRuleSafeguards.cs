using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Indexers;
using QueueItem = NzbDrone.Core.Queue.Queue;

namespace NzbDrone.Core.Hygiene
{
    public static class HygieneRuleSafeguards
    {
        public static HygieneRule MatchForItem(IEnumerable<HygieneRule> rules, IHygieneRuleService ruleService, string reasonCode, int mediaKind, QueueItem item)
        {
            var privacy = item.Protocol == DownloadProtocol.Torrent ? item.RemoteRelease?.Release?.IndexerPrivacy : null;
            var scope = privacy switch
            {
                IndexerPrivacy.Public => "public",
                IndexerPrivacy.Private or IndexerPrivacy.SemiPrivate => "private",
                _ => null
            };

            // Match keeps its media/client specificity order. LINQ's stable sort
            // lets an exact privacy scope win when those scopes are equal.
            var applicable = rules.Where(rule => rule.TorrentPrivacyScope is null or "all" ||
                                                 (scope != null && rule.TorrentPrivacyScope == scope))
                .OrderByDescending(rule => scope != null && rule.TorrentPrivacyScope == scope);
            return ruleService.Match(applicable, reasonCode, mediaKind, item.DownloadClientId);
        }

        public static string HoldReason(QueueItem item, HygieneRule rule, string reasonCode)
        {
            if (rule == null)
            {
                return null;
            }

            if (Ignored(item, rule.IgnoredDownloads))
            {
                return "This download matches an ignore value on the rule.";
            }

            if (item.Protocol == DownloadProtocol.Torrent && rule.TorrentPrivacyScope != "all")
            {
                var privacy = item.RemoteRelease?.Release?.IndexerPrivacy;
                if (privacy == null)
                {
                    return "Torrent privacy is unverified for this rule's privacy scope.";
                }

                var isPrivate = privacy is IndexerPrivacy.Private or IndexerPrivacy.SemiPrivate;
                if ((rule.TorrentPrivacyScope == "private") != isPrivate)
                {
                    return "Torrent privacy is outside this rule's scope.";
                }
            }

            if (reasonCode == "import-blocked" && !FailedImportMatches(item.StatusMessages, rule))
            {
                return "The failed-import messages do not pass this rule's reason filter.";
            }

            if (rule.IgnoreWhileAlternateSpeedActive && item.DownloadClientAlternateSpeedActive &&
                (reasonCode == "download-slow" || (reasonCode == "download-unstable" && !item.DownloadClientStalled)))
            {
                return "The download client is using alternate speed limits or their state could not be checked.";
            }

            if (reasonCode is ("download-stalled" or "download-slow" or "download-unstable") &&
                (rule.MinimumCompletionPercent > 0 || rule.MaximumCompletionPercent < 100 ||
                 rule.MinimumTorrentSizeBytes > 0 || rule.MaximumTorrentSizeBytes > 0))
            {
                if (item.Size <= 0 || item.SizeLeft < 0 || item.SizeLeft > item.Size)
                {
                    return "Torrent size or completion is unavailable for the rule safeguards.";
                }

                var completion = 100 * (1 - (item.SizeLeft / item.Size));
                if (completion < rule.MinimumCompletionPercent || completion > rule.MaximumCompletionPercent)
                {
                    return "Torrent completion is outside this rule's permitted range.";
                }

                if (item.Size < rule.MinimumTorrentSizeBytes)
                {
                    return "Torrent size is below this rule's minimum.";
                }

                if (rule.MaximumTorrentSizeBytes > 0 && item.Size > rule.MaximumTorrentSizeBytes)
                {
                    return "Torrent size exceeds this rule's maximum.";
                }
            }

            return null;
        }

        public static bool Ignored(QueueItem item, string values)
        {
            foreach (var value in Lines(values))
            {
                if (Same(value, item.DownloadId) || Same(value, item.DownloadCategory) || Same(value, item.DownloadClient) ||
                    (item.DownloadTags ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(tag => Same(value, tag)) ||
                    TrackerMatches(value, item.DownloadTracker))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool FailedImportMatches(IEnumerable<TrackedDownloadStatusMessage> messages, HygieneRule rule)
        {
            var mode = rule.FailedImportPatternMode ?? "all";
            if (mode == "all")
            {
                return true;
            }

            var patterns = Lines(rule.FailedImportPatterns).ToList();
            var matched = (messages ?? Enumerable.Empty<TrackedDownloadStatusMessage>())
                .SelectMany(x => x.Messages ?? new List<string>())
                .Any(message => patterns.Any(pattern => message?.Contains(pattern, StringComparison.OrdinalIgnoreCase) == true));
            return mode == "include" ? matched : !matched;
        }

        private static bool TrackerMatches(string value, string tracker)
        {
            if (!Uri.TryCreate(tracker, UriKind.Absolute, out var uri))
            {
                return false;
            }

            return Same(value, uri.Host) || uri.Host.EndsWith("." + value, StringComparison.OrdinalIgnoreCase);
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> Lines(string value)
        {
            return (value ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim()).Where(x => x.Length > 0);
        }
    }
}
