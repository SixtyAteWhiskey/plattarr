using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.CustomFormats;
using NzbDrone.Core.Music.DecisionEngine.Specifications;
using NzbDrone.Core.Music.History;
using Radarr.Api.V3.Music.Albums;
using Radarr.Api.V3.Music.Artist;
using Radarr.Api.V3.Music.Tracks;
using Radarr.Http;
using Radarr.Http.Extensions;

namespace Radarr.Api.V3.Music.History
{
    // Lidarr's history controller. The movie controller owns /history, so the Music history is served
    // beside it at /history/music.
    [V3ApiController("history/music")]
    public class AlbumHistoryController : Controller
    {
        private readonly IHistoryService _historyService;
        private readonly IMusicCustomFormatCalculationService _formatCalculator;
        private readonly IUpgradableSpecification _upgradableSpecification;
        private readonly IFailedDownloadService _failedDownloadService;
        private readonly IArtistService _artistService;
        private readonly IAlbumService _albumService;
        private readonly ITrackService _trackService;

        public AlbumHistoryController(IHistoryService historyService,
                                      IMusicCustomFormatCalculationService formatCalculator,
                                      IUpgradableSpecification upgradableSpecification,
                                      IFailedDownloadService failedDownloadService,
                                      IArtistService artistService,
                                      IAlbumService albumService,
                                      ITrackService trackService)
        {
            _historyService = historyService;
            _formatCalculator = formatCalculator;
            _upgradableSpecification = upgradableSpecification;
            _failedDownloadService = failedDownloadService;
            _artistService = artistService;
            _albumService = albumService;
            _trackService = trackService;
        }

        protected AlbumHistoryResource MapToResource(AlbumHistory model, bool includeArtist, bool includeAlbum, bool includeTrack)
        {
            model.Artist ??= _artistService.GetArtist(model.ParentId);

            var resource = model.ToResource(_formatCalculator);

            if (includeArtist)
            {
                resource.Artist = model.Artist.ToResource();
            }

            if (includeAlbum)
            {
                model.Album ??= _albumService.GetAlbum(model.ItemId);
                resource.Album = model.Album.ToResource();
            }

            if (includeTrack && resource.TrackId > 0)
            {
                resource.Track = _trackService.GetTrack(resource.TrackId).ToResource();
            }

            if (model.Artist != null)
            {
                resource.QualityCutoffNotMet = _upgradableSpecification.QualityCutoffNotMet(model.Artist.QualityProfile.Value, model.Quality);
            }

            return resource;
        }

        [HttpGet]
        [Produces("application/json")]
        public PagingResource<AlbumHistoryResource> GetHistory([FromQuery] PagingRequestResource paging, bool includeArtist, bool includeAlbum, bool includeTrack, [FromQuery(Name = "eventType")] int[] eventTypes, int? albumId, string downloadId, [FromQuery] int[] artistIds = null, [FromQuery] int[] quality = null)
        {
            var pagingResource = new PagingResource<AlbumHistoryResource>(paging);
            var pagingSpec = pagingResource.MapToPagingSpec<AlbumHistoryResource, AlbumHistory>(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "date",
                    "artists.sortName",
                    "albums.title"
                },
                "date",
                SortDirection.Descending);

            if (eventTypes != null && eventTypes.Any())
            {
                pagingSpec.FilterExpressions.Add(v => eventTypes.Contains((int)v.EventType));
            }

            if (albumId.HasValue)
            {
                pagingSpec.FilterExpressions.Add(h => h.ItemId == albumId);
            }

            if (downloadId.IsNotNullOrWhiteSpace())
            {
                pagingSpec.FilterExpressions.Add(h => h.DownloadId == downloadId);
            }

            if (artistIds != null && artistIds.Any())
            {
                pagingSpec.FilterExpressions.Add(h => artistIds.Contains(h.ParentId));
            }

            return pagingSpec.ApplyToPage(h => _historyService.Paged(pagingSpec, quality), h => MapToResource(h, includeArtist, includeAlbum, includeTrack));
        }

        [HttpGet("since")]
        [Produces("application/json")]
        public List<AlbumHistoryResource> GetHistorySince(DateTime date, AlbumHistoryEventType? eventType = null, bool includeArtist = false, bool includeAlbum = false, bool includeTrack = false)
        {
            return _historyService.Since(date, eventType).Select(h => MapToResource(h, includeArtist, includeAlbum, includeTrack)).ToList();
        }

        [HttpGet("artist")]
        [Produces("application/json")]
        public List<AlbumHistoryResource> GetArtistHistory(int artistId, int? albumId = null, AlbumHistoryEventType? eventType = null, bool includeArtist = false, bool includeAlbum = false, bool includeTrack = false)
        {
            var artist = _artistService.GetArtist(artistId);

            if (albumId.HasValue)
            {
                return _historyService.GetByAlbum(albumId.Value, eventType).Select(h =>
                {
                    h.Artist = artist;

                    return MapToResource(h, includeArtist, includeAlbum, includeTrack);
                }).ToList();
            }

            return _historyService.GetByArtist(artistId, eventType).Select(h =>
            {
                h.Artist = artist;

                return MapToResource(h, includeArtist, includeAlbum, includeTrack);
            }).ToList();
        }

        [HttpPost("failed/{id}")]
        public object MarkAsFailed([FromRoute] int id)
        {
            _failedDownloadService.MarkAsFailed(MediaKind.Music, id);
            return new { };
        }
    }
}
