using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Music;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Tv;
using NzbDrone.SignalR;
using Radarr.Http;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Queue
{
    [V3ApiController("queue/details")]
    public class QueueDetailsController : RestControllerWithSignalR<QueueResource, NzbDrone.Core.Queue.Queue>,
                               IHandle<QueueUpdatedEvent>, IHandle<PendingReleasesUpdatedEvent>
    {
        private readonly IQueueService _queueService;
        private readonly IPendingQueueService _pendingReleaseService;

        public QueueDetailsController(IBroadcastSignalRMessage broadcastSignalRMessage, IQueueService queueService, IPendingQueueService pendingReleaseService)
            : base(broadcastSignalRMessage)
        {
            _queueService = queueService;
            _pendingReleaseService = pendingReleaseService;
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

        [HttpGet]
        [Produces("application/json")]
        public Ok<List<QueueResource>> GetQueue(int? movieId, int? seriesId, int? artistId, [FromQuery] int[] albumIds, bool includeMovie = false, bool includeSeries = false, bool includeEpisode = false, bool includeArtist = false, bool includeAlbum = false)
        {
            var queue = _queueService.GetQueue();
            var pending = _pendingReleaseService.GetPendingQueue();
            var fullQueue = queue.Concat(pending);

            if (movieId.HasValue)
            {
                return TypedResults.Ok(fullQueue.Where(q => q.GetMovie()?.Id == movieId.Value).ToResource(includeMovie, includeSeries, includeEpisode, includeArtist, includeAlbum));
            }

            if (seriesId.HasValue)
            {
                return TypedResults.Ok(fullQueue.Where(q => q.GetSeries()?.Id == seriesId.Value).ToResource(includeMovie, includeSeries, includeEpisode, includeArtist, includeAlbum));
            }

            if (artistId.HasValue)
            {
                return TypedResults.Ok(fullQueue.Where(q => q.GetArtist()?.Id == artistId.Value).ToResource(includeMovie, includeSeries, includeEpisode, includeArtist, includeAlbum));
            }

            if (albumIds is { Length: > 0 })
            {
                return TypedResults.Ok(fullQueue.Where(q => q.GetRemoteAlbum()?.Albums != null && q.GetRemoteAlbum().Albums.Any(a => albumIds.Contains(a.Id))).ToResource(includeMovie, includeSeries, includeEpisode, includeArtist, includeAlbum));
            }

            return TypedResults.Ok(fullQueue.ToResource(includeMovie, includeSeries, includeEpisode, includeArtist, includeAlbum));
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
