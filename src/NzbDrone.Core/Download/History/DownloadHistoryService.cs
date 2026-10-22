using System;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Download.History
{
    public interface IDownloadHistoryService
    {
        bool DownloadAlreadyImported(string downloadId);
        DownloadHistory GetLatestDownloadHistoryItem(string downloadId);
        DownloadHistory GetLatestGrab(string downloadId);
    }

    public class DownloadHistoryService : IDownloadHistoryService,
                                          IHandle<ReleaseGrabbedEvent>,
                                          IHandle<DownloadCompletedEvent>,
                                          IHandle<DownloadFailedEvent>,
                                          IHandle<DownloadIgnoredEvent>
    {
        private readonly IDownloadHistoryRepository _repository;

        public DownloadHistoryService(IDownloadHistoryRepository repository)
        {
            _repository = repository;
        }

        public bool DownloadAlreadyImported(string downloadId)
        {
            var events = _repository.FindByDownloadId(downloadId);

            // Events are ordered by date descending, if a grabbed event comes before an imported event then it was never imported
            // or grabbed again after importing and should be reprocessed.
            foreach (var e in events)
            {
                if (e.EventType == DownloadHistoryEventType.DownloadGrabbed)
                {
                    return false;
                }

                if (e.EventType == DownloadHistoryEventType.DownloadImported)
                {
                    return true;
                }
            }

            return false;
        }

        public DownloadHistory GetLatestDownloadHistoryItem(string downloadId)
        {
            var events = _repository.FindByDownloadId(downloadId);

            // Events are ordered by date descending. We'll return the most recent expected event.
            foreach (var e in events)
            {
                if (e.EventType == DownloadHistoryEventType.DownloadIgnored)
                {
                    return e;
                }

                if (e.EventType == DownloadHistoryEventType.DownloadGrabbed)
                {
                    return e;
                }

                if (e.EventType == DownloadHistoryEventType.DownloadImported)
                {
                    return e;
                }

                if (e.EventType == DownloadHistoryEventType.DownloadFailed)
                {
                    return e;
                }
            }

            return null;
        }

        public DownloadHistory GetLatestGrab(string downloadId)
        {
            return _repository.FindByDownloadId(downloadId)
                              .FirstOrDefault(d => d.EventType == DownloadHistoryEventType.DownloadGrabbed);
        }

        public void Handle(ReleaseGrabbedEvent message)
        {
            // Don't store grabbed events for clients that don't download IDs
            if (message.DownloadId.IsNullOrWhiteSpace())
            {
                return;
            }

            var history = new DownloadHistory
            {
                MediaKind = message.Release.Kind,
                EventType = DownloadHistoryEventType.DownloadGrabbed,
                ParentId = message.Release.Parent.Id,
                DownloadId = message.DownloadId,
                SourceTitle = message.Release.Release.Title,
                Date = DateTime.UtcNow,
                Protocol = message.Release.Release.DownloadProtocol,
                IndexerId = message.Release.Release.IndexerId,
                DownloadClientId = message.DownloadClientId,
                Release = message.Release.Release
            };

            history.Data.Add("Indexer", message.Release.Release.Indexer);
            history.Data.Add("DownloadClient", message.DownloadClient);
            history.Data.Add("DownloadClientName", message.DownloadClientName);

            history.Data.Add("CustomFormatScore", message.Release.CustomFormatScore.ToString());
            _repository.Insert(history);
        }

        public void Handle(DownloadCompletedEvent message)
        {
            var downloadItem = message.TrackedDownload.DownloadItem;

            var history = new DownloadHistory
            {
                MediaKind = message.TrackedDownload.Kind.GetValueOrDefault(),
                EventType = DownloadHistoryEventType.DownloadImported,
                ParentId = message.ParentId,
                DownloadId = downloadItem.DownloadId,
                SourceTitle = downloadItem.Title,
                Date = DateTime.UtcNow,
                Protocol = message.TrackedDownload.Protocol,
                DownloadClientId = message.TrackedDownload.DownloadClient
            };

            history.Data.Add("DownloadClient", downloadItem.DownloadClientInfo.Type);
            history.Data.Add("DownloadClientName", downloadItem.DownloadClientInfo.Name);

            _repository.Insert(history);
        }

        public void Handle(DownloadFailedEvent message)
        {
            // Don't track failed download for an unknown download
            if (message.TrackedDownload == null)
            {
                return;
            }

            var history = new DownloadHistory
            {
                MediaKind = message.Kind,
                EventType = DownloadHistoryEventType.DownloadFailed,
                ParentId = message.ParentId,
                DownloadId = message.DownloadId,
                SourceTitle = message.SourceTitle,
                Date = DateTime.UtcNow,
                Protocol = message.TrackedDownload.Protocol,
                DownloadClientId = message.TrackedDownload.DownloadClient
            };

            history.Data.Add("DownloadClient", message.TrackedDownload.DownloadItem.DownloadClientInfo.Type);
            history.Data.Add("DownloadClientName", message.TrackedDownload.DownloadItem.DownloadClientInfo.Name);

            _repository.Insert(history);
        }

        public void Handle(DownloadIgnoredEvent message)
        {
            var history = new DownloadHistory
            {
                MediaKind = message.Kind,
                EventType = DownloadHistoryEventType.DownloadIgnored,
                ParentId = message.ParentId,
                DownloadId = message.DownloadId,
                SourceTitle = message.SourceTitle,
                Date = DateTime.UtcNow,
                Protocol = message.DownloadClientInfo.Protocol,
                DownloadClientId = message.DownloadClientInfo.Id
            };

            history.Data.Add("DownloadClient", message.DownloadClientInfo.Type);
            history.Data.Add("DownloadClientName", message.DownloadClientInfo.Name);

            _repository.Insert(history);
        }
    }
}
