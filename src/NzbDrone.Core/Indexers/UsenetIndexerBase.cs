using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Indexers
{
    public abstract class UsenetIndexerBase<TSettings> : HttpIndexerBase<TSettings>
        where TSettings : IIndexerSettings, new()
    {
        private readonly IValidateNzbs _nzbValidationService;

        public override DownloadProtocol Protocol => DownloadProtocol.Usenet;

        protected UsenetIndexerBase(IIndexerHttpClient httpClient, IEventAggregator eventAggregator, IIndexerStatusService indexerStatusService, IConfigService configService, IValidateNzbs nzbValidationService, Logger logger)
            : base(httpClient, eventAggregator, indexerStatusService, configService, logger)
        {
            _nzbValidationService = nzbValidationService;
        }

        protected override void ValidateDownloadData(byte[] fileData)
        {
            // Radarr's validator takes a name for its error messages; the indexer name identifies the source.
            _nzbValidationService.Validate(Definition?.Name, fileData);
        }
    }
}
