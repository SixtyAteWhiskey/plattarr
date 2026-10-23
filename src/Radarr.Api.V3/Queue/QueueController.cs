using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Tv;
using NzbDrone.SignalR;
using Radarr.Http;
using Radarr.Http.Extensions;
using Radarr.Http.REST;
using Radarr.Http.REST.Attributes;

namespace Radarr.Api.V3.Queue
{
    [V3ApiController]
    public class QueueController : RestControllerWithSignalR<QueueResource, NzbDrone.Core.Queue.Queue>,
                               IHandle<QueueUpdatedEvent>, IHandle<PendingReleasesUpdatedEvent>
    {
        private readonly IQueueService _queueService;
        private readonly IPendingQueueService _pendingReleaseService;

        private readonly QualityModelComparer _qualityComparer;
        private readonly ITrackedDownloadService _trackedDownloadService;
        private readonly IFailedDownloadService _failedDownloadService;
        private readonly IIgnoredDownloadService _ignoredDownloadService;
        private readonly IProvideDownloadClient _downloadClientProvider;
        private readonly NzbDrone.Core.Movies.Blocklisting.IBlocklistService _movieBlocklistService;
        private readonly NzbDrone.Core.Tv.Blocklisting.IBlocklistService _tvBlocklistService;

        public QueueController(IBroadcastSignalRMessage broadcastSignalRMessage,
                           IQueueService queueService,
                           IPendingQueueService pendingReleaseService,
                           QualityProfileService qualityProfileService,
                           ITrackedDownloadService trackedDownloadService,
                           IFailedDownloadService failedDownloadService,
                           IIgnoredDownloadService ignoredDownloadService,
                           IProvideDownloadClient downloadClientProvider,
                           NzbDrone.Core.Movies.Blocklisting.IBlocklistService movieBlocklistService,
                           NzbDrone.Core.Tv.Blocklisting.IBlocklistService tvBlocklistService)
            : base(broadcastSignalRMessage)
        {
            _queueService = queueService;
            _pendingReleaseService = pendingReleaseService;
            _trackedDownloadService = trackedDownloadService;
            _failedDownloadService = failedDownloadService;
            _ignoredDownloadService = ignoredDownloadService;
            _downloadClientProvider = downloadClientProvider;
            _movieBlocklistService = movieBlocklistService;
            _tvBlocklistService = tvBlocklistService;

            _qualityComparer = new QualityModelComparer(qualityProfileService.GetDefaultProfile(string.Empty));
        }

        [NonAction]
        public override Results<Ok<QueueResource>, NotFound> GetResourceByIdWithErrorHandler(int id)
        {
            return base.GetResourceByIdWithErrorHandler(id);
        }

        protected override QueueResource GetResourceById(int id)
        {
            throw new NotImplementedException();
        }

        [RestDeleteById]
        public Results<Ok, NotFound> RemoveAction(int id, bool removeFromClient = true, bool blocklist = false, bool skipRedownload = false, bool changeCategory = false)
        {
            var pendingRelease = _pendingReleaseService.FindPendingQueueItem(id);

            if (pendingRelease != null)
            {
                Remove(pendingRelease, blocklist);

                return TypedResults.Ok();
            }

            var trackedDownload = GetTrackedDownload(id);

            if (trackedDownload == null)
            {
                throw new NotFoundException();
            }

            Remove(trackedDownload, removeFromClient, blocklist, skipRedownload, changeCategory);
            _trackedDownloadService.StopTracking(trackedDownload.DownloadItem.DownloadId);

            return TypedResults.Ok();
        }

        [HttpDelete("bulk")]
        [Consumes("application/json")]
        public Ok<object> RemoveMany([FromBody] QueueBulkResource resource, [FromQuery] bool removeFromClient = true, [FromQuery] bool blocklist = false, [FromQuery] bool skipRedownload = false, [FromQuery] bool changeCategory = false)
        {
            var trackedDownloadIds = new List<string>();
            var pendingToRemove = new List<NzbDrone.Core.Queue.Queue>();
            var trackedToRemove = new List<TrackedDownload>();

            foreach (var id in resource.Ids)
            {
                var pendingRelease = _pendingReleaseService.FindPendingQueueItem(id);

                if (pendingRelease != null)
                {
                    pendingToRemove.Add(pendingRelease);
                    continue;
                }

                var trackedDownload = GetTrackedDownload(id);

                if (trackedDownload != null)
                {
                    trackedToRemove.Add(trackedDownload);
                }
            }

            foreach (var pendingRelease in pendingToRemove.DistinctBy(p => p.Id))
            {
                Remove(pendingRelease, blocklist);
            }

            foreach (var trackedDownload in trackedToRemove.DistinctBy(t => t.DownloadItem.DownloadId))
            {
                Remove(trackedDownload, removeFromClient, blocklist, skipRedownload, changeCategory);
                trackedDownloadIds.Add(trackedDownload.DownloadItem.DownloadId);
            }

            _trackedDownloadService.StopTracking(trackedDownloadIds);

            return TypedResults.Ok<object>(new { });
        }

        [HttpGet]
        [Produces("application/json")]
        public Ok<PagingResource<QueueResource>> GetQueue([FromQuery] PagingRequestResource paging, bool includeUnknownMovieItems = false, bool includeMovie = false, bool includeSeries = false, bool includeEpisode = false, [FromQuery] int[] movieIds = null, [FromQuery] int[] seriesIds = null, DownloadProtocol? protocol = null, [FromQuery] int[] languages = null, [FromQuery] int[] quality = null, [FromQuery] QueueStatus[] status = null)
        {
            var pagingResource = new PagingResource<QueueResource>(paging);
            var pagingSpec = pagingResource.MapToPagingSpec<QueueResource, NzbDrone.Core.Queue.Queue>(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "added",
                    "downloadClient",
                    "estimatedCompletionTime",
                    "indexer",
                    "languages",
                    "movies.sortTitle",
                    "series.sortTitle",
                    "progress",
                    "protocol",
                    "quality",
                    "size",
                    "status",
                    "timeleft",
                    "title",
                    "year"
                },
                "timeleft",
                SortDirection.Ascending);

            return TypedResults.Ok(pagingSpec.ApplyToPage((spec) => GetQueue(spec, movieIds?.ToHashSet(), seriesIds?.ToHashSet(), protocol, languages?.ToHashSet(), quality?.ToHashSet(), status?.ToHashSet(), includeUnknownMovieItems), (q) => MapToResource(q, includeMovie, includeSeries, includeEpisode)));
        }

