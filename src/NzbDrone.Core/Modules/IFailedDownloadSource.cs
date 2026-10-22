using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;

namespace NzbDrone.Core.Modules
{
    // Contract 5, failure half (porting step 9k). The core decides when a download has failed
    // (encrypted, failed in the client, marked by the user) and runs the tracked download state
    // machine. The module owns the history the download was grabbed from, so it describes the
    // failure: which library entry, quality, languages and grab data the failure belongs to.
    // Picked by TrackedDownload.Kind, or by the media kind of a history row.
    public interface IFailedDownloadSource
    {
        MediaKind Kind { get; }

        // True when the module's history holds a grab for the download id.
        bool WasGrabbed(string downloadId);

        // The failure of a download the module grabbed, built from its newest grab. Null when
        // the module has no grab for the download id.
        DownloadFailedEvent Describe(string downloadId, string message, TrackedDownload trackedDownload, bool skipRedownload);

        // The failure of the download behind one history row (the history page's "mark as
        // failed"). Throws when the history row does not exist.
        DownloadFailedEvent Describe(int historyId, string message, bool skipRedownload);
    }
}
