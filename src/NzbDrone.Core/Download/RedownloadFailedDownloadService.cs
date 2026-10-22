using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Messaging;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Download
{
    // Applies the user's redownload settings to every media type. What a new search looks like
    // is the module's IRedownloadSearcher.
    public class RedownloadFailedDownloadService : IHandle<DownloadFailedEvent>
    {
        private readonly IConfigService _configService;
        private readonly Dictionary<MediaKind, IRedownloadSearcher> _searchers;
        private readonly Logger _logger;

        public RedownloadFailedDownloadService(IConfigService configService,
                                               IEnumerable<IRedownloadSearcher> searchers,
                                               Logger logger)
        {
            _configService = configService;
            _searchers = searchers.ToDictionary(s => s.Kind);
            _logger = logger;
        }

        [EventHandleOrder(EventHandleOrder.Last)]
        public void Handle(DownloadFailedEvent message)
        {
            if (message.SkipRedownload)
            {
                _logger.Debug("Skip redownloading requested by user");
                return;
            }

            if (!_configService.AutoRedownloadFailed)
            {
                _logger.Debug("Auto redownloading failed downloads is disabled");
                return;
            }

            if (message.ReleaseSource == ReleaseSourceType.InteractiveSearch && !_configService.AutoRedownloadFailedFromInteractiveSearch)
            {
                _logger.Debug("Auto redownloading failed downloads from interactive search is disabled");
                return;
            }

            if (!_searchers.TryGetValue(message.Kind, out var searcher))
            {
                _logger.Debug("No redownload search is registered for {0}", message.Kind);
                return;
            }

            searcher.Search(message);
        }
    }
}
