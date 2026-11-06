using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NzbDrone.Core.Hygiene
{
    public class HygieneContentAssessment
    {
        public int QueueId { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public List<string> BlockedFiles { get; set; } = new();
        public List<string> AllowedFiles { get; set; } = new();
        public bool WouldRemoveDownload { get; set; }
        public bool WouldAct { get; set; }
        public string ActionExplanation { get; set; }
        public string Explanation { get; set; }
    }

    public static class HygieneContentPolicy
    {
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

        public static HygieneContentAssessment Inspect(IEnumerable<string> fileNames, string patterns, string listType, bool deleteIfAnyFileBlocked)
        {
            var files = fileNames?.ToList() ?? new List<string>();
            var matchers = Compile(patterns).ToList();
            var assessment = new HygieneContentAssessment();

            if (files.Count == 0)
            {
                assessment.Status = "files-unavailable";
                assessment.Explanation = "The download client did not provide a file list.";
                return assessment;
            }

            foreach (var file in files)
            {
                var name = file?.Replace('\\', '/').Split('/').LastOrDefault() ?? string.Empty;
                var matched = matchers.Any(matcher => matcher.IsMatch(name));
                var blocked = listType == "allowlist" ? !matched : matched;
                (blocked ? assessment.BlockedFiles : assessment.AllowedFiles).Add(file);
            }

            assessment.WouldRemoveDownload = assessment.BlockedFiles.Count > 0 &&
                                             (deleteIfAnyFileBlocked || assessment.AllowedFiles.Count == 0);
            assessment.Status = assessment.BlockedFiles.Count == 0 ? "allowed" :
                assessment.WouldRemoveDownload ? "remove-download" : "skip-blocked-files";
            assessment.Explanation = assessment.Status switch
            {
                "allowed" => "Every listed file is allowed by the content policy.",
                "remove-download" => "The content policy calls for removal of the whole download.",
                _ => "The content policy calls for skipping only the blocked files."
            };
            return assessment;
        }

        public static void Validate(string patterns, string listType, bool requirePatterns = false)
        {
            if (listType is not ("blocklist" or "allowlist"))
            {
                throw new ArgumentException("Content list type must be blocklist or allowlist.");
            }

            var compiled = Compile(patterns).ToList();
            if (requirePatterns && compiled.Count == 0)
            {
                throw new ArgumentException("At least one content pattern is required.");
            }
        }

        private static IEnumerable<Regex> Compile(string patterns)
        {
            foreach (var raw in (patterns ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var pattern = raw.Trim();
                if (pattern.Length == 0 || pattern.StartsWith('#'))
                {
                    continue;
                }

                if (string.Equals(pattern, "regex:", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("A regular expression cannot be empty.");
                }

                var expression = pattern.StartsWith("regex:", StringComparison.OrdinalIgnoreCase)
                    ? pattern[6..]
                    : "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
                yield return new Regex(expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
            }
        }
    }
}
