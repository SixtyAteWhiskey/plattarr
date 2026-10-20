using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.IndexerSearch
{
    // Fetches RSS once per indexer and routes each release to the media types it belongs
    // to (docs/module-interface.md, contract 2).
    public class RssSyncService : IExecute<RssSyncCommand>
    {
        // Prowlarr numbers indexer-specific categories from 100000. They carry no standard
        // meaning, so a release with only those counts as uncategorized.
        private const int CustomCategoryStart = 100000;

        private readonly IIndexerReleaseFetcher _releaseFetcher;
        private readonly List<IReleaseMatcher> _matchers;
        private readonly List<IRssReleaseHandler> _handlers;
        private readonly Logger _logger;

        public RssSyncService(IIndexerReleaseFetcher releaseFetcher,
                              IEnumerable<IReleaseMatcher> matchers,
                              IEnumerable<IRssReleaseHandler> handlers,
                              Logger logger)
        {
            _releaseFetcher = releaseFetcher;
            _matchers = matchers.ToList();
            _handlers = handlers.ToList();
            _logger = logger;
        }

        public void Execute(RssSyncCommand message)
        {
            SyncAsync().GetAwaiter().GetResult();
        }

        private async Task SyncAsync()
        {
            _logger.ProgressInfo("Starting RSS Sync");

            var matchers = _matchers.Where(m => _handlers.Any(h => h.Kind == m.Kind)).ToList();
            var categories = matchers.SelectMany(m => m.Categories).Distinct().ToArray();

            var releases = await _releaseFetcher.FetchRss(categories);

            foreach (var matcher in matchers)
            {
                var routed = Route(releases, matcher);

                foreach (var handler in _handlers.Where(h => h.Kind == matcher.Kind))
                {
                    await handler.Process(routed);
                }
            }
        }

        public static List<ReleaseInfo> Route(IEnumerable<ReleaseInfo> releases, IReleaseMatcher matcher)
        {
            return releases.Where(r => BelongsTo(r, matcher.Categories)).ToList();
        }

        public static bool BelongsTo(ReleaseInfo release, IReadOnlyList<int> topLevelCategories)
        {
            var standard = release.Categories?
                .Select(c => c.Id)
                .Where(id => id < CustomCategoryStart)
                .ToList();

            // No standard category: offer it to every media type, which parses the title.
            if (standard == null || standard.Count == 0)
            {
                return true;
            }

            return standard.Any(id => topLevelCategories.Any(top => id >= top && id < top + 1000));
        }
    }
}
