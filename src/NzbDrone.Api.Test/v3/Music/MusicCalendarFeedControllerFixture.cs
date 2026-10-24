using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Music;
using NzbDrone.Core.Tags;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Music.Calendar;

namespace NzbDrone.Api.Test.v3.Music
{
    [TestFixture]
    public class MusicCalendarFeedControllerFixture : TestBase<MusicCalendarFeedController>
    {
        private Artist _tagged;
        private Artist _untagged;

        [SetUp]
        public void Setup()
        {
            _tagged = NewArtist(1, "The Black Keys", 5);
            _untagged = NewArtist(2, "Other Artist");

            Mocker.GetMock<IArtistService>().Setup(s => s.GetArtist(1)).Returns(_tagged);
            Mocker.GetMock<IArtistService>().Setup(s => s.GetArtist(2)).Returns(_untagged);
            Mocker.GetMock<ITagService>().Setup(s => s.GetTag("rock")).Returns(new Tag { Id = 5, Label = "rock" });

            Mocker.GetMock<IAlbumService>()
                  .Setup(s => s.AlbumsBetweenDates(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
                  .Returns(new List<Album>
                  {
                      NewAlbum(20, 2, "Elsewhere", new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc)),
                      NewAlbum(10, 1, "Brothers", new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc))
                  });
        }

        private static Artist NewArtist(int id, string name, params int[] tags)
        {
            return new Artist
            {
                Id = id,
                Metadata = new ArtistMetadata { Name = name },
                Tags = new HashSet<int>(tags)
            };
        }

        private static Album NewAlbum(int id, int artistId, string title, DateTime release)
        {
            return new Album
            {
                Id = id,
                ArtistId = artistId,
                Title = title,
                Overview = "Overview " + id,
                ReleaseDate = release,
                Genres = new List<string> { "Rock" }
            };
        }

        private string Feed(string tags = "")
        {
            var result = Subject.GetCalendarFeed(tags: tags);

            result.Should().BeOfType<ContentResult>().Which.ContentType.Should().Be("text/calendar");

            return ((ContentResult)result).Content;
        }

        [Test]
        public void should_write_each_album_as_an_all_day_event_on_its_release_date()
        {
            var feed = Feed();

            feed.Should().Contain("PRODID:-//lidarr.audio//Lidarr//EN");
            feed.Should().Contain("UID:Lidarr_album_10");
            feed.Should().Contain("SUMMARY:The Black Keys - Brothers");
            feed.Should().Contain("DTSTART;VALUE=DATE:20261101");
            feed.Should().Contain("DTSTART;VALUE=DATE:20261105");
        }

        [Test]
        public void should_list_events_in_release_order()
        {
            var feed = Feed();

            feed.IndexOf("Lidarr_album_10", StringComparison.Ordinal).Should().BeLessThan(feed.IndexOf("Lidarr_album_20", StringComparison.Ordinal));
        }

        [Test]
        public void should_keep_only_albums_of_artists_with_a_requested_tag()
        {
            var feed = Feed("rock");

            feed.Should().Contain("Lidarr_album_10");
            feed.Should().NotContain("Lidarr_album_20");
        }
    }
}
