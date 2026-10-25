using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Queue;
using Radarr.Http;
using Radarr.Http.Middleware;
using MovieBlocklist = NzbDrone.Core.Movies.Blocklisting;
using MusicBlocklist = NzbDrone.Core.Music.Blocklisting;
using TvBlocklist = NzbDrone.Core.Tv.Blocklisting;

namespace NzbDrone.Api.Test.v3.Queue
{
    [TestFixture]
    public class QueueControllerMusicFixture : TestBase<QueueController>
    {
        private NzbDrone.Core.Queue.Queue _movie;
        private NzbDrone.Core.Queue.Queue _tv;
        private NzbDrone.Core.Queue.Queue _blackKeys;
        private NzbDrone.Core.Queue.Queue _abba;
        private NzbDrone.Core.Queue.Queue _carpenters;
        private NzbDrone.Core.Queue.Queue _unmatched;

        private static Album NewAlbum(int id, string title)
        {
            return new Album { Id = id, Title = title };
        }

        private static NzbDrone.Core.Queue.Queue NewMusicItem(int id, string title, Artist artist, params Album[] albums)
        {
            return new NzbDrone.Core.Queue.Queue
            {
                Id = id,
                Title = title,
                Quality = new QualityModel(Quality.Unknown),
                Languages = new List<NzbDrone.Core.Languages.Language>(),
                RemoteRelease = new RemoteAlbum { Artist = artist, Albums = albums.ToList() }
            };
        }

        [SetUp]
        public void Setup()
        {
            _movie = new NzbDrone.Core.Queue.Queue
            {
                Id = 1,
                Title = "Movie.2020",
                Quality = new QualityModel(Quality.WEBDL1080p),
                Languages = new List<NzbDrone.Core.Languages.Language>(),
                RemoteRelease = new RemoteMovie { Movie = new Movie { Id = 8 } }
            };

            _tv = new NzbDrone.Core.Queue.Queue
            {
                Id = 2,
                Title = "Series.S01E01",
                Quality = new QualityModel(Quality.WEBDL1080p),
                Languages = new List<NzbDrone.Core.Languages.Language>(),
                RemoteRelease = new RemoteEpisode { Series = new Series { Id = 3 }, Episodes = new List<Episode> { new Episode { Id = 4 } } }
            };

            _blackKeys = NewMusicItem(3, "The Black Keys - Brothers", new Artist { Id = 12, SortName = "black keys" }, NewAlbum(101, "Brothers"), NewAlbum(102, "El Camino"));

            // Title, artist sort name and album title each give a different order:
            // by title Carpenters, Black Keys, ABBA; by artist ABBA, Black Keys, Carpenters; by album Carpenters, ABBA, Black Keys.
            _abba = NewMusicItem(4, "Zzz ABBA - Arrival", new Artist { Id = 13, SortName = "abba" }, NewAlbum(201, "Arrival"));
            _carpenters = NewMusicItem(6, "Carpenters - Aerial", new Artist { Id = 14, SortName = "carpenters" }, NewAlbum(301, "Aerial"));
            _unmatched = new NzbDrone.Core.Queue.Queue
            {
                Id = 5,
                Title = "Nothing parsed",
                Quality = new QualityModel(Quality.Unknown),
                Languages = new List<NzbDrone.Core.Languages.Language>(),
                RemoteRelease = new RemoteAlbum()
            };

            // The controller builds a default quality profile for sorting; it reads the custom formats.
            Mocker.GetMock<ICustomFormatService>().Setup(s => s.All()).Returns(new List<CustomFormat>());

            Mocker.GetMock<IQueueService>()
                  .Setup(s => s.GetQueue())
                  .Returns(new List<NzbDrone.Core.Queue.Queue> { _movie, _tv, _blackKeys, _abba, _carpenters, _unmatched });

            Mocker.GetMock<IPendingQueueService>()
                  .Setup(s => s.GetPendingQueue())
                  .Returns(new List<NzbDrone.Core.Queue.Queue>());
        }

        private List<int> Ids(PagingRequestResource paging, int[] artistIds = null, int[] albumIds = null, bool includeUnknown = false)
        {
            return Subject.GetQueue(paging, includeUnknownMovieItems: includeUnknown, artistIds: artistIds, albumIds: albumIds)
                          .Value.Records.Select(r => r.Id).ToList();
        }

        private static PagingRequestResource Paging(string sortKey = "title", SortDirection direction = SortDirection.Ascending)
        {
            return new PagingRequestResource { Page = 1, PageSize = 20, SortKey = sortKey, SortDirection = direction };
        }

