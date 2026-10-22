using System.Collections.Generic;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.History;

namespace NzbDrone.Core.Movies.Upstream
{
    // Radarr's History.EventType and DownloadHistory.EventType values, mapped to
    // Plattarr's one by one (docs/importer.md, "Event type maps"). The two enums
    // are copies of Radarr's with the same numbers today; the table is explicit
    // so that a change on either side fails a test instead of silently
    // re-labelling history. A value with no entry is a skipped row (UNKNOWN_EVENT),
    // not a guess.
    //
    // Values are Radarr's MovieHistoryEventType and DownloadHistoryEventType at
    // develop 56f6168. The owner's database uses 1, 3, 4, 6 and 1, 2, 3, 5.
    //
    // This is module code (the event enum is the movie module's), so the core importer
    // cannot reference it directly; see docs/importer.md, "Module seam".
    public static class RadarrEventTypes
    {
        private static readonly Dictionary<int, MovieHistoryEventType> HistoryTypes = new Dictionary<int, MovieHistoryEventType>
        {
            { 0, MovieHistoryEventType.Unknown },
            { 1, MovieHistoryEventType.Grabbed },
            { 3, MovieHistoryEventType.DownloadFolderImported },
            { 4, MovieHistoryEventType.DownloadFailed },
            { 6, MovieHistoryEventType.MovieFileDeleted },
            { 7, MovieHistoryEventType.MovieFolderImported },
            { 8, MovieHistoryEventType.MovieFileRenamed },
            { 9, MovieHistoryEventType.DownloadIgnored }
        };

        private static readonly Dictionary<int, DownloadHistoryEventType> DownloadTypes = new Dictionary<int, DownloadHistoryEventType>
        {
            { 1, DownloadHistoryEventType.DownloadGrabbed },
            { 2, DownloadHistoryEventType.DownloadImported },
            { 3, DownloadHistoryEventType.DownloadFailed },
            { 4, DownloadHistoryEventType.DownloadIgnored },
            { 5, DownloadHistoryEventType.FileImported }
        };

        public static IReadOnlyDictionary<int, MovieHistoryEventType> History => HistoryTypes;
        public static IReadOnlyDictionary<int, DownloadHistoryEventType> Downloads => DownloadTypes;

        public static bool TryMapHistory(int upstream, out MovieHistoryEventType type)
        {
            return HistoryTypes.TryGetValue(upstream, out type);
        }

        public static bool TryMapDownloadHistory(int upstream, out DownloadHistoryEventType type)
        {
            return DownloadTypes.TryGetValue(upstream, out type);
        }
    }
}
