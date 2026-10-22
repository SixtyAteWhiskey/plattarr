using NLog;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Download
{
    public interface IIgnoredDownloadService
    {
        bool IgnoreDownload(TrackedDownload trackedDownload);
    }

    // Reads only the neutral release (parent, quality, languages), so it serves every media type
    // with no module contract.
    public class IgnoredDownloadService : IIgnoredDownloadService
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public IgnoredDownloadService(IEventAggregator eventAggregator,
                                      Logger logger)
        {
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public bool IgnoreDownload(TrackedDownload trackedDownload)
        {
            var release = trackedDownload.RemoteRelease;

            if (release?.Parent == null)
            {
                _logger.Warn("Unable to ignore download for unknown {0}", release?.Kind.ToString().ToLowerInvariant() ?? "media");
                return false;
            }

            var downloadIgnoredEvent = new DownloadIgnoredEvent
            {
                Kind = release.Kind,
                ParentId = release.Parent.Id,
                Languages = release.Languages,
                Quality = release.Quality,
                SourceTitle = trackedDownload.DownloadItem.Title,
                DownloadClientInfo = trackedDownload.DownloadItem.DownloadClientInfo,
                DownloadId = trackedDownload.DownloadItem.DownloadId,
                TrackedDownload = trackedDownload,
                Message = "Manually ignored"
            };

            _eventAggregator.PublishEvent(downloadIgnoredEvent);
            return true;
        }
    }
}
