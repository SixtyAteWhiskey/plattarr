using System.Collections.Generic;
using NzbDrone.Core.Indexers;

namespace NzbDrone.Core.Modules
{
    // Contract 5 in docs/module-interface.md. What the core reads from one file a module
    // considered while importing a completed download.
    public interface IImportResult
    {
        ImportResultType Result { get; }

        List<string> Errors { get; }

        // The file the result is about. Null when the module rejected the download before it
        // matched a file (for example, an unknown movie).
        string Path { get; }

        // The indexer FailDownloads option this rejection falls under, or null. The core fails
        // the download when the grabbing indexer has that option set.
        FailDownloads? FailsDownloadAs { get; }

        // True when this result alone makes automatic import impossible (for example a TV release
        // that spans several seasons): the core warns with the result's errors and blocks the
        // download for manual import, even when the release was not grabbed by Plattarr.
        bool BlocksAutomaticImport { get; }
    }
}
