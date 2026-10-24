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
    public class UpgradeDiskSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly IMediaFileService _mediaFileService;
        private readonly ITrackService _trackService;
        private readonly UpgradableSpecification _upgradableSpecification;
        private readonly IMusicCustomFormatCalculationService _formatService;
        private readonly Logger _logger;
        private readonly ICached<bool> _missingFilesCache;

        public UpgradeDiskSpecification(UpgradableSpecification qualityUpgradableSpecification,
                                        IMediaFileService mediaFileService,
                                        ITrackService trackService,
                                        ICacheManager cacheManager,
                                        IMusicCustomFormatCalculationService formatService,
                                        Logger logger)
        {
            _upgradableSpecification = qualityUpgradableSpecification;
            _mediaFileService = mediaFileService;
            _trackService = trackService;
            _formatService = formatService;
            _logger = logger;
            _missingFilesCache = cacheManager.GetCache<bool>(GetType());
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual DownloadSpecDecision IsSatisfiedBy(RemoteAlbum subject, ReleaseDecisionInformation information)
        {
            foreach (var album in subject.Albums)
            {
                var tracksMissing = _missingFilesCache.Get(album.Id.ToString(),
                                                           () => _trackService.TracksWithoutFiles(album.Id).Any(),
                                                           TimeSpan.FromSeconds(30));

                var trackFiles = _mediaFileService.GetFilesByAlbum(album.Id);

                if (!tracksMissing && trackFiles.Any())
                {
                    var currentQualities = trackFiles.Select(c => c.Quality).Distinct().ToList();

                    var customFormats = _formatService.ParseCustomFormat(trackFiles[0]);

                    if (!_upgradableSpecification.IsUpgradable(subject.Artist.QualityProfile,
                                                               currentQualities,
                                                               customFormats,
                                                               subject.ParsedAlbumInfo.Quality,
                                                               subject.CustomFormats))
                    {
                        return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskHigherPreference, "Existing files on disk is of equal or higher preference: {0}", currentQualities.ConcatToString());
                    }
                }
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
