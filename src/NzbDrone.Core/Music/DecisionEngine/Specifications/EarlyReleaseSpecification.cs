using System.Linq;
using NLog;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers.Overrides;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.Parser.Model;

namespace NzbDrone.Core.Music.DecisionEngine.Specifications
{
    // Lidarr's EarlyReleaseSpecification. The limit is Lidarr's indexer setting, kept here on the
    // Music indexer override (migration 010) since the indexers are the shared ones.
    public class EarlyReleaseSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly IIndexerOverrideService _indexerOverrideService;
        private readonly Logger _logger;

        public EarlyReleaseSpecification(IIndexerOverrideService indexerOverrideService, Logger logger)
        {
            _indexerOverrideService = indexerOverrideService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public DownloadSpecDecision IsSatisfiedBy(RemoteAlbum subject, ReleaseDecisionInformation information)
        {
            var releaseInfo = subject.Release;

            if (releaseInfo == null || releaseInfo.IndexerId == 0)
            {
                return DownloadSpecDecision.Accept();
            }

            var earlyReleaseLimit = _indexerOverrideService.Get(releaseInfo.IndexerId, MediaKind.Music).EarlyReleaseLimit;

            if (subject.Albums.Count != 1 || earlyReleaseLimit == null)
            {
                return DownloadSpecDecision.Accept();
            }

            var releaseDate = subject.Albums.First().ReleaseDate;

            if (releaseDate == null)
            {
                return DownloadSpecDecision.Accept();
            }

            var isEarly = releaseDate.Value > subject.Release.PublishDate.AddDays(earlyReleaseLimit.Value);

            if (isEarly)
            {
                var message = $"Release published date, {subject.Release.PublishDate.ToShortDateString()}, is outside of {earlyReleaseLimit.Value} day early grab limit allowed by user";

                _logger.Debug(message);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.EarlyRelease, message);
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
