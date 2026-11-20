using System;
using NLog;
using NzbDrone.Core.Download;
using NzbDrone.Core.Localization;
using NzbDrone.Core.ThingiProvider.Events;

namespace NzbDrone.Core.HealthCheck.Checks
{
    [CheckOn(typeof(ProviderAddedEvent<IDownloadClient>))]
    [CheckOn(typeof(ProviderUpdatedEvent<IDownloadClient>))]
    [CheckOn(typeof(ProviderDeletedEvent<IDownloadClient>))]
    public class DownloadClientConnectionCheck : HealthCheckBase
    {
        private readonly IProvideDownloadClient _downloadClientProvider;
        private readonly Logger _logger;

        public DownloadClientConnectionCheck(IProvideDownloadClient downloadClientProvider,
                                             ILocalizationService localizationService,
                                             Logger logger)
            : base(localizationService)
        {
            _downloadClientProvider = downloadClientProvider;
            _logger = logger;
        }

        public override HealthCheck Check()
        {
            var statusUnavailable = false;

            foreach (var client in _downloadClientProvider.GetDownloadClients())
            {
                if (client is not IDownloadClientConnectionStatus connectionStatus)
                {
                    continue;
                }

                try
                {
                    if (connectionStatus.GetConnectionStatus() == "disconnected")
                    {
                        return new HealthCheck(GetType(),
                            HealthCheckResult.Warning,
                            HealthCheckReason.DownloadClientConnectionDisconnected,
                            _localizationService.GetLocalizedString("DownloadClientConnectionDisconnectedMessage"));
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Unable to read connection status from a download client");
                    statusUnavailable = true;
                }
            }

            if (statusUnavailable)
            {
                return new HealthCheck(GetType(),
                    HealthCheckResult.Warning,
                    HealthCheckReason.DownloadClientConnectionStatusUnavailable,
                    _localizationService.GetLocalizedString("DownloadClientConnectionStatusUnavailableMessage"));
            }

            return new HealthCheck(GetType());
        }
    }
}
