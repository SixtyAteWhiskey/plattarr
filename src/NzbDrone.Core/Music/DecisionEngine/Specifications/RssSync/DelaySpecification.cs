using System.Linq;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Pending;
using NzbDrone.Core.Music.Qualities;
using NzbDrone.Core.Profiles.Delay;

namespace NzbDrone.Core.Music.DecisionEngine.Specifications.RssSync
{
    public class DelaySpecification : IDownloadDecisionEngineSpecification
    {
        private readonly IPendingReleaseService _pendingReleaseService;
        private readonly IDelayProfileService _delayProfileService;
        private readonly IMediaFileService _mediaFileService;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public DelaySpecification(IPendingReleaseService pendingReleaseService,
                                  IDelayProfileService delayProfileService,
                                  IMediaFileService mediaFileService,
                                  IConfigService configService,
                                  Logger logger)
        {
            _pendingReleaseService = pendingReleaseService;
            _delayProfileService = delayProfileService;
            _mediaFileService = mediaFileService;
            _configService = configService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Database;
        public RejectionType Type => RejectionType.Temporary;

        public virtual DownloadSpecDecision IsSatisfiedBy(RemoteAlbum subject, ReleaseDecisionInformation information)
        {
            if (information.SearchCriteria is { UserInvokedSearch: true })
            {
                _logger.Debug("Ignoring delay for user invoked search");
                return DownloadSpecDecision.Accept();
            }

            var qualityProfile = subject.Artist.QualityProfile.Value;
            var delayProfile = _delayProfileService.BestForTags(subject.Artist.Tags);
            var delay = delayProfile.GetProtocolDelay(subject.Release.DownloadProtocol);
            var isPreferredProtocol = subject.Release.DownloadProtocol == delayProfile.PreferredProtocol;

            if (delay == 0)
            {
                _logger.Debug("Profile does not require a waiting period before download for {0}.", subject.Release.DownloadProtocol);
                return DownloadSpecDecision.Accept();
            }

            var qualityComparer = new AudioQualityModelComparer(qualityProfile);

            if (isPreferredProtocol)
            {
                foreach (var album in subject.Albums)
                {
                    var trackFiles = _mediaFileService.GetFilesByAlbum(album.Id);

                    foreach (var file in trackFiles)
                    {
                        var currentQuality = file.Quality;
                        var newQuality = subject.ParsedAlbumInfo.Quality;
                        var qualityCompare = qualityComparer.Compare(newQuality?.Quality, currentQuality.Quality);

                        if (qualityCompare == 0 && newQuality?.Revision.CompareTo(currentQuality.Revision) > 0)
                        {
                            _logger.Debug("New quality is a better revision for existing quality, skipping delay");
                            return DownloadSpecDecision.Accept();
                        }
                    }
                }
            }

            // If quality meets or exceeds the best allowed quality in the profile accept it immediately
            if (delayProfile.BypassIfHighestQuality)
            {
                var bestQualityInProfile = qualityProfile.LastAllowedQuality();
                var isBestInProfile = qualityComparer.Compare(subject.ParsedAlbumInfo.Quality.Quality, bestQualityInProfile) >= 0;

                if (isBestInProfile && isPreferredProtocol)
                {
                    _logger.Debug("Quality is highest in profile for preferred protocol, will not delay");
                    return DownloadSpecDecision.Accept();
                }
            }

            // If the custom format score meets the delay profile's minimum accept it immediately
            if (delayProfile.BypassIfAboveCustomFormatScore)
            {
                var score = subject.CustomFormatScore;
                var minimum = delayProfile.MinimumCustomFormatScore;

                if (score >= minimum && isPreferredProtocol)
                {
                    _logger.Debug("Custom format score ({0}) meets minimum ({1}) for preferred protocol, will not delay", score, minimum);
                    return DownloadSpecDecision.Accept();
                }
            }

            var albumIds = subject.Albums.Select(e => e.Id);

            var oldest = _pendingReleaseService.OldestPendingRelease(subject.Artist.Id, albumIds.ToArray());

            if (oldest != null && oldest.Release.AgeMinutes > delay)
            {
                return DownloadSpecDecision.Accept();
            }

            if (subject.Release.AgeMinutes < delay)
            {
                _logger.Debug("Waiting for better quality release, There is a {0} minute delay on {1}", delay, subject.Release.DownloadProtocol);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.MinimumAgeDelay, "Waiting for better quality release");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
