using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Crypto;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Queue
{
    public interface IQueueService
    {
        List<Queue> GetQueue();
        Queue Find(int id);
        void Remove(int id);
    }

    public class QueueService : IQueueService, IHandle<TrackedDownloadRefreshedEvent>
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly NzbDrone.Core.Hygiene.IHygieneQueueStrikeService _hygieneQueueStrikeService;
        private readonly Logger _logger;
        private static List<Queue> _queue = new();

        public QueueService(IEventAggregator eventAggregator, NzbDrone.Core.Hygiene.IHygieneQueueStrikeService hygieneQueueStrikeService, Logger logger)
        {
            _eventAggregator = eventAggregator;
            _hygieneQueueStrikeService = hygieneQueueStrikeService;
            _logger = logger;
        }

        public List<Queue> GetQueue()
        {
            return _queue;
        }

        public Queue Find(int id)
        {
            return _queue.SingleOrDefault(q => q.Id == id);
        }

        public void Remove(int id)
        {
            _queue.Remove(Find(id));
        }

        private Queue MapQueue(TrackedDownload trackedDownload)
        {
            var remoteRelease = trackedDownload.RemoteRelease;

            var queue = new Queue
            {
                Languages = remoteRelease?.Languages ?? new List<Language> { Language.Unknown },
                Quality = remoteRelease?.Quality ?? new QualityModel(Quality.Unknown),
                Title = trackedDownload.DownloadItem.Title,
                Size = trackedDownload.DownloadItem.TotalSize,
                SizeLeft = trackedDownload.DownloadItem.RemainingSize,
                TimeLeft = trackedDownload.DownloadItem.RemainingTime,
                Status = Enum.TryParse(trackedDownload.DownloadItem.Status.ToString(), out QueueStatus outValue) ? outValue : QueueStatus.Unknown,
                TrackedDownloadStatus = trackedDownload.Status,
                TrackedDownloadState = trackedDownload.State,
                StatusMessages = trackedDownload.StatusMessages.ToList(),
                ErrorMessage = trackedDownload.DownloadItem.Message,
                RemoteRelease = remoteRelease,
                DownloadId = trackedDownload.DownloadItem.DownloadId,
                Protocol = trackedDownload.Protocol,
                DownloadClient = trackedDownload.DownloadItem.DownloadClientInfo.Name,
                DownloadClientId = trackedDownload.DownloadClient,
                Indexer = trackedDownload.Indexer,
                OutputPath = trackedDownload.DownloadItem.OutputPath.ToString(),
                Added = trackedDownload.Added,
                DownloadClientHasPostImportCategory = trackedDownload.DownloadItem.DownloadClientInfo.HasPostImportCategory
            };

            queue.Id = HashConverter.GetHashInt31($"trackedDownload-{trackedDownload.DownloadClient}-{trackedDownload.DownloadItem.DownloadId}");

            if (queue.TimeLeft.HasValue)
            {
                queue.EstimatedCompletionTime = DateTime.UtcNow.Add(queue.TimeLeft.Value);
            }

            return queue;
        }

        public void Handle(TrackedDownloadRefreshedEvent message)
        {
            _queue = message.TrackedDownloads
                .Where(t => t.IsTrackable)
                .OrderBy(c => c.DownloadItem.RemainingTime)
                .Select(MapQueue)
                .ToList();

            try
            {
                _hygieneQueueStrikeService.Observe(_queue);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Unable to update Hygiene queue strikes");
            }

            _eventAggregator.PublishEvent(new QueueUpdatedEvent());
        }
    }
}
