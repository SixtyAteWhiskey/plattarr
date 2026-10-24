using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.ArtistStats;
using NzbDrone.Core.Music.Covers;
using NzbDrone.Core.Music.DecisionEngine.Specifications;
using NzbDrone.SignalR;
using Radarr.Api.V3.Music.Albums;
using Radarr.Http;
using Radarr.Http.Extensions;

namespace Radarr.Api.V3.Music.Wanted
{
    // Lidarr's wanted/cutoff. The movie controller owns /wanted/cutoff, so the albums whose cutoff
    // is unmet are served beside it at /wanted/cutoff/music.
    [V3ApiController("wanted/cutoff/music")]
    public class MusicCutoffController : AlbumControllerWithSignalR
    {
        private readonly IAlbumCutoffService _albumCutoffService;

        public MusicCutoffController(IAlbumCutoffService albumCutoffService,
                                     IAlbumService albumService,
                                     IArtistStatisticsService artistStatisticsService,
                                     IMapCoversToLocal coverMapper,
                                     IUpgradableSpecification upgradableSpecification,
                                     IBroadcastSignalRMessage signalRBroadcaster)
            : base(albumService, artistStatisticsService, coverMapper, upgradableSpecification, signalRBroadcaster)
        {
            _albumCutoffService = albumCutoffService;
        }

        [NonAction]
        public override Results<Ok<AlbumResource>, NotFound> GetResourceByIdWithErrorHandler(int id)
        {
            throw new NotImplementedException();
        }

        [HttpGet]
        [Produces("application/json")]
        public PagingResource<AlbumResource> GetCutoffUnmetAlbums([FromQuery] PagingRequestResource paging, bool includeArtist = false, bool monitored = true)
        {
            var pagingResource = new PagingResource<AlbumResource>(paging);
            var pagingSpec = pagingResource.MapToPagingSpec<AlbumResource, Album>(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "albums.releaseDate",
                    "albums.title",
                    "albums.lastSearchTime",
                    "artists.sortName"
                },
                "albums.releaseDate",
                SortDirection.Descending);

            if (monitored)
            {
                pagingSpec.FilterExpressions.Add(v => v.Monitored == true && v.Artist.Value.Monitored == true);
            }
            else
            {
                pagingSpec.FilterExpressions.Add(v => v.Monitored == false || v.Artist.Value.Monitored == false);
            }

            return pagingSpec.ApplyToPage(_albumCutoffService.AlbumsWhereCutoffUnmet, v => MapToResource(v, includeArtist));
        }
    }
}