        [Test]
        public void should_list_a_music_download_with_the_matched_items_of_every_kind()
        {
            Ids(Paging()).Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 6 });
        }

        [Test]
        public void should_hide_a_music_download_that_matched_no_artist_until_unknown_items_are_asked_for()
        {
            Ids(Paging()).Should().NotContain(5);
            Ids(Paging(), includeUnknown: true).Should().Contain(5);
        }

        [Test]
        public void should_filter_by_artist_ids()
        {
            Ids(Paging(), artistIds: new[] { 12 }).Should().BeEquivalentTo(new[] { 3 });
            Ids(Paging(), artistIds: new[] { 12, 13 }).Should().BeEquivalentTo(new[] { 3, 4 });
            Ids(Paging(), artistIds: new[] { 99 }).Should().BeEmpty();
        }

        [Test]
        public void should_filter_by_any_album_of_a_download()
        {
            Ids(Paging(), albumIds: new[] { 102 }).Should().BeEquivalentTo(new[] { 3 });
            Ids(Paging(), albumIds: new[] { 201, 101 }).Should().BeEquivalentTo(new[] { 3, 4 });
            Ids(Paging(), albumIds: new[] { 999 }).Should().BeEmpty();
        }

        [Test]
        public void should_require_both_artist_and_album_filters_to_match()
        {
            Ids(Paging(), artistIds: new[] { 12 }, albumIds: new[] { 201 }).Should().BeEmpty();
            Ids(Paging(), artistIds: new[] { 12 }, albumIds: new[] { 101 }).Should().BeEquivalentTo(new[] { 3 });
        }

        [Test]
        public void should_sort_by_artist_sort_name()
        {
            var artists = new[] { 12, 13, 14 };

            Ids(Paging("artists.sortName"), artistIds: artists).Should().Equal(4, 3, 6);
            Ids(Paging("artists.sortName", SortDirection.Descending), artistIds: artists).Should().Equal(6, 3, 4);
        }

        [Test]
        public void should_sort_by_album_title()
        {
            var artists = new[] { 12, 13, 14 };

            Ids(Paging("albums.title"), artistIds: artists).Should().Equal(6, 4, 3);
            Ids(Paging("albums.title", SortDirection.Descending), artistIds: artists).Should().Equal(3, 4, 6);
            Ids(Paging("title"), artistIds: artists).Should().Equal(6, 3, 4);
        }

        [Test]
        public void should_include_the_artist_and_album_resources_when_asked()
        {
            var records = Subject.GetQueue(Paging(), includeArtist: true, includeAlbum: true, artistIds: new[] { 13 }).Value.Records;

            records.Should().ContainSingle();
            records[0].ArtistId.Should().Be(13);
            records[0].AlbumId.Should().Be(201);
        }

        [Test]
        public void should_blocklist_a_pending_music_release_with_the_music_blocklist_only()
        {
            Mocker.GetMock<IPendingQueueService>().Setup(s => s.FindPendingQueueItem(4)).Returns(_abba);

            Subject.RemoveAction(4, blocklist: true);

            Mocker.GetMock<MusicBlocklist.IBlocklistService>()
                  .Verify(s => s.Block((RemoteAlbum)_abba.RemoteRelease, It.IsAny<string>()), Times.Once());
            Mocker.GetMock<MovieBlocklist.IBlocklistService>()
                  .Verify(s => s.Block(It.IsAny<RemoteMovie>(), It.IsAny<string>()), Times.Never());
            Mocker.GetMock<TvBlocklist.IBlocklistService>()
                  .Verify(s => s.Block(It.IsAny<RemoteEpisode>(), It.IsAny<string>()), Times.Never());
            Mocker.GetMock<IPendingQueueService>()
                  .Verify(s => s.RemovePendingQueueItem(_abba), Times.Once());
        }

        [Test]
        public void should_not_blocklist_a_pending_music_release_unless_asked()
        {
            Mocker.GetMock<IPendingQueueService>().Setup(s => s.FindPendingQueueItem(4)).Returns(_abba);

            Subject.RemoveAction(4);

            Mocker.GetMock<MusicBlocklist.IBlocklistService>()
                  .Verify(s => s.Block(It.IsAny<RemoteAlbum>(), It.IsAny<string>()), Times.Never());
            Mocker.GetMock<IPendingQueueService>()
                  .Verify(s => s.RemovePendingQueueItem(_abba), Times.Once());
        }

        [Test]
        public void should_still_blocklist_a_pending_movie_with_the_movie_blocklist()
        {
            Mocker.GetMock<IPendingQueueService>().Setup(s => s.FindPendingQueueItem(1)).Returns(_movie);

            Subject.RemoveAction(1, blocklist: true);

            Mocker.GetMock<MovieBlocklist.IBlocklistService>()
                  .Verify(s => s.Block((RemoteMovie)_movie.RemoteRelease, It.IsAny<string>()), Times.Once());
            Mocker.GetMock<MusicBlocklist.IBlocklistService>()
                  .Verify(s => s.Block(It.IsAny<RemoteAlbum>(), It.IsAny<string>()), Times.Never());
        }

        [TestCase(SeerrCompatibilityMiddleware.RadarrApplication, 1)]
        [TestCase(SeerrCompatibilityMiddleware.SonarrApplication, 2)]
        public void should_filter_the_Seerr_queue_to_the_requested_application(
            string application,
            int expectedId)
        {
            var context = new DefaultHttpContext();
            context.Items[SeerrCompatibilityMiddleware.ApplicationItemKey] = application;
            Subject.ControllerContext = new ControllerContext { HttpContext = context };

            Subject.GetQueue(Paging()).Value.Records.Select(r => r.Id).Should().Equal(expectedId);
        }
    }

    [TestFixture]
    public class QueueDetailsControllerMusicFixture : TestBase<QueueDetailsController>
    {
        private NzbDrone.Core.Queue.Queue _movie;
        private NzbDrone.Core.Queue.Queue _blackKeys;
        private NzbDrone.Core.Queue.Queue _abba;

        [SetUp]
        public void Setup()
        {
            _movie = new NzbDrone.Core.Queue.Queue
            {
                Id = 1,
                Title = "Movie.2020",
                Quality = new QualityModel(Quality.WEBDL1080p),
                Languages = new List<NzbDrone.Core.Languages.Language>(),
                RemoteRelease = new RemoteMovie { Movie = new Movie { Id = 8 } }
            };

            _blackKeys = new NzbDrone.Core.Queue.Queue
            {
                Id = 3,
                Quality = new QualityModel(Quality.Unknown),
                Languages = new List<NzbDrone.Core.Languages.Language>(),
                RemoteRelease = new RemoteAlbum { Artist = new Artist { Id = 12 }, Albums = new List<Album> { new Album { Id = 101 }, new Album { Id = 102 } } }
            };

            _abba = new NzbDrone.Core.Queue.Queue
            {
                Id = 4,
                Quality = new QualityModel(Quality.Unknown),
                Languages = new List<NzbDrone.Core.Languages.Language>(),
                RemoteRelease = new RemoteAlbum
                {
                    Artist = new Artist
                    {
                        Id = 13,
                        Metadata = new ArtistMetadata { Name = "ABBA", Images = new List<NzbDrone.Core.MediaCover.MediaCover>() },
                        Tags = new HashSet<int>()
                    },
                    Albums = new List<Album> { new Album { Id = 201, Title = "Arrival", Images = new List<NzbDrone.Core.MediaCover.MediaCover>(), SecondaryTypes = new List<SecondaryAlbumType>(), Genres = new List<string>() } }
                }
            };

            Mocker.GetMock<IQueueService>()
                  .Setup(s => s.GetQueue())
                  .Returns(new List<NzbDrone.Core.Queue.Queue> { _movie, _blackKeys });

            // A pending release is part of the details too.
            Mocker.GetMock<IPendingQueueService>()
                  .Setup(s => s.GetPendingQueue())
                  .Returns(new List<NzbDrone.Core.Queue.Queue> { _abba });
        }

        [Test]
        public void should_list_the_downloads_of_an_artist_including_pending_ones()
        {
            Subject.GetQueue(null, null, 13, null).Value.Select(r => r.Id).Should().Equal(4);
            Subject.GetQueue(null, null, 12, null).Value.Select(r => r.Id).Should().Equal(3);
        }

        [Test]
        public void should_include_the_artist_and_album_only_when_asked()
        {
            var plain = Subject.GetQueue(null, null, 13, null).Value.Single();
            plain.Artist.Should().BeNull();
            plain.Album.Should().BeNull();

            var byArtist = Subject.GetQueue(null, null, 13, null, includeArtist: true, includeAlbum: true).Value.Single();
            byArtist.Artist.ArtistName.Should().Be("ABBA");
            byArtist.Album.Title.Should().Be("Arrival");

            var byAlbum = Subject.GetQueue(null, null, null, new[] { 201 }, includeArtist: true, includeAlbum: true).Value.Single();
            byAlbum.Artist.ArtistName.Should().Be("ABBA");
            byAlbum.Album.Title.Should().Be("Arrival");
        }

        [Test]
        public void should_list_the_downloads_of_any_of_the_albums()
        {
            Subject.GetQueue(null, null, null, new[] { 102, 201 }).Value.Select(r => r.Id).Should().BeEquivalentTo(new[] { 3, 4 });
            Subject.GetQueue(null, null, null, new[] { 999 }).Value.Should().BeEmpty();
        }

        [Test]
        public void should_list_everything_without_a_filter_and_keep_the_movie_filter()
        {
            Subject.GetQueue(null, null, null, null).Value.Should().HaveCount(3);
            Subject.GetQueue(8, null, null, null).Value.Select(r => r.Id).Should().Equal(1);
        }
    }
}
