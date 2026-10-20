using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Movies.Search
{
    // Radarr's RSS sync after the fetch: decisions, pending releases, and grabs.
    public class MovieRssReleaseHandler : IRssReleaseHandler
    {
        private readonly IMakeDownloadDecision _downloadDecisionMaker;
        private readonly IProcessDownloadDecisions _processDownloadDecisions;
        private readonly IPendingReleaseService _pendingReleaseService;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public MovieRssReleaseHandler(IMakeDownloadDecision downloadDecisionMaker,
                                      IProcessDownloadDecisions processDownloadDecisions,
                                      IPendingReleaseService pendingReleaseService,
                                      IEventAggregator eventAggregator,
                                      Logger logger)
        {
            _downloadDecisionMaker = downloadDecisionMaker;
            _processDownloadDecisions = processDownloadDecisions;
            _pendingReleaseService = pendingReleaseService;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public MediaKind Kind => MediaKind.Movies;

        public async Task Process(IReadOnlyList<ReleaseInfo> releases)
        {
            var pendingReleases = _pendingReleaseService.GetPending();

            var reports = releases.Concat(pendingReleases).ToList();
            var decisions = _downloadDecisionMaker.GetRssDecision(reports);
            var processed = await _processDownloadDecisions.ProcessDecisions(decisions);

            if (processed.Pending.Any())
            {
                _logger.ProgressInfo("RSS Sync Completed. Reports found: {0}, Reports grabbed: {1}, Reports pending: {2}", reports.Count, processed.Grabbed.Count, processed.Pending.Count);
            }
            else
            {
                _logger.ProgressInfo("RSS Sync Completed. Reports found: {0}, Reports grabbed: {1}", reports.Count, processed.Grabbed.Count);
            }

            _eventAggregator.PublishEvent(new RssSyncCompleteEvent(processed));
        }
    }
}
