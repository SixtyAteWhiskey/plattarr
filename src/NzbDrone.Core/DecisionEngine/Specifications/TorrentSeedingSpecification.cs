using NLog;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class TorrentSeedingSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly ICachedIndexerSettingsProvider _cachedIndexerSettingsProvider;
        private readonly Logger _logger;

        public TorrentSeedingSpecification(ICachedIndexerSettingsProvider cachedIndexerSettingsProvider, Logger logger)
        {
            _cachedIndexerSettingsProvider = cachedIndexerSettingsProvider;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, ReleaseDecisionInformation information)
        {
            var torrentInfo = subject.Release as TorrentInfo;

            if (torrentInfo == null || torrentInfo.IndexerId == 0)
            {
                return DownloadSpecDecision.Accept();
            }

            var settings = _cachedIndexerSettingsProvider.GetSettings(torrentInfo.IndexerId);

            if (settings == null)
            {
                _logger.Debug("Indexer with id {0} does not exist, skipping seeders check", torrentInfo.IndexerId);
                return DownloadSpecDecision.Accept();
            }

            if (settings.TorrentBaseSettings != null)
            {
                var minimumSeeders = settings.TorrentBaseSettings.MinimumSeeders ?? IndexerDefaults.MINIMUM_SEEDERS;

                if (torrentInfo.Seeders.HasValue && torrentInfo.Seeders.Value < minimumSeeders)
                {
                    _logger.Debug("Not enough seeders: {0}. Minimum seeders: {1}", torrentInfo.Seeders, minimumSeeders);
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.MinimumSeeders, "Not enough seeders: {0}. Minimum seeders: {1}", torrentInfo.Seeders, minimumSeeders);
                }
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
