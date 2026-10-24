using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Music.CustomFormats;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Queue;

namespace NzbDrone.Core.Music.DecisionEngine.Specifications
{
    public class QueueSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly IQueueService _queueService;
        private readonly UpgradableSpecification _upgradableSpecification;
        private readonly IMusicCustomFormatCalculationService _formatService;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public QueueSpecification(IQueueService queueService,
                                  UpgradableSpecification upgradableSpecification,
                                  IMusicCustomFormatCalculationService formatService,
                                  IConfigService configService,
                                  Logger logger)
        {
            _queueService = queueService;
            _upgradableSpecification = upgradableSpecification;
            _formatService = formatService;
            _configService = configService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public DownloadSpecDecision IsSatisfiedBy(RemoteAlbum subject, ReleaseDecisionInformation information)
        {
            var queue = _queueService.GetQueue();
            var matchingAlbum = queue.Where(q => q.GetRemoteAlbum()?.Artist != null &&
                                                 q.GetRemoteAlbum().Artist.Id == subject.Artist.Id &&
                                                 q.GetRemoteAlbum().Albums.Select(e => e.Id).Intersect(subject.Albums.Select(e => e.Id)).Any())
                                     .ToList();

            foreach (var queueItem in matchingAlbum)
            {
                var remoteAlbum = queueItem.GetRemoteAlbum();
                var qualityProfile = subject.Artist.QualityProfile.Value;

                // To avoid a race make sure it's not FailedPending (failed awaiting removal/search).
                // Failed items (already searching for a replacement) won't be part of the queue since
                // it's a copy, of the tracked download, not a reference.
                if (queueItem.TrackedDownloadState == TrackedDownloadState.FailedPending)
                {
                    continue;
                }

                var queuedItemCustomFormats = _formatService.ParseCustomFormat(remoteAlbum, (long)queueItem.Size);

                _logger.Debug("Checking if existing release in queue meets cutoff. Queued quality is: {0}", remoteAlbum.ParsedAlbumInfo.Quality);

                if (!_upgradableSpecification.CutoffNotMet(qualityProfile,
                                                           new List<AudioQualityModel> { remoteAlbum.ParsedAlbumInfo.Quality },
                                                           queuedItemCustomFormats,
                                                           subject.ParsedAlbumInfo.Quality))
                {
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.QueueCutoffMet, "Release in queue already meets cutoff: {0}", remoteAlbum.ParsedAlbumInfo.Quality);
                }

                _logger.Debug("Checking if release is higher quality than queued release. Queued: {0}", remoteAlbum.ParsedAlbumInfo.Quality);

                if (!_upgradableSpecification.IsUpgradable(qualityProfile,
                                                           new List<AudioQualityModel> { remoteAlbum.ParsedAlbumInfo.Quality },
                                                           queuedItemCustomFormats,
                                                           subject.ParsedAlbumInfo.Quality,
                                                           subject.CustomFormats))
                {
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.QueueHigherPreference, "Release in queue is of equal or higher preference: {0}", remoteAlbum.ParsedAlbumInfo.Quality);
                }

                _logger.Debug("Checking if profiles allow upgrading. Queued: {0}", remoteAlbum.ParsedAlbumInfo.Quality);

                if (!_upgradableSpecification.IsUpgradeAllowed(qualityProfile,
                                                               new List<AudioQualityModel> { remoteAlbum.ParsedAlbumInfo.Quality },
                                                               queuedItemCustomFormats,
                                                               subject.ParsedAlbumInfo.Quality,
                                                               subject.CustomFormats))
                {
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.QueueUpgradesNotAllowed, "Another release is queued and the Quality profile does not allow upgrades");
                }

                if (_upgradableSpecification.IsRevisionUpgrade(remoteAlbum.ParsedAlbumInfo.Quality, subject.ParsedAlbumInfo.Quality))
                {
                    if (_configService.DownloadPropersAndRepacks == ProperDownloadTypes.DoNotUpgrade)
                    {
                        _logger.Debug("Auto downloading of propers is disabled");
                        return DownloadSpecDecision.Reject(DownloadRejectionReason.QueuePropersDisabled, "Proper downloading is disabled");
                    }
                }
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
