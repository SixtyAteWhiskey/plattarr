using NLog;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Profiles.Delay;

namespace NzbDrone.Core.DecisionEngine.ReleaseSpecifications
{
    public class ProtocolSpecification : IReleaseSpecification
    {
        private readonly IDelayProfileService _delayProfileService;
        private readonly Logger _logger;

        public ProtocolSpecification(IDelayProfileService delayProfileService,
                                     Logger logger)
        {
            _delayProfileService = delayProfileService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual DownloadSpecDecision IsSatisfiedBy(RemoteRelease subject, ReleaseDecisionInformation information)
        {
            var delayProfile = _delayProfileService.BestForTags(subject.Parent.Tags);

            if (subject.Release.DownloadProtocol == DownloadProtocol.Usenet && !delayProfile.EnableUsenet)
            {
                var message = $"Usenet is not enabled for this {ParentNoun(subject.Kind)}";

                _logger.Debug("[{0}] {1}", subject.Release.Title, message);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.ProtocolDisabled, message);
            }

            if (subject.Release.DownloadProtocol == DownloadProtocol.Torrent && !delayProfile.EnableTorrent)
            {
                var message = $"Torrent is not enabled for this {ParentNoun(subject.Kind)}";

                _logger.Debug("[{0}] {1}", subject.Release.Title, message);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.ProtocolDisabled, message);
            }

            return DownloadSpecDecision.Accept();
        }

        // Upstream wording: Radarr says "movie", Sonarr "series", Lidarr "artist".
        private static string ParentNoun(MediaKind kind)
        {
            return kind switch
            {
                MediaKind.Movies => "movie",
                MediaKind.Tv => "series",
                MediaKind.Music => "artist",
                _ => kind.ToString()
            };
        }
    }
}
