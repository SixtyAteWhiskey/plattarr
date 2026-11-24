using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Download
{
    public class DownloadProcessingService : IExecute<ProcessMonitoredDownloadsCommand>
    {
        private readonly IConfigService _configService;
        private readonly ICompletedDownloadService _completedDownloadService;
        private readonly IFailedDownloadService _failedDownloadService;
        private readonly ITrackedDownloadService _trackedDownloadService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IHygieneRuleService _hygieneRules;
        private readonly Logger _logger;

        public DownloadProcessingService(IConfigService configService,
                                         ICompletedDownloadService completedDownloadService,
                                         IFailedDownloadService failedDownloadService,
                                         ITrackedDownloadService trackedDownloadService,
                                         IEventAggregator eventAggregator,
                                         IHygieneRuleService hygieneRules,
                                         Logger logger)
        {
            _configService = configService;
            _completedDownloadService = completedDownloadService;
            _failedDownloadService = failedDownloadService;
            _trackedDownloadService = trackedDownloadService;
            _eventAggregator = eventAggregator;
            _hygieneRules = hygieneRules;
            _logger = logger;
        }

        private void RemoveCompletedDownloads()
        {
            var trackedDownloads = _trackedDownloadService.GetTrackedDownloads()
                                                          .Where(t => !t.DownloadItem.Removed && t.DownloadItem.CanBeRemoved && t.State == TrackedDownloadState.Imported)
                                                          .ToList();

            foreach (var trackedDownload in trackedDownloads)
            {
                _eventAggregator.PublishEvent(new DownloadCanBeRemovedEvent(trackedDownload));
            }
        }

        public void Execute(ProcessMonitoredDownloadsCommand message)
        {
            var enableCompletedDownloadHandling = _configService.EnableCompletedDownloadHandling;
            var hygieneRules = _configService.HygieneActionsEnabled ? _hygieneRules.All() : new List<HygieneRule>();
            var trackedDownloads = _trackedDownloadService.GetTrackedDownloads()
                                              .Where(t => t.IsTrackable)
                                              .ToList();

            foreach (var trackedDownload in trackedDownloads)
            {
                try
                {
                    // Process completed items followed by failed, this allows failed imports to have
                    // their state changed and be processed immediately instead of the next execution.

                    if (enableCompletedDownloadHandling && trackedDownload.State == TrackedDownloadState.ImportPending)
                    {
                        _completedDownloadService.Import(trackedDownload);
                    }

                    if (trackedDownload.State == TrackedDownloadState.FailedPending &&
                        !ShouldDeferFailure(trackedDownload, hygieneRules))
                    {
                        _failedDownloadService.ProcessFailed(trackedDownload);
                    }
                }
                catch (Exception e)
                {
                    _logger.Debug(e, "Failed to process download: {0}", trackedDownload.DownloadItem.Title);
                }
            }

            // Imported downloads are no longer trackable so process them after processing trackable downloads
            RemoveCompletedDownloads();

            _eventAggregator.PublishEvent(new DownloadsProcessedEvent());
        }

        private bool ShouldDeferFailure(TrackedDownload trackedDownload, IEnumerable<HygieneRule> rules)
        {
            if (trackedDownload.DownloadItem.DownloadClientNetworkUnavailable)
            {
                return true;
            }

            // A matching active Hygiene rule owns the failure threshold and its
            // safeguards. Native failure processing must not remove the job first.
            var item = new NzbDrone.Core.Queue.Queue
            {
                DownloadClientId = trackedDownload.DownloadClient,
                Protocol = trackedDownload.Protocol,
                RemoteRelease = trackedDownload.RemoteRelease
            };

            return HygieneRuleSafeguards.MatchForItem(
                rules,
                _hygieneRules,
                "client-failed",
                (int?)trackedDownload.Kind ?? 0,
                item)?.Enabled == true;
        }
    }
}