        private PagingSpec<NzbDrone.Core.Queue.Queue> GetQueue(PagingSpec<NzbDrone.Core.Queue.Queue> pagingSpec, HashSet<int> movieIds, HashSet<int> seriesIds, DownloadProtocol? protocol, HashSet<int> languages, HashSet<int> quality, HashSet<QueueStatus> status, bool includeUnknownMovieItems)
        {
            var ascending = pagingSpec.SortDirection == SortDirection.Ascending;
            var orderByFunc = GetOrderByFunc(pagingSpec);

            var queue = _queueService.GetQueue();
            var filteredQueue = includeUnknownMovieItems ? queue : queue.Where(q => q.IsMatched());
            var pending = _pendingReleaseService.GetPendingQueue();

            var hasMovieIdFilter = movieIds is { Count: > 0 };
            var hasSeriesIdFilter = seriesIds is { Count: > 0 };
            var hasLanguageFilter = languages is { Count: > 0 };
            var hasQualityFilter = quality is { Count: > 0 };
            var hasStatusFilter = status is { Count: > 0 };

            var fullQueue = filteredQueue.Concat(pending).Where(q =>
            {
                var include = true;

                if (hasMovieIdFilter)
                {
                    include &= q.GetMovie() != null && movieIds.Contains(q.GetMovie().Id);
                }

                if (hasSeriesIdFilter)
                {
                    include &= q.GetSeries() != null && seriesIds.Contains(q.GetSeries().Id);
                }

                if (include && protocol.HasValue)
                {
                    include &= q.Protocol == protocol.Value;
                }

                if (include && hasLanguageFilter)
                {
                    include &= q.Languages.Any(l => languages.Contains(l.Id));
                }

                if (include && hasQualityFilter)
                {
                    include &= quality.Contains(q.Quality.Quality.Id);
                }

                if (include && hasStatusFilter)
                {
                    include &= status.Contains(q.Status);
                }

                return include;
            }).ToList();

            IOrderedEnumerable<NzbDrone.Core.Queue.Queue> ordered;

            if (pagingSpec.SortKey == "timeleft")
            {
                ordered = ascending
                    ? fullQueue.OrderBy(q => q.TimeLeft, new TimeleftComparer())
                    : fullQueue.OrderByDescending(q => q.TimeLeft, new TimeleftComparer());
            }
            else if (pagingSpec.SortKey == "estimatedCompletionTime")
            {
                ordered = ascending
                    ? fullQueue.OrderBy(q => q.EstimatedCompletionTime, new DatetimeComparer())
                    : fullQueue.OrderByDescending(q => q.EstimatedCompletionTime,
                        new DatetimeComparer());
            }
            else if (pagingSpec.SortKey == "added")
            {
                ordered = ascending
                    ? fullQueue.OrderBy(q => q.Added, new DatetimeComparer())
                    : fullQueue.OrderByDescending(q => q.Added,
                        new DatetimeComparer());
            }
            else if (pagingSpec.SortKey == "protocol")
            {
                ordered = ascending
                    ? fullQueue.OrderBy(q => q.Protocol)
                    : fullQueue.OrderByDescending(q => q.Protocol);
            }
            else if (pagingSpec.SortKey == "indexer")
            {
                ordered = ascending
                    ? fullQueue.OrderBy(q => q.Indexer, StringComparer.InvariantCultureIgnoreCase)
                    : fullQueue.OrderByDescending(q => q.Indexer, StringComparer.InvariantCultureIgnoreCase);
            }
            else if (pagingSpec.SortKey == "downloadClient")
            {
                ordered = ascending
                    ? fullQueue.OrderBy(q => q.DownloadClient, StringComparer.InvariantCultureIgnoreCase)
                    : fullQueue.OrderByDescending(q => q.DownloadClient, StringComparer.InvariantCultureIgnoreCase);
            }
            else if (pagingSpec.SortKey == "quality")
            {
                ordered = ascending
                    ? fullQueue.OrderBy(q => q.Quality, _qualityComparer)
                    : fullQueue.OrderByDescending(q => q.Quality, _qualityComparer);
            }
            else if (pagingSpec.SortKey == "languages")
            {
                ordered = ascending
                    ? fullQueue.OrderBy(q => q.Languages, new LanguagesComparer())
                    : fullQueue.OrderByDescending(q => q.Languages, new LanguagesComparer());
            }
            else
            {
                ordered = ascending ? fullQueue.OrderBy(orderByFunc) : fullQueue.OrderByDescending(orderByFunc);
            }

            ordered = ordered.ThenByDescending(q => q.Size == 0 ? 0 : 100 - (q.SizeLeft / q.Size * 100));

            pagingSpec.Records = ordered.Skip((pagingSpec.Page - 1) * pagingSpec.PageSize).Take(pagingSpec.PageSize).ToList();
            pagingSpec.TotalRecords = fullQueue.Count;

            if (pagingSpec.Records.Empty() && pagingSpec.Page > 1)
            {
                pagingSpec.Page = (int)Math.Max(Math.Ceiling((decimal)(pagingSpec.TotalRecords / pagingSpec.PageSize)), 1);
                pagingSpec.Records = ordered.Skip((pagingSpec.Page - 1) * pagingSpec.PageSize).Take(pagingSpec.PageSize).ToList();
            }

            return pagingSpec;
        }

