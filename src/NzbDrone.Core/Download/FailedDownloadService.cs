using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Download
{
    public interface IFailedDownloadService
    {
        void MarkAsFailed(MediaKind kind, int historyId, bool skipRedownload = false);
        DownloadFailedEvent MarkAsFailed(TrackedDownload trackedDownload, bool skipRedownload = false);
        void Check(TrackedDownload trackedDownload);
        void ProcessFailed(TrackedDownload trackedDownload);
    }

    // Runs the failed half of the tracked download state machine for every media type. What a
    // failure is about (the grab, the library entry) comes from the media type's
    // IFailedDownloadSource, picked by TrackedDownload.Kind.
    public class FailedDownloadService : IFailedDownloadService
    {
        private readonly Dictionary<MediaKind, IFailedDownloadSource> _sources;
        private readonly IEventAggregator _eventAggregator;

        public FailedDownloadService(IEnumerable<IFailedDownloadSource> sources,
                                     IEventAggregator eventAggregator)
        {
            _sources = sources.ToDictionary(s => s.Kind);
            _eventAggregator = eventAggregator;
        }

        public void MarkAsFailed(MediaKind kind, int historyId, bool skipRedownload = false)
        {
            if (!_sources.TryGetValue(kind, out var source))
            {
                throw new InvalidOperationException($"No failed download source is registered for {kind}");
            }

            _eventAggregator.PublishEvent(source.Describe(historyId, "Manually marked as failed", skipRedownload));
        }

        public DownloadFailedEvent MarkAsFailed(TrackedDownload trackedDownload, bool skipRedownload = false)
        {
            var failure = FindSource(trackedDownload)?.Describe(trackedDownload.DownloadItem.DownloadId, "Manually marked as failed", trackedDownload, skipRedownload);

            if (failure != null)
            {
                _eventAggregator.PublishEvent(failure);
            }

            return failure;
        }

        public void Check(TrackedDownload trackedDownload)
        {
            // Only process tracked downloads that are still downloading or import is blocked (if they fail after attempting to be processed)
            if (trackedDownload.State != TrackedDownloadState.Downloading && trackedDownload.State != TrackedDownloadState.ImportBlocked)
            {
                return;
            }

            if (trackedDownload.DownloadItem.IsEncrypted ||
                trackedDownload.DownloadItem.Status == DownloadItemStatus.Failed)
            {
                if (!WasGrabbed(trackedDownload))
                {
                    trackedDownload.Warn(trackedDownload.DownloadItem.IsEncrypted ? "Download is encrypted and wasn't grabbed by Radarr, skipping automatic download handling" : "Download has failed wasn't grabbed by Radarr, skipping automatic download handling");
                    return;
                }

                trackedDownload.State = TrackedDownloadState.FailedPending;
            }
        }

        public void ProcessFailed(TrackedDownload trackedDownload)
        {
            if (trackedDownload.State != TrackedDownloadState.FailedPending)
            {
                return;
            }

            var failure = "Failed download detected";

            if (trackedDownload.DownloadItem.IsEncrypted)
            {
                failure = "Encrypted download detected";
            }
            else if (trackedDownload.DownloadItem.Status == DownloadItemStatus.Failed && trackedDownload.DownloadItem.Message.IsNotNullOrWhiteSpace())
            {
                failure = trackedDownload.DownloadItem.Message;
            }

            var failedEvent = FindSource(trackedDownload)?.Describe(trackedDownload.DownloadItem.DownloadId, failure, trackedDownload, false);

            if (failedEvent == null)
            {
                return;
            }

            trackedDownload.State = TrackedDownloadState.Failed;
            _eventAggregator.PublishEvent(failedEvent);
        }

        // A download whose media type is unknown, or has no source, was not grabbed by any module
        // as far as the core can tell. The queue shows the warning; nothing is failed.
        private IFailedDownloadSource FindSource(TrackedDownload trackedDownload)
        {
            return trackedDownload.Kind.HasValue && _sources.TryGetValue(trackedDownload.Kind.Value, out var source)
                ? source
                : null;
        }

        private bool WasGrabbed(TrackedDownload trackedDownload)
        {
            return FindSource(trackedDownload)?.WasGrabbed(trackedDownload.DownloadItem.DownloadId) ?? false;
        }
    }
}
