using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.Parser.Model;

namespace NzbDrone.Core.Music.DecisionEngine.Specifications.RssSync
{
    public class DeletedTrackFileSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly IDiskProvider _diskProvider;
        private readonly IConfigService _configService;
        private readonly IMediaFileService _mediaFileService;
        private readonly Logger _logger;

        public DeletedTrackFileSpecification(IDiskProvider diskProvider,
                                             IConfigService configService,
                                             IMediaFileService mediaFileService,
                                             Logger logger)
        {
            _diskProvider = diskProvider;
            _configService = configService;
            _mediaFileService = mediaFileService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Disk;
        public RejectionType Type => RejectionType.Temporary;

        public virtual DownloadSpecDecision IsSatisfiedBy(RemoteAlbum subject, ReleaseDecisionInformation information)
        {
            if (!_configService.AutoUnmonitorPreviouslyDownloadedTracks)
            {
                return DownloadSpecDecision.Accept();
            }

            if (information.SearchCriteria != null)
            {
                _logger.Debug("Skipping deleted trackfile check during search");
                return DownloadSpecDecision.Accept();
            }

            var missingTrackFiles = subject.Albums
                                           .SelectMany(v => _mediaFileService.GetFilesByAlbum(v.Id))
                                           .DistinctBy(v => v.Id)
                                           .Where(v => !_diskProvider.FileExists(v.Path))
                                           .ToArray();

            if (missingTrackFiles.Any())
            {
                foreach (var missingTrackFile in missingTrackFiles)
                {
                    _logger.Trace("Track file {0} is missing from disk.", missingTrackFile.Path);
                }

                _logger.Debug("Files for this album exist in the database but not on disk, will be unmonitored on next diskscan. skipping.");
                return DownloadSpecDecision.Reject(DownloadRejectionReason.ArtistNotMonitored, "Artist is not monitored");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
