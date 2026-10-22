using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.History;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Movies.Downloads
{
    // Describes a failed movie download from the movie history row it was grabbed from.
    public class MovieFailedDownloadSource : IFailedDownloadSource
    {
        private readonly IHistoryService _historyService;

        public MovieFailedDownloadSource(IHistoryService historyService)
        {
            _historyService = historyService;
        }

        public MediaKind Kind => MediaKind.Movies;

        public bool WasGrabbed(string downloadId)
        {
            return GetGrabbedHistory(downloadId).Any();
        }

        public DownloadFailedEvent Describe(string downloadId, string message, TrackedDownload trackedDownload, bool skipRedownload)
        {
            var history = GetGrabbedHistory(downloadId);

            return history.Any()
                ? Describe(history.First(), message, trackedDownload, skipRedownload)
                : null;
        }

        public DownloadFailedEvent Describe(int historyId, string message, bool skipRedownload)
        {
            var history = _historyService.Get(historyId);

            // Upstream behavior: a request to skip the redownload only counts for a history row
            // with no download id. A row with one always lets the redownload run.
            return history.DownloadId.IsNullOrWhiteSpace()
                ? Describe(history, message, null, skipRedownload)
                : Describe(history, message, null, false);
        }

        private static DownloadFailedEvent Describe(MovieHistory historyItem, string message, TrackedDownload trackedDownload, bool skipRedownload)
        {
            Enum.TryParse(historyItem.Data.GetValueOrDefault(MovieHistory.RELEASE_SOURCE, ReleaseSourceType.Unknown.ToString()), out ReleaseSourceType releaseSource);

            return new DownloadFailedEvent
            {
                Kind = MediaKind.Movies,
                ParentId = historyItem.ParentId,
                Quality = historyItem.Quality,
                SourceTitle = historyItem.SourceTitle,
                DownloadClient = historyItem.Data.GetValueOrDefault(MovieHistory.DOWNLOAD_CLIENT),
                DownloadId = historyItem.DownloadId,
                Message = message,
                Data = historyItem.Data,
                TrackedDownload = trackedDownload,
                Languages = historyItem.Languages,
                SkipRedownload = skipRedownload,
                ReleaseSource = releaseSource,
            };
        }

        private List<MovieHistory> GetGrabbedHistory(string downloadId)
        {
            // Sort by date so items are always in the same order
            return _historyService.Find(downloadId, MovieHistoryEventType.Grabbed)
                .OrderByDescending(h => h.Date)
                .ToList();
        }
    }
}
