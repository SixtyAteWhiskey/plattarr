using System;
using System.Linq;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Music.CustomFormats;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.Parser.Model;

namespace NzbDrone.Core.Music.DecisionEngine.Specifications
{
    public class UpgradeAllowedSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly UpgradableSpecification _upgradableSpecification;
        private readonly IMediaFileService _mediaFileService;
        private readonly ITrackService _trackService;
        private readonly IMusicCustomFormatCalculationService _formatService;
        private readonly Logger _logger;
        private readonly ICached<bool> _missingFilesCache;

        public UpgradeAllowedSpecification(UpgradableSpecification upgradableSpecification,
                                           Logger logger,
                                           ICacheManager cacheManager,
                                           IMediaFileService mediaFileService,
                                           ITrackService trackService,
                                           IMusicCustomFormatCalculationService formatService)
        {
            _upgradableSpecification = upgradableSpecification;
            _mediaFileService = mediaFileService;
            _trackService = trackService;
            _formatService = formatService;
            _missingFilesCache = cacheManager.GetCache<bool>(GetType());
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual DownloadSpecDecision IsSatisfiedBy(RemoteAlbum subject, ReleaseDecisionInformation information)
        {
            var qualityProfile = subject.Artist.QualityProfile.Value;

            foreach (var album in subject.Albums)
            {
                var tracksMissing = _missingFilesCache.Get(album.Id.ToString(),
                                                           () => _trackService.TracksWithoutFiles(album.Id).Any(),
                                                           TimeSpan.FromSeconds(30));

                var trackFiles = _mediaFileService.GetFilesByAlbum(album.Id);

                if (!tracksMissing && trackFiles.Any())
                {
                    // Get a distinct list of all current track qualities for a given album
                    var currentQualities = trackFiles.Select(c => c.Quality).Distinct().ToList();

                    // Lidarr takes the custom formats of the first file for the whole album.
                    var fileCustomFormats = _formatService.ParseCustomFormat(trackFiles.First(), subject.Artist);

                    _logger.Debug("Comparing file quality with report. Existing files contain {0}", currentQualities.ConcatToString());

                    if (!_upgradableSpecification.IsUpgradeAllowed(qualityProfile,
                                                                   currentQualities,
                                                                   fileCustomFormats,
                                                                   subject.ParsedAlbumInfo.Quality,
                                                                   subject.CustomFormats))
                    {
                        _logger.Debug("Upgrading is not allowed by the quality profile");

                        return DownloadSpecDecision.Reject(DownloadRejectionReason.QualityUpgradesDisabled, "Existing files and the Quality profile does not allow upgrades");
                    }
                }
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
