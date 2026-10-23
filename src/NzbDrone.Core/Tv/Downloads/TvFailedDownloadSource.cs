using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv.History;

namespace NzbDrone.Core.Tv.Downloads
{
    // Describes a failed TV download from the episode history rows it was grabbed from. A release
    // that covers several episodes has one grab row per episode, so one failure names them all.
    // Sonarr's FailedDownloadService, split at the seam the core's FailedDownloadService leaves.
    public class TvFailedDownloadSource : IFailedDownloadSource
    {
        private readonly IHistoryService _historyService;

        public TvFailedDownloadSource(IHistoryService historyService)
        {
            _historyService = historyService;
        }

        public MediaKind Kind => MediaKind.Tv;

        public bool WasGrabbed(string downloadId)
        {
            return GetGrabbedHistory(downloadId).Any();
        }

        public DownloadFailedEvent Describe(string downloadId, string message, TrackedDownload trackedDownload, bool skipRedownload)
        {
            var history = GetGrabbedHistory(downloadId);

            return history.Any()
                ? Describe(history.First(), GetEpisodeIds(history), message, trackedDownload, skipRedownload)
                : null;
        }

        public DownloadFailedEvent Describe(int historyId, string message, bool skipRedownload)
        {
            var history = _historyService.Get(historyId);
            var downloadId = history.DownloadId;

            if (downloadId.IsNullOrWhiteSpace())
            {
                if (history.EventType == EpisodeHistoryEventType.Grabbed)
                {
                    return Describe(history, new List<int> { history.ItemId }, message, null, skipRedownload);
                }

                throw new InvalidOperationException("Unable to mark download as failed, history item was not grabbed and has no download ID");
            }

            var grabbedHistory = new List<EpisodeHistory>();

            // If the history item is a grabbed item (it should be, at least from the UI) add it as the first history item
            if (history.EventType == EpisodeHistoryEventType.Grabbed)
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
                return Describe(grabbedHistory.First(), GetEpisodeIds(grabbedHistory), message, null, false);
            }

            throw new InvalidOperationException("Unable to mark download as failed, no grabbed history available");
        }

        private static DownloadFailedEvent Describe(EpisodeHistory historyItem, List<int> episodeIds, string message, TrackedDownload trackedDownload, bool skipRedownload)
        {
            Enum.TryParse(historyItem.Data.GetValueOrDefault(EpisodeHistory.RELEASE_SOURCE, ReleaseSourceType.Unknown.ToString()), out ReleaseSourceType releaseSource);

            return new DownloadFailedEvent
            {
                Kind = MediaKind.Tv,
                ParentId = historyItem.ParentId,
                ItemIds = episodeIds,
                Quality = historyItem.Quality,
                SourceTitle = historyItem.SourceTitle,
                DownloadClient = historyItem.Data.GetValueOrDefault(EpisodeHistory.DOWNLOAD_CLIENT),
                DownloadId = historyItem.DownloadId,
                Message = message,
                Data = historyItem.Data,
                TrackedDownload = trackedDownload,
                Languages = historyItem.Languages,
                SkipRedownload = skipRedownload,
                ReleaseSource = releaseSource,
            };
        }

        private static List<int> GetEpisodeIds(List<EpisodeHistory> historyItems)
        {
            return historyItems.Select(h => h.ItemId).Distinct().ToList();
        }

        private List<EpisodeHistory> GetGrabbedHistory(string downloadId)
        {
            // Sort by date so items are always in the same order
            return _historyService.Find(downloadId, EpisodeHistoryEventType.Grabbed)
                .OrderByDescending(h => h.Date)
                .ToList();
        }
    }
}
