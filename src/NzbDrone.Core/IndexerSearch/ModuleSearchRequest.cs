using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.IndexerSearch
{
    public static class InteractiveSearchExecution
    {
        private static readonly AsyncLocal<Options> Current = new();

        public static bool Refresh => Current.Value?.Refresh == true;
        public static bool Preview => Current.Value?.Preview == true;
        public static string SearchId => Current.Value?.SearchId;

        public static IDisposable Begin(bool refresh, bool preview, string searchId)
        {
            var previous = Current.Value;
            Current.Value = new Options { Refresh = refresh, Preview = preview, SearchId = searchId };
            return new ExecutionScope(() => Current.Value = previous);
        }

        private sealed class Options
        {
            public bool Refresh { get; init; }
            public bool Preview { get; init; }
            public string SearchId { get; init; }
        }

        private sealed class ExecutionScope : IDisposable
        {
            private readonly Action _restore;

            public ExecutionScope(Action restore)
            {
                _restore = restore;
            }

            public void Dispose()
            {
                _restore();
            }
        }
    }

    // Base for a media module's search request (docs/module-interface.md, contract 3).
    // Module requests are translated into the indexer layer's SearchCriteriaBase types.
    public abstract class ModuleSearchRequest
    {
        private static readonly Regex SpecialCharacter = new Regex(@"['.`´‘’]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex NonWord = new Regex(@"[\W]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex BeginningThe = new Regex(@"^the\s", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public List<string> SceneTitles { get; set; }
        public virtual bool MonitoredEpisodesOnly { get; set; }
        public virtual bool UserInvokedSearch { get; set; }
        public virtual bool InteractiveSearch { get; set; }

        // Interactive refresh bypasses the short-lived provider-result cache.
        public bool RefreshSearch { get; set; }

        public List<string> CleanSceneTitles => SceneTitles.Select(GetCleanSceneTitle).Distinct().ToList();

        public static string GetCleanSceneTitle(string title)
        {
            Ensure.That(title, () => title).IsNotNullOrWhiteSpace();

            var cleanTitle = BeginningThe.Replace(title, string.Empty);

            cleanTitle = cleanTitle.Replace("&", "and");
            cleanTitle = SpecialCharacter.Replace(cleanTitle, "");
            cleanTitle = NonWord.Replace(cleanTitle, "+");

            // remove any repeating +s
            cleanTitle = Regex.Replace(cleanTitle, @"\+{2,}", "+");
            cleanTitle = cleanTitle.RemoveAccent();
            return cleanTitle.Trim('+', ' ');
        }
    }
}
