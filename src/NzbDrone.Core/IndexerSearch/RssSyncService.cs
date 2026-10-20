using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.IndexerSearch
{
    // Fetches RSS once per indexer and routes each release to the media types it belongs
    // to (docs/module-interface.md, contract 2).
    public class RssSyncService : IExecute<RssSyncCommand>
    {
        private readonly IIndexerReleaseFetcher _releaseFetcher;
        private readonly IIndexerCategoryService _indexerCategoryService;
        private readonly List<IReleaseMatcher> _matchers;
        private readonly List<IRssReleaseHandler> _handlers;
        private readonly Logger _logger;

        public RssSyncService(IIndexerReleaseFetcher releaseFetcher,
                              IIndexerCategoryService indexerCategoryService,
                              IEnumerable<IReleaseMatcher> matchers,
                              IEnumerable<IRssReleaseHandler> handlers,
                              Logger logger)
        {
            _releaseFetcher = releaseFetcher;
            _indexerCategoryService = indexerCategoryService;
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

            var releases = await _releaseFetcher.FetchRss(matchers);

            foreach (var matcher in matchers)
            {
                var routed = releases.Where(r => _indexerCategoryService.BelongsTo(r, matcher)).ToList();

                foreach (var handler in _handlers.Where(h => h.Kind == matcher.Kind))
                {
                    await handler.Process(routed);
                }
            }
        }
    }
}
