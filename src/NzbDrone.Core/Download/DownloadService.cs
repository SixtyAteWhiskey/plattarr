using System;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Common.TPL;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Events;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Download
{
    public interface IDownloadService
    {
        Task DownloadReport(RemoteRelease release, int? downloadClientId);
    }

    public class DownloadService : IDownloadService
    {
        private readonly IProvideDownloadClient _downloadClientProvider;
        private readonly IDownloadClientStatusService _downloadClientStatusService;
        private readonly IIndexerFactory _indexerFactory;
        private readonly IIndexerStatusService _indexerStatusService;
        private readonly IRateLimitService _rateLimitService;
        private readonly IEventAggregator _eventAggregator;
        private readonly ISeedConfigProvider _seedConfigProvider;
        private readonly Logger _logger;

        public DownloadService(IProvideDownloadClient downloadClientProvider,
                               IDownloadClientStatusService downloadClientStatusService,
                               IIndexerFactory indexerFactory,
                               IIndexerStatusService indexerStatusService,
                               IRateLimitService rateLimitService,
                               IEventAggregator eventAggregator,
                               ISeedConfigProvider seedConfigProvider,
                               Logger logger)
        {
            _downloadClientProvider = downloadClientProvider;
            _downloadClientStatusService = downloadClientStatusService;
            _indexerFactory = indexerFactory;
            _indexerStatusService = indexerStatusService;
            _rateLimitService = rateLimitService;
            _eventAggregator = eventAggregator;
            _seedConfigProvider = seedConfigProvider;
            _logger = logger;
        }

        public async Task DownloadReport(RemoteRelease release, int? downloadClientId)
        {
            var filterBlockedClients = release.Release.PendingReleaseReason == PendingReleaseReason.DownloadClientUnavailable;

            var tags = release.Parent?.Tags;

            var downloadClient = downloadClientId.HasValue
                ? _downloadClientProvider.Get(downloadClientId.Value)
                : _downloadClientProvider.GetDownloadClient(release.Release.DownloadProtocol, release.Release.IndexerId, filterBlockedClients, tags);

            await DownloadReport(release, downloadClient);
        }

        private async Task DownloadReport(RemoteRelease release, IDownloadClient downloadClient)
        {
            Ensure.That(release.Parent, () => release.Parent).IsNotNull();

            var downloadTitle = release.Release.Title;

            if (downloadClient == null)
            {
                throw new DownloadClientUnavailableException($"{release.Release.DownloadProtocol} Download client isn't configured yet");
            }

            // Get the seed configuration for this release.
            release.SeedConfiguration = _seedConfigProvider.GetSeedConfiguration(release.Release);

            // Limit grabs to 2 per second.
            if (release.Release.DownloadUrl.IsNotNullOrWhiteSpace() && !release.Release.DownloadUrl.StartsWith("magnet:"))
            {
                var url = new HttpUri(release.Release.DownloadUrl);
                await _rateLimitService.WaitAndPulseAsync(url.Host, TimeSpan.FromSeconds(2));
            }

            IIndexer indexer = null;

            if (release.Release.IndexerId > 0)
            {
                indexer = _indexerFactory.GetInstance(_indexerFactory.Get(release.Release.IndexerId));
            }

            string downloadClientId;
            try
            {
                downloadClientId = await downloadClient.Download(release, indexer);
                _downloadClientStatusService.RecordSuccess(downloadClient.Definition.Id);
                _indexerStatusService.RecordSuccess(release.Release.IndexerId);
            }
            catch (ReleaseUnavailableException)
            {
                _logger.Trace("Release {0} no longer available on indexer.", release);
                PublishIndexerGrab(release, indexer, downloadClient, null, false);
                throw;
            }
            catch (ReleaseBlockedException)
            {
                _logger.Trace("Release {0} previously added to blocklist, not sending to download client again.", release);
                throw;
            }
            catch (DownloadClientRejectedReleaseException)
            {
                _logger.Trace("Release {0} rejected by download client, possible duplicate.", release);
                throw;
            }
            catch (ReleaseDownloadException ex)
            {
                if (ex.InnerException is TooManyRequestsException http429)
                {
                    _indexerStatusService.RecordFailure(release.Release.IndexerId, http429.RetryAfter);
                }
                else
                {
                    _indexerStatusService.RecordFailure(release.Release.IndexerId);
                }

                PublishIndexerGrab(release, indexer, downloadClient, null, false);
                throw;
            }

            PublishIndexerGrab(release, indexer, downloadClient, downloadClientId, true);

            var grabbedEvent = new ReleaseGrabbedEvent(release);
            grabbedEvent.DownloadClient = downloadClient.Name;
            grabbedEvent.DownloadClientId = downloadClient.Definition.Id;
            grabbedEvent.DownloadClientName = downloadClient.Definition.Name;

            if (downloadClientId.IsNotNullOrWhiteSpace())
            {
                grabbedEvent.DownloadId = downloadClientId;
            }

            _logger.ProgressInfo("Report for {0} ({1}) sent to {2} from indexer {3}. {4}", release.Parent.Title, release.Year, downloadClient.Definition.Name, release.Release.Indexer, downloadTitle);
            _eventAggregator.PublishEvent(grabbedEvent);
        }

        // Indexer history and stats count grabs per indexer (Prowlarr's grab event). Only
        // releases from an indexer are recorded; pushed releases without one are not.
        private void PublishIndexerGrab(RemoteRelease release, IIndexer indexer, IDownloadClient downloadClient, string downloadId, bool successful)
        {
            if (indexer == null)
            {
                return;
            }

            var releaseInfo = release.Release;
            var redirect = indexer.SupportsRedirect && indexer.Definition is IndexerDefinition { Redirect: true };

            _eventAggregator.PublishEvent(new IndexerDownloadEvent(releaseInfo, successful, IndexerReleaseFetcher.SearchSource, null, releaseInfo.Title, releaseInfo.DownloadUrl)
            {
                Indexer = indexer,
                Redirect = redirect,
                DownloadClient = downloadClient.Name,
                DownloadClientName = downloadClient.Definition.Name,
                DownloadClientId = downloadClient.Definition.Id,
                DownloadId = downloadId,
                GrabTrigger = release.ReleaseSource == ReleaseSourceType.InteractiveSearch ? GrabTrigger.Manual : GrabTrigger.Api
            });
        }
    }
}
