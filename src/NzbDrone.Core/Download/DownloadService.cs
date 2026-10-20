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
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Download
{
    public interface IDownloadService
    {
        Task DownloadReport(RemoteMovie remoteMovie, int? downloadClientId);
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

        public async Task DownloadReport(RemoteMovie remoteMovie, int? downloadClientId)
        {
            var filterBlockedClients = remoteMovie.Release.PendingReleaseReason == PendingReleaseReason.DownloadClientUnavailable;

            var tags = remoteMovie.Movie?.Tags;

            var downloadClient = downloadClientId.HasValue
                ? _downloadClientProvider.Get(downloadClientId.Value)
                : _downloadClientProvider.GetDownloadClient(remoteMovie.Release.DownloadProtocol, remoteMovie.Release.IndexerId, filterBlockedClients, tags);

            await DownloadReport(remoteMovie, downloadClient);
        }

        private async Task DownloadReport(RemoteMovie remoteMovie, IDownloadClient downloadClient)
        {
            Ensure.That(remoteMovie.Movie, () => remoteMovie.Movie).IsNotNull();

            var downloadTitle = remoteMovie.Release.Title;

            if (downloadClient == null)
            {
                throw new DownloadClientUnavailableException($"{remoteMovie.Release.DownloadProtocol} Download client isn't configured yet");
            }

            // Get the seed configuration for this release.
            remoteMovie.SeedConfiguration = _seedConfigProvider.GetSeedConfiguration(remoteMovie.Release);

            // Limit grabs to 2 per second.
            if (remoteMovie.Release.DownloadUrl.IsNotNullOrWhiteSpace() && !remoteMovie.Release.DownloadUrl.StartsWith("magnet:"))
            {
                var url = new HttpUri(remoteMovie.Release.DownloadUrl);
                await _rateLimitService.WaitAndPulseAsync(url.Host, TimeSpan.FromSeconds(2));
            }

            IIndexer indexer = null;

            if (remoteMovie.Release.IndexerId > 0)
            {
                indexer = _indexerFactory.GetInstance(_indexerFactory.Get(remoteMovie.Release.IndexerId));
            }

            string downloadClientId;
            try
            {
                downloadClientId = await downloadClient.Download(remoteMovie, indexer);
                _downloadClientStatusService.RecordSuccess(downloadClient.Definition.Id);
                _indexerStatusService.RecordSuccess(remoteMovie.Release.IndexerId);
            }
            catch (ReleaseUnavailableException)
            {
                _logger.Trace("Release {0} no longer available on indexer.", remoteMovie);
                PublishIndexerGrab(remoteMovie, indexer, downloadClient, null, false);
                throw;
            }
            catch (ReleaseBlockedException)
            {
                _logger.Trace("Release {0} previously added to blocklist, not sending to download client again.", remoteMovie);
                throw;
            }
            catch (DownloadClientRejectedReleaseException)
            {
                _logger.Trace("Release {0} rejected by download client, possible duplicate.", remoteMovie);
                throw;
            }
            catch (ReleaseDownloadException ex)
            {
                if (ex.InnerException is TooManyRequestsException http429)
                {
                    _indexerStatusService.RecordFailure(remoteMovie.Release.IndexerId, http429.RetryAfter);
                }
                else
                {
                    _indexerStatusService.RecordFailure(remoteMovie.Release.IndexerId);
                }

                PublishIndexerGrab(remoteMovie, indexer, downloadClient, null, false);
                throw;
            }

            PublishIndexerGrab(remoteMovie, indexer, downloadClient, downloadClientId, true);

            var movieGrabbedEvent = new MovieGrabbedEvent(remoteMovie);
            movieGrabbedEvent.DownloadClient = downloadClient.Name;
            movieGrabbedEvent.DownloadClientId = downloadClient.Definition.Id;
            movieGrabbedEvent.DownloadClientName = downloadClient.Definition.Name;

            if (downloadClientId.IsNotNullOrWhiteSpace())
            {
                movieGrabbedEvent.DownloadId = downloadClientId;
            }

            _logger.ProgressInfo("Report for {0} ({1}) sent to {2} from indexer {3}. {4}", remoteMovie.Movie.Title, remoteMovie.Movie.Year, downloadClient.Definition.Name, remoteMovie.Release.Indexer, downloadTitle);
            _eventAggregator.PublishEvent(movieGrabbedEvent);
        }

        // Indexer history and stats count grabs per indexer (Prowlarr's grab event). Only
        // releases from an indexer are recorded; pushed releases without one are not.
        private void PublishIndexerGrab(RemoteMovie remoteMovie, IIndexer indexer, IDownloadClient downloadClient, string downloadId, bool successful)
        {
            if (indexer == null)
            {
                return;
            }

            var release = remoteMovie.Release;
            var redirect = indexer.SupportsRedirect && indexer.Definition is IndexerDefinition { Redirect: true };

            _eventAggregator.PublishEvent(new IndexerDownloadEvent(release, successful, IndexerReleaseFetcher.SearchSource, null, release.Title, release.DownloadUrl)
            {
                Indexer = indexer,
                Redirect = redirect,
                DownloadClient = downloadClient.Name,
                DownloadClientName = downloadClient.Definition.Name,
                DownloadClientId = downloadClient.Definition.Id,
                DownloadId = downloadId,
                GrabTrigger = remoteMovie.ReleaseSource == ReleaseSourceType.InteractiveSearch ? GrabTrigger.Manual : GrabTrigger.Api
            });
        }
    }
}