        private Func<NzbDrone.Core.Queue.Queue, object> GetOrderByFunc(PagingSpec<NzbDrone.Core.Queue.Queue> pagingSpec)
        {
            switch (pagingSpec.SortKey)
            {
                case "status":
                    return q => q.Status.ToString();
                case "movies.sortTitle":
                    return q => q.GetMovie()?.MovieMetadata.Value.SortTitle ?? q.Title;
                case "series.sortTitle":
                    return q => q.GetSeries()?.SortTitle ?? q.Title;
                case "title":
                    return q => q.Title;
                case "year":
                    return q => q.RemoteRelease?.Year ?? 0;
                case "languages":
                    return q => q.Languages;
                case "quality":
                    return q => q.Quality;
                case "size":
                    return q => q.Size;
                case "progress":
                    // Avoid exploding if a download's size is 0
                    return q => 100 - (q.SizeLeft / Math.Max(q.Size * 100, 1));
                default:
                    return q => q.TimeLeft;
            }
        }

        private void Remove(NzbDrone.Core.Queue.Queue pendingRelease, bool blocklist)
        {
            if (blocklist)
            {
                if (pendingRelease.IsKind(MediaKind.Tv))
                {
                    _tvBlocklistService.Block(pendingRelease.GetRemoteEpisode(), "Pending release manually blocklisted");
                }
                else
                {
                    _movieBlocklistService.Block(pendingRelease.GetRemoteMovie(), "Pending release manually blocklisted");
                }
            }

            _pendingReleaseService.RemovePendingQueueItem(pendingRelease);
        }

