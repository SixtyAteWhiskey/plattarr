using System;
using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Upstream
{
    // The options file's pathMap: rewrites the start of a path, longest matching
    // prefix first. A prefix matches whole path segments only, so /movies does
    // not touch /movies2. Matching is case-sensitive (the upstream containers
    // are Linux); separators in the rest of the path are rewritten to the style
    // of the target prefix, so a Windows source maps onto a Linux target.
    public class PathMapper
    {
        private readonly List<(string Prefix, string Target)> _rules;

        public PathMapper(IDictionary<string, string> map)
        {
            var rules = new List<(string Prefix, string Target)>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var pair in map ?? new Dictionary<string, string>())
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
                {
                    continue;
                }

                var prefix = TrimSeparators(pair.Key);

                if (!seen.Add(prefix))
                {
                    throw new UpstreamImportException("pathMap lists the same source path twice (a trailing slash does not make it different)");
                }

                rules.Add((prefix, TrimSeparators(pair.Value)));
            }

            _rules = rules.OrderByDescending(r => r.Prefix.Length).ToList();
        }

        public string Map(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            foreach (var (prefix, target) in _rules)
            {
                if (!path.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (path.Length == prefix.Length)
                {
                    return target;
                }

                var next = path[prefix.Length];

                if (next != '/' && next != '\\')
                {
                    continue;
                }

                return target + Restyle(path.Substring(prefix.Length), target);
            }

            return path;
        }

        private static string TrimSeparators(string path)
        {
            return path.TrimEnd('/', '\\');
        }

        private static string Restyle(string rest, string target)
        {
            var hasForward = target.Contains('/');
            var hasBack = target.Contains('\\');

            if (hasBack && !hasForward)
            {
                return rest.Replace('/', '\\');
            }

            if (hasForward && !hasBack)
            {
                return rest.Replace('\\', '/');
            }

            return rest;
        }
    }
}
