using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.ArtistStats;
using NzbDrone.Core.Music.Covers;
using NzbDrone.Core.Music.DecisionEngine.Specifications;
using NzbDrone.Core.Tags;
using NzbDrone.SignalR;
using Radarr.Api.V3.Music.Albums;
using Radarr.Http;

namespace Radarr.Api.V3.Music.Calendar
{
    // Lidarr's calendar. The movie controller owns /calendar, so the album calendar is served beside
    // it at /calendar/music.
    [V3ApiController("calendar/music")]
    public class MusicCalendarController : AlbumControllerWithSignalR
    {
        private readonly IArtistService _artistService;
        private readonly ITagService _tagService;

        public MusicCalendarController(IAlbumService albumService,
                                       IArtistService artistService,
                                       IArtistStatisticsService artistStatisticsService,
                                       IMapCoversToLocal coverMapper,
                                       IUpgradableSpecification upgradableSpecification,
                                       ITagService tagService,
                                       IBroadcastSignalRMessage signalRBroadcaster)
            : base(albumService, artistStatisticsService, coverMapper, upgradableSpecification, signalRBroadcaster)
        {
            _artistService = artistService;
            _tagService = tagService;
        }

        [NonAction]
        public override Results<Ok<AlbumResource>, NotFound> GetResourceByIdWithErrorHandler(int id)
        {
            throw new NotImplementedException();
        }

        [HttpGet]
        [Produces("application/json")]
        public List<AlbumResource> GetCalendar(DateTime? start, DateTime? end, bool unmonitored = false, bool includeArtist = false, string tags = "")
        {
            var startUse = start ?? DateTime.Today;
            var endUse = end ?? DateTime.Today.AddDays(2);
            var albums = _albumService.AlbumsBetweenDates(startUse, endUse, unmonitored);
            var allArtists = _artistService.GetAllArtists();
            var parsedTags = new List<int>();
            var result = new List<Album>();

            if (tags.IsNotNullOrWhiteSpace())
            {
                parsedTags.AddRange(tags.Split(',').Select(_tagService.GetTag).Select(t => t.Id));
            }

            foreach (var album in albums)
            {
                var artist = allArtists.SingleOrDefault(s => s.Id == album.ArtistId);

                if (artist == null)
                {
                    continue;
                }

                if (parsedTags.Any() && parsedTags.None(artist.Tags.Contains))
                {
                    continue;
                }

                result.Add(album);
            }

            var resources = MapToResource(result, includeArtist);

            return resources.OrderBy(e => e.ReleaseDate).ToList();
        }
    }
}
