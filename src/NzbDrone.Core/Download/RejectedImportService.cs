using NLog;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Download;

public interface IRejectedImportService
{
    bool Process(TrackedDownload trackedDownload, IImportResult importResult);
}

public class RejectedImportService : IRejectedImportService
{
    private readonly ICachedIndexerSettingsProvider _cachedIndexerSettingsProvider;
    private readonly Logger _logger;

    public RejectedImportService(ICachedIndexerSettingsProvider cachedIndexerSettingsProvider, Logger logger)
    {
        _cachedIndexerSettingsProvider = cachedIndexerSettingsProvider;
        _logger = logger;
    }

    public bool Process(TrackedDownload trackedDownload, IImportResult importResult)
    {
        if (importResult.Result != ImportResultType.Rejected || trackedDownload.RemoteRelease?.Release == null)
        {
            return false;
        }

        var indexerSettings = _cachedIndexerSettingsProvider.GetSettings(trackedDownload.RemoteRelease.Release.IndexerId);

        if (indexerSettings == null)
        {
            trackedDownload.Warn(new TrackedDownloadStatusMessage(trackedDownload.DownloadItem.Title, importResult.Errors));
            return true;
        }

        var failsDownloadAs = importResult.FailsDownloadAs;

        if (failsDownloadAs.HasValue && indexerSettings.FailDownloads.Contains(failsDownloadAs.Value))
        {
            _logger.Trace("Download '{0}' contains a file rejected as {1}, marking as failed", trackedDownload.DownloadItem.Title, failsDownloadAs.Value);
            trackedDownload.Fail();
        }
        else
        {
            trackedDownload.Warn(new TrackedDownloadStatusMessage(trackedDownload.DownloadItem.Title, importResult.Errors));
        }

        return true;
    }
}
