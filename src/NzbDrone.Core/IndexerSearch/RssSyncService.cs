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
        private readonly List<IMediaModule> _modules;
        private readonly List<IRssReleaseHandler> _handlers;
        private readonly Logger _logger;

        public RssSyncService(IIndexerReleaseFetcher releaseFetcher,
                              IIndexerCategoryService indexerCategoryService,
                              IEnumerable<IMediaModule> modules,
                              IEnumerable<IRssReleaseHandler> handlers,
                              Logger logger)
        {
            _releaseFetcher = releaseFetcher;
            _indexerCategoryService = indexerCategoryService;
            _modules = modules.ToList();
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

            var kinds = _modules.Select(m => m.Kind).Where(k => _handlers.Any(h => h.Kind == k)).ToList();

            var releases = await _releaseFetcher.FetchRss(kinds);

            foreach (var kind in kinds)
            {
                var routed = releases.Where(r => _indexerCategoryService.BelongsTo(r, kind)).ToList();

                foreach (var handler in _handlers.Where(h => h.Kind == kind))
                {
                    await handler.Process(routed);
                }
            }
        }
    }
}
