using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Download.TrackedDownloads
{
    public interface ITrackedDownloadService
    {
        TrackedDownload Find(string downloadId);
        void StopTracking(string downloadId);
        void StopTracking(List<string> downloadIds);
        TrackedDownload TrackDownload(DownloadClientDefinition downloadClient, DownloadClientItem downloadItem);
        List<TrackedDownload> GetTrackedDownloads();
        void UpdateTrackable(List<TrackedDownload> trackedDownloads);

        // Ask each download's media type to match it again, after its library changed.
        void Rematch(List<TrackedDownload> trackedDownloads);
    }

    // Contract 5 routing. The service owns the cache of tracked downloads and decides which media type
    // each one belongs to; the media type's ITrackedDownloadMatcher attributes it to a library entity.
    public class TrackedDownloadService : ITrackedDownloadService,
                                          IHandle<ReleaseGrabbedEvent>
    {
        private readonly IDownloadHistoryService _downloadHistoryService;
        private readonly IEventAggregator _eventAggregator;
        private readonly List<ITrackedDownloadMatcher> _matchers;
        private readonly Logger _logger;

        private readonly ICached<TrackedDownload> _cache;

        public TrackedDownloadService(IDownloadHistoryService downloadHistoryService,
                                      IEnumerable<ITrackedDownloadMatcher> matchers,
                                      IEventAggregator eventAggregator,
                                      ICacheManager cacheManager,
                                      Logger logger)
        {
            _downloadHistoryService = downloadHistoryService;
            _matchers = matchers.ToList();
            _eventAggregator = eventAggregator;
            _logger = logger;

            _cache = cacheManager.GetCache<TrackedDownload>(GetType());
        }

        public TrackedDownload Find(string downloadId)
        {
            return _cache.Find(downloadId);
        }

        public void StopTracking(string downloadId)
        {
            var trackedDownload = _cache.Find(downloadId);

            _cache.Remove(downloadId);
            _eventAggregator.PublishEvent(new TrackedDownloadsRemovedEvent(new List<TrackedDownload> { trackedDownload }));
        }

        public void StopTracking(List<string> downloadIds)
        {
            var trackedDownloads = new List<TrackedDownload>();

            foreach (var downloadId in downloadIds)
            {
                var trackedDownload = _cache.Find(downloadId);

                _cache.Remove(downloadId);
                trackedDownloads.Add(trackedDownload);
            }

            _eventAggregator.PublishEvent(new TrackedDownloadsRemovedEvent(trackedDownloads));
        }

        public TrackedDownload TrackDownload(DownloadClientDefinition downloadClient, DownloadClientItem downloadItem)
        {
            var existingItem = Find(downloadItem.DownloadId);

            if (existingItem != null && existingItem.State != TrackedDownloadState.Downloading)
            {
                LogItemChange(existingItem, existingItem.DownloadItem, downloadItem);

                existingItem.DownloadItem = downloadItem;
                existingItem.IsTrackable = true;

                return existingItem;
            }

            var trackedDownload = new TrackedDownload
            {
                DownloadClient = downloadClient.Id,
                DownloadItem = downloadItem,
                Protocol = downloadClient.Protocol,
                IsTrackable = true,
                HasNotifiedManualInteractionRequired = existingItem?.HasNotifiedManualInteractionRequired ?? false
            };

            var downloadHistory = _downloadHistoryService.GetLatestDownloadHistoryItem(downloadItem.DownloadId);

            if (downloadHistory != null)
            {
                trackedDownload.State = GetStateFromHistory(downloadHistory.EventType);
            }

            trackedDownload.Kind = Route(downloadClient, downloadItem, downloadHistory);

            if (trackedDownload.Kind.HasValue)
            {
                GetMatcher(trackedDownload.Kind.Value).Match(trackedDownload, downloadHistory);
            }
            else
            {
                _logger.Debug("No media type found for download '{0}'", downloadItem.Title);
            }

            LogItemChange(trackedDownload, existingItem?.DownloadItem, trackedDownload.DownloadItem);

            _cache.Set(trackedDownload.DownloadItem.DownloadId, trackedDownload);
            return trackedDownload;
        }

        public List<TrackedDownload> GetTrackedDownloads()
        {
            return _cache.Values.ToList();
        }

        public void UpdateTrackable(List<TrackedDownload> trackedDownloads)
        {
            var untrackable = GetTrackedDownloads().ExceptBy(t => t.DownloadItem.DownloadId, trackedDownloads, t => t.DownloadItem.DownloadId, StringComparer.CurrentCulture).ToList();

            foreach (var trackedDownload in untrackable)
            {
                trackedDownload.IsTrackable = false;
            }
        }

        public void Rematch(List<TrackedDownload> trackedDownloads)
        {
            if (trackedDownloads.Empty())
            {
                return;
            }

            foreach (var trackedDownload in trackedDownloads)
            {
                if (trackedDownload.Kind.HasValue)
                {
                    GetMatcher(trackedDownload.Kind.Value).Rematch(trackedDownload);
                }
            }

            _eventAggregator.PublishEvent(new TrackedDownloadRefreshedEvent(GetTrackedDownloads()));
        }

        public void Handle(ReleaseGrabbedEvent message)
        {
            if (message.DownloadId.IsNullOrWhiteSpace())
            {
                return;
            }

            var trackedDownload = _cache.Find(message.DownloadId);

            if (trackedDownload is { State: TrackedDownloadState.Imported or
                                            TrackedDownloadState.Failed or
                                            TrackedDownloadState.Ignored })
            {
                _cache.Remove(message.DownloadId);
            }
        }

        // The media type that owns a download, or null when none can be told.
        //
        // 1. The download history row records the media type of whatever Plattarr grabbed.
        // 2. The category the download sits in, when the client routes by category.
        // 3. Each media type is asked whether the title parses as its own. One yes wins. With no
        //    yes, a lone media type owns what no rule attributed (nothing to choose between). Several
        //    yes, or no yes with several media types, leave the download without a kind, and the
        //    completed download service blocks it with a warning.
        private MediaKind? Route(DownloadClientDefinition downloadClient, DownloadClientItem downloadItem, DownloadHistory downloadHistory)
        {
            if (downloadHistory != null && HasMatcher(downloadHistory.MediaKind))
            {
                return downloadHistory.MediaKind;
            }

            if (downloadClient.Settings is IModuleCategorySettings settings)
            {
                var kind = settings.KindForCategory(downloadItem.Category);

                if (kind.HasValue && HasMatcher(kind.Value))
                {
                    return kind;
                }
            }

            var matching = _matchers.Where(m => m.MatchesTitle(downloadItem.Title)).ToList();

            if (matching.Count == 1)
            {
                return matching[0].Kind;
            }

            if (matching.Count == 0 && _matchers.Count == 1)
            {
                return _matchers[0].Kind;
            }

            return null;
        }

        private bool HasMatcher(MediaKind kind)
        {
            return _matchers.Any(m => m.Kind == kind);
        }

        private ITrackedDownloadMatcher GetMatcher(MediaKind kind)
        {
            return _matchers.Single(m => m.Kind == kind);
        }

        private void LogItemChange(TrackedDownload trackedDownload, DownloadClientItem existingItem, DownloadClientItem downloadItem)
        {
            if (existingItem == null ||
                existingItem.Status != downloadItem.Status ||
                existingItem.CanBeRemoved != downloadItem.CanBeRemoved ||
                existingItem.CanMoveFiles != downloadItem.CanMoveFiles)
            {
                _logger.Debug("Tracking '{0}:{1}': ClientState={2}{3} RadarrStage={4} Kind={5} Match='{6}' OutputPath={7}.",
                    downloadItem.DownloadClientInfo.Name,
                    downloadItem.Title,
                    downloadItem.Status,
                    downloadItem.CanBeRemoved ? "" : downloadItem.CanMoveFiles ? " (busy)" : " (readonly)",
                    trackedDownload.State,
                    trackedDownload.Kind,
                    trackedDownload.RemoteRelease?.Parent?.Title,
                    downloadItem.OutputPath);
            }
        }

        private static TrackedDownloadState GetStateFromHistory(DownloadHistoryEventType eventType)
        {
            switch (eventType)
            {
                case DownloadHistoryEventType.DownloadImported:
                    return TrackedDownloadState.Imported;
                case DownloadHistoryEventType.DownloadFailed:
                    return TrackedDownloadState.Failed;
                case DownloadHistoryEventType.DownloadIgnored:
                    return TrackedDownloadState.Ignored;
                default:
                    return TrackedDownloadState.Downloading;
            }
        }
    }
}
