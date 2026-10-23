using System.Collections.Generic;
using NzbDrone.Core.Download.TrackedDownloads;

namespace NzbDrone.Core.Modules
{
    // Contract 5 in docs/module-interface.md. The core tracks downloads, checks their output path,
    // and owns the tracked download state; the module matches files to its library and imports them.
    // CompletedDownloadService picks the importer by TrackedDownload.Kind.
    public interface IDownloadImporter
    {
        MediaKind Kind { get; }

        // True when a single rejected result (already warned about, or failed, by the core) also
        // moves the download to ImportBlocked, so the manual-interaction notification goes out once.
        // False leaves it pending and retried (Radarr's behavior); True is Sonarr's.
        bool BlocksImportAfterRejection { get; }

        // Called for a completed download whose output path is valid. Warns on the tracked download
        // when it returns Skip or Blocked; the core sets the state.
        DownloadImportCheck Check(TrackedDownload trackedDownload);

        // Imports from the download's output path. One result per file considered. Called only when
        // TrackedDownload.RemoteRelease has a parent.
        IReadOnlyList<IImportResult> Import(TrackedDownload trackedDownload, string outputPath);

        // True when everything the download should provide is imported, judged from the results
        // just returned and the module's history.
        bool IsImported(TrackedDownload trackedDownload, IReadOnlyList<IImportResult> importResults);

        // Called once per tracked download when automatic import is blocked, so the module can
        // publish its manual-interaction notification.
        void ImportBlocked(TrackedDownload trackedDownload);
    }

    public enum DownloadImportCheck
    {
        // Ready to import.
        Ready,

        // Not for automatic import; leave the download as it is.
        Skip,

        // Needs manual import.
        Blocked
    }
}
