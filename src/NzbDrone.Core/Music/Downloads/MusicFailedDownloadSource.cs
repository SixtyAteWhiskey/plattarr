using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.History;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Music.Downloads
{
    // Describes a failed Music download from the album history rows it was grabbed from. A release that
    // covers several albums has one grab row per album, so one failure names them all. Lidarr's
    // FailedDownloadService, split at the seam the core's FailedDownloadService leaves (as TV did).
    public class MusicFailedDownloadSource : IFailedDownloadSource
    {
        private readonly IHistoryService _historyService;

        public MusicFailedDownloadSource(IHistoryService historyService)
        {
            _historyService = historyService;
        }

        public MediaKind Kind => MediaKind.Music;

        public bool WasGrabbed(string downloadId)
        {
            return GetGrabbedHistory(downloadId).Any();
        }

        public DownloadFailedEvent Describe(string downloadId, string message, TrackedDownload trackedDownload, bool skipRedownload)
        {
            var history = GetGrabbedHistory(downloadId);

            return history.Any()
                ? Describe(history.First(), GetAlbumIds(history), message, trackedDownload, skipRedownload)
                : null;
        }

        public DownloadFailedEvent Describe(int historyId, string message, bool skipRedownload)
        {
            var history = _historyService.Get(historyId);
            var downloadId = history.DownloadId;

            if (downloadId.IsNullOrWhiteSpace())
            {
                if (history.EventType == AlbumHistoryEventType.Grabbed)
                {
                    return Describe(history, new List<int> { history.ItemId }, message, null, skipRedownload);
                }

                throw new InvalidOperationException("Unable to mark download as failed, history item was not grabbed and has no download ID");
            }

            var grabbedHistory = new List<AlbumHistory>();

            // If the history item is a grabbed item (it should be, at least from the UI) add it as the first history item
            if (history.EventType == AlbumHistoryEventType.Grabbed)
            {
                grabbedHistory.Add(history);
            }

            // Add any other history items for the download ID then filter out any duplicate history items.
            grabbedHistory.AddRange(GetGrabbedHistory(downloadId));
            grabbedHistory = grabbedHistory.DistinctBy(h => h.Id).ToList();

            if (grabbedHistory.Any())
            {
                // Upstream behavior: a request to skip the redownload only counts for a history row
                // with no download id. A row with one always lets the redownload run.
                return Describe(grabbedHistory.First(), GetAlbumIds(grabbedHistory), message, null, false);
            }

            throw new InvalidOperationException("Unable to mark download as failed, no grabbed history available");
        }

        private static DownloadFailedEvent Describe(AlbumHistory historyItem, List<int> albumIds, string message, TrackedDownload trackedDownload, bool skipRedownload)
        {
            Enum.TryParse(historyItem.Data.GetValueOrDefault(AlbumHistory.RELEASE_SOURCE, ReleaseSourceType.Unknown.ToString()), out ReleaseSourceType releaseSource);

            return new DownloadFailedEvent
            {
                Kind = MediaKind.Music,
                ParentId = historyItem.ParentId,
                ItemIds = albumIds,

                // Core events carry the display copy of an audio quality; the Music handlers turn it back.
                Quality = historyItem.Quality.ToDisplayModel(),
                SourceTitle = historyItem.SourceTitle,
                DownloadClient = historyItem.Data.GetValueOrDefault(AlbumHistory.DOWNLOAD_CLIENT),
                DownloadId = historyItem.DownloadId,
                Message = message,
                Data = historyItem.Data,
                TrackedDownload = trackedDownload,
                SkipRedownload = skipRedownload,
                ReleaseSource = releaseSource
            };
        }

        private static List<int> GetAlbumIds(List<AlbumHistory> historyItems)
        {
            return historyItems.Select(h => h.ItemId).Distinct().ToList();
        }

        private List<AlbumHistory> GetGrabbedHistory(string downloadId)
        {
            // Sort by date so items are always in the same order
            return _historyService.Find(downloadId, AlbumHistoryEventType.Grabbed)
                .OrderByDescending(h => h.Date)
                .ToList();
        }
    }
}
