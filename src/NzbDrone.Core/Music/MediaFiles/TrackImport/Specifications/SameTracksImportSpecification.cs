using NLog;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Music.DecisionEngine.Specifications;
using NzbDrone.Core.Music.Parser.Model;

namespace NzbDrone.Core.Music.MediaFiles.TrackImport.Specifications
{
    public class SameTracksImportSpecification : IImportDecisionEngineSpecification<LocalTrack>
    {
        private readonly SameTracksSpecification _sameTracksSpecification;
        private readonly Logger _logger;

        public SameTracksImportSpecification(SameTracksSpecification sameTracksSpecification, Logger logger)
        {
            _sameTracksSpecification = sameTracksSpecification;
            _logger = logger;
        }

        public RejectionType Type => RejectionType.Permanent;

        public ImportSpecDecision IsSatisfiedBy(LocalTrack item, DownloadClientItem downloadClientItem)
        {
            if (_sameTracksSpecification.IsSatisfiedBy(item.Tracks))
            {
                return ImportSpecDecision.Accept();
            }

            _logger.Debug("Track file on disk contains more tracks than this file contains");
            return ImportSpecDecision.Reject(ImportRejectionReason.ExistingFileHasMoreTracks, "Track file on disk contains more tracks than this file contains");
        }
    }
}