        private TrackedDownload Remove(TrackedDownload trackedDownload, bool removeFromClient, bool blocklist, bool skipRedownload, bool changeCategory)
        {
            if (removeFromClient)
            {
                var downloadClient = _downloadClientProvider.Get(trackedDownload.DownloadClient);

                if (downloadClient == null)
                {
                    throw new BadRequestException();
                }

                downloadClient.RemoveItem(trackedDownload.DownloadItem, true);
            }
            else if (changeCategory)
            {
                var downloadClient = _downloadClientProvider.Get(trackedDownload.DownloadClient);

                if (downloadClient == null)
                {
                    throw new BadRequestException();
                }

                downloadClient.MarkItemAsImported(trackedDownload.DownloadItem);
            }

            if (blocklist)
            {
                _failedDownloadService.MarkAsFailed(trackedDownload, skipRedownload);
            }

            if (!removeFromClient && !blocklist && !changeCategory)
            {
                if (!_ignoredDownloadService.IgnoreDownload(trackedDownload))
                {
                    return null;
                }
            }

            return trackedDownload;
        }

        private TrackedDownload GetTrackedDownload(int queueId)
        {
            var queueItem = _queueService.Find(queueId);

            if (queueItem == null)
            {
                throw new NotFoundException();
            }

            var trackedDownload = _trackedDownloadService.Find(queueItem.DownloadId);

            if (trackedDownload == null)
            {
                throw new NotFoundException();
            }

            return trackedDownload;
        }

        private QueueResource MapToResource(NzbDrone.Core.Queue.Queue queueItem, bool includeMovie, bool includeSeries, bool includeEpisode)
        {
            return queueItem.ToResource(includeMovie, includeSeries, includeEpisode);
        }

        [NonAction]
        public void Handle(QueueUpdatedEvent message)
        {
            BroadcastResourceChange(ModelAction.Sync);
        }

        [NonAction]
        public void Handle(PendingReleasesUpdatedEvent message)
        {
            BroadcastResourceChange(ModelAction.Sync);
        }
    }
}
