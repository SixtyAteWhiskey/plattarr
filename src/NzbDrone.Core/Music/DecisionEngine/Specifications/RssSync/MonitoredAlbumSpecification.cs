using System.Linq;
using NLog;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Music.Parser.Model;

namespace NzbDrone.Core.Music.DecisionEngine.Specifications.RssSync
{
    public class MonitoredAlbumSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public MonitoredAlbumSpecification(Logger logger)
        {
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual DownloadSpecDecision IsSatisfiedBy(RemoteAlbum subject, ReleaseDecisionInformation information)
        {
            if (information.SearchCriteria != null)
            {
                if (!information.SearchCriteria.MonitoredEpisodesOnly)
                {
                    _logger.Debug("Skipping monitored check during search");
                    return DownloadSpecDecision.Accept();
                }
            }

            if (!subject.Artist.Monitored)
            {
                _logger.Debug("{0} is present in the DB but not tracked. Rejecting.", subject.Artist);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.ArtistNotMonitored, "Artist is not monitored");
            }

            var monitoredCount = subject.Albums.Count(album => album.Monitored);

            if (monitoredCount == subject.Albums.Count)
            {
                return DownloadSpecDecision.Accept();
            }

            if (subject.Albums.Count == 1)
            {
                _logger.Debug("Album is not monitored. Rejecting");
                return DownloadSpecDecision.Reject(DownloadRejectionReason.AlbumNotMonitored, "Album is not monitored");
            }

            if (monitoredCount == 0)
            {
                _logger.Debug("No albums in the release are monitored. Rejecting");
            }
            else
            {
                _logger.Debug("Only {0}/{1} albums in the release are monitored. Rejecting", monitoredCount, subject.Albums.Count);
            }

            return DownloadSpecDecision.Reject(DownloadRejectionReason.AlbumNotMonitored, "Album is not monitored");
        }
    }
}
