using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.ArtistStats;
using NzbDrone.Core.Tags;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Music.Calendar;

namespace NzbDrone.Api.Test.v3.Music
{
    [TestFixture]
    public class MusicCalendarControllerFixture : TestBase<MusicCalendarController>
    {
        private Artist _tagged;
        private Artist _untagged;

        [SetUp]
        public void Setup()
        {
            _tagged = NewArtist(1, 5);
            _untagged = NewArtist(2);

            Mocker.GetMock<IArtistService>().Setup(s => s.GetAllArtists()).Returns(new List<Artist> { _tagged, _untagged });
            Mocker.GetMock<IArtistStatisticsService>().Setup(s => s.ArtistStatistics()).Returns(new List<ArtistStatistics>());
            Mocker.GetMock<ITagService>().Setup(s => s.GetTag("rock")).Returns(new Tag { Id = 5, Label = "rock" });

            Mocker.GetMock<IAlbumService>()
                  .Setup(s => s.AlbumsBetweenDates(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
                  .Returns(new List<Album>
                  {
                      NewAlbum(30, 2, new DateTime(2026, 11, 3)),
                      NewAlbum(10, 1, new DateTime(2026, 11, 1)),
                      NewAlbum(20, 1, new DateTime(2026, 11, 2)),
                      NewAlbum(99, 404, new DateTime(2026, 11, 2))
                  });
        }

        private static Artist NewArtist(int id, params int[] tags)
        {
            return new Artist
            {
                Id = id,
                Metadata = new ArtistMetadata { Name = "Artist " + id, Images = new List<NzbDrone.Core.MediaCover.MediaCover>() },
                Tags = new HashSet<int>(tags)
            };
        }

        private Album NewAlbum(int id, int artistId, DateTime release)
        {
            var artist = artistId == 1 ? _tagged : _untagged;

            return new Album
            {
                Id = id,
                ArtistId = artistId,
                Title = "Album " + id,
                ReleaseDate = release,
                Artist = new NzbDrone.Core.Datastore.LazyLoaded<Artist>(artistId == 404 ? null : artist),
                Images = new List<NzbDrone.Core.MediaCover.MediaCover>(),
                SecondaryTypes = new List<SecondaryAlbumType>(),
                Genres = new List<string>()
            };
        }

        [Test]
        public void should_return_albums_in_release_order_and_skip_albums_of_unknown_artists()
        {
            var result = Subject.GetCalendar(null, null);

            result.Select(r => r.Id).Should().Equal(10, 20, 30);
        }

        [Test]
        public void should_keep_only_albums_of_artists_with_a_requested_tag()
        {
            var result = Subject.GetCalendar(null, null, tags: "rock");

            result.Select(r => r.Id).Should().Equal(10, 20);
        }

        [Test]
        public void should_pass_the_range_and_the_unmonitored_flag_to_the_album_query()
        {
            var start = new DateTime(2026, 11, 1);
            var end = new DateTime(2026, 11, 30);

            Subject.GetCalendar(start, end, unmonitored: true);

            Mocker.GetMock<IAlbumService>().Verify(s => s.AlbumsBetweenDates(start, end, true), Times.Once());
        }

        [Test]
        public void should_include_the_artist_when_asked()
        {
            Subject.GetCalendar(null, null, includeArtist: true).Should().OnlyContain(r => r.Artist != null);
        }
    }
}
