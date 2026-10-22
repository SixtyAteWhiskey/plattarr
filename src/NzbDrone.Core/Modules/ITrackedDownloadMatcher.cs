using NzbDrone.Core.Download.History;
using NzbDrone.Core.Download.TrackedDownloads;

namespace NzbDrone.Core.Modules
{
    // Contract 5, tracked download routing (docs/module-interface.md). The core decides which media
    // type a download in a client belongs to (grab history, then the client category, then the title)
    // and hands it to that media type's matcher, which attributes it to a library entity.
    public interface ITrackedDownloadMatcher
    {
        MediaKind Kind { get; }

        // Routing rule 3: true when the download's title parses as this media type. Nothing is
        // mapped and nothing is logged as a failure.
        bool MatchesTitle(string title);

        // Attribute the download: set RemoteRelease (the module's RemoteRelease subclass, or null
        // when the download matches no library entity), Indexer and Added from the grab, and warn on
        // the tracked download when the match is ambiguous or fails. downloadHistory is the latest
        // download history row for the download, null when Plattarr never grabbed it.
        void Match(TrackedDownload trackedDownload, DownloadHistory downloadHistory);

        // Rebuild RemoteRelease from the title after the library changed (a series or movie was
        // added, edited or deleted). The grab details on the release are not recomputed.
        void Rematch(TrackedDownload trackedDownload);
    }
}
