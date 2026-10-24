using System;
using System.Collections.Generic;
using System.Linq;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Music;
using NzbDrone.Core.Tags;
using Radarr.Http;

namespace Radarr.Api.V3.Music.Calendar
{
    // Lidarr's iCal feed. Radarr's feed is Radarr.ics; this one keeps Lidarr's file name so existing
    // calendar subscriptions keep working.
    [V3FeedController("calendar")]
    public class MusicCalendarFeedController : Controller
    {
        private readonly IAlbumService _albumService;
        private readonly IArtistService _artistService;
        private readonly ITagService _tagService;

        public MusicCalendarFeedController(IAlbumService albumService, IArtistService artistService, ITagService tagService)
        {
            _albumService = albumService;
            _artistService = artistService;
            _tagService = tagService;
        }

        [HttpGet("Lidarr.ics")]
        public IActionResult GetCalendarFeed(int pastDays = 7, int futureDays = 28, string tags = "", bool unmonitored = false)
        {
            var start = DateTime.Today.AddDays(-pastDays);
            var end = DateTime.Today.AddDays(futureDays);
            var parsedTags = new List<int>();

            if (tags.IsNotNullOrWhiteSpace())
            {
                parsedTags.AddRange(tags.Split(',').Select(_tagService.GetTag).Select(t => t.Id));
            }

            var albums = _albumService.AlbumsBetweenDates(start, end, unmonitored);
            var calendar = new Ical.Net.Calendar
            {
                ProductId = "-//lidarr.audio//Lidarr//EN"
            };

            var calendarName = "Lidarr Music Schedule";
            calendar.AddProperty(new CalendarProperty("NAME", calendarName));
            calendar.AddProperty(new CalendarProperty("X-WR-CALNAME", calendarName));

            foreach (var album in albums.OrderBy(v => v.ReleaseDate.Value))
            {
                // Album.Artist is not populated by the AlbumsBetweenDates query, so the artist is read by id.
                var artist = _artistService.GetArtist(album.ArtistId);

                if (parsedTags.Any() && parsedTags.None(artist.Tags.Contains))
                {
                    continue;
                }

                var occurrence = calendar.Create<CalendarEvent>();
                occurrence.Uid = "Lidarr_album_" + album.Id;
                occurrence.Description = album.Overview;
                occurrence.Categories = album.Genres;

                // A release date is a calendar day, not an instant: it is written as that day. Ical.Net
                // rejects a Local-kind value, so the kind is left unspecified.
                occurrence.Start = new CalDateTime(DateTime.SpecifyKind(album.ReleaseDate.Value.Date, DateTimeKind.Unspecified), false);
                occurrence.End = occurrence.Start;

                occurrence.Summary = $"{artist.Name} - {album.Title}";
            }

            var serializer = (IStringSerializer)new SerializerFactory().Build(calendar.GetType(), new SerializationContext());
            var icalendar = serializer.SerializeToString(calendar);

            return Content(icalendar, "text/calendar");
        }
    }
}
