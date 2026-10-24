using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Music;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Music.Wanted;
using Radarr.Http;

namespace NzbDrone.Api.Test.v3.Music
{
    [TestFixture]
    public class MusicMissingControllerFixture : TestBase<MusicMissingController>
    {
        private PagingSpec<Album> _spec;

        [SetUp]
        public void Setup()
        {
            _spec = null;

            Mocker.GetMock<IAlbumService>()
                  .Setup(s => s.AlbumsWithoutFiles(It.IsAny<PagingSpec<Album>>()))
                  .Returns<PagingSpec<Album>>(spec =>
                  {
                      _spec = spec;
                      spec.Records = new List<Album> { WantedFixtures.NewAlbum(1, true, true) };
                      spec.TotalRecords = 1;

                      return spec;
                  });
        }

        [Test]
        public void should_default_to_newest_release_first_and_ignore_a_sort_key_that_is_not_allowed()
        {
            Subject.GetMissingAlbums(new PagingRequestResource { SortKey = "overview" });

            _spec.SortKey.Should().Be("albums.releaseDate");
            _spec.SortDirection.Should().Be(SortDirection.Descending);
        }

        [TestCase("albums.title")]
        [TestCase("albums.lastSearchTime")]
        [TestCase("artists.sortName")]
        public void should_allow_the_sort_keys_of_the_wanted_table(string sortKey)
        {
            Subject.GetMissingAlbums(new PagingRequestResource { SortKey = sortKey, SortDirection = SortDirection.Ascending });

            _spec.SortKey.Should().Be(sortKey);
            _spec.SortDirection.Should().Be(SortDirection.Ascending);
        }

        [Test]
        public void should_filter_monitored_albums_of_monitored_artists_by_default()
        {
            Subject.GetMissingAlbums(new PagingRequestResource());

            var filter = _spec.FilterExpressions.Single().Compile();

            filter(WantedFixtures.NewAlbum(1, true, true)).Should().BeTrue();
            filter(WantedFixtures.NewAlbum(1, true, false)).Should().BeFalse();
            filter(WantedFixtures.NewAlbum(1, false, true)).Should().BeFalse();
        }

        [Test]
        public void should_filter_unmonitored_albums_or_artists_when_asked()
        {
            Subject.GetMissingAlbums(new PagingRequestResource(), monitored: false);

            var filter = _spec.FilterExpressions.Single().Compile();

            filter(WantedFixtures.NewAlbum(1, true, true)).Should().BeFalse();
            filter(WantedFixtures.NewAlbum(1, true, false)).Should().BeTrue();
            filter(WantedFixtures.NewAlbum(1, false, true)).Should().BeTrue();
        }

        [Test]
        public void should_include_the_artist_when_asked()
        {
            Subject.GetMissingAlbums(new PagingRequestResource(), includeArtist: true).Records.Single().Artist.ArtistName.Should().Be("The Black Keys");
        }

        [Test]
        public void should_not_serve_a_single_album_by_id()
        {
            Action action = () => Subject.GetResourceByIdWithErrorHandler(1);

            action.Should().Throw<NotImplementedException>();
        }
    }

    [TestFixture]
    public class MusicCutoffControllerFixture : TestBase<MusicCutoffController>
    {
        private PagingSpec<Album> _spec;

        [SetUp]
        public void Setup()
        {
            _spec = null;

            Mocker.GetMock<IAlbumCutoffService>()
                  .Setup(s => s.AlbumsWhereCutoffUnmet(It.IsAny<PagingSpec<Album>>()))
                  .Returns<PagingSpec<Album>>(spec =>
                  {
                      _spec = spec;
                      spec.Records = new List<Album> { WantedFixtures.NewAlbum(1, true, true) };
                      spec.TotalRecords = 1;

                      return spec;
                  });
        }

        [Test]
        public void should_default_to_newest_release_first_and_ignore_a_sort_key_that_is_not_allowed()
        {
            Subject.GetCutoffUnmetAlbums(new PagingRequestResource { SortKey = "overview" });

            _spec.SortKey.Should().Be("albums.releaseDate");
            _spec.SortDirection.Should().Be(SortDirection.Descending);
        }

        [Test]
        public void should_filter_monitored_albums_of_monitored_artists_by_default()
        {
            Subject.GetCutoffUnmetAlbums(new PagingRequestResource());

            var filter = _spec.FilterExpressions.Single().Compile();

            filter(WantedFixtures.NewAlbum(1, true, true)).Should().BeTrue();
            filter(WantedFixtures.NewAlbum(1, true, false)).Should().BeFalse();
            filter(WantedFixtures.NewAlbum(1, false, true)).Should().BeFalse();
        }

        [Test]
        public void should_filter_unmonitored_albums_or_artists_when_asked()
        {
            Subject.GetCutoffUnmetAlbums(new PagingRequestResource(), monitored: false);

            var filter = _spec.FilterExpressions.Single().Compile();

            filter(WantedFixtures.NewAlbum(1, true, true)).Should().BeFalse();
            filter(WantedFixtures.NewAlbum(1, true, false)).Should().BeTrue();
            filter(WantedFixtures.NewAlbum(1, false, true)).Should().BeTrue();
        }
    }

    internal static class WantedFixtures
    {
        public static Album NewAlbum(int id, bool albumMonitored, bool artistMonitored)
        {
            var artist = new Artist
            {
                Id = 12,
                Monitored = artistMonitored,
                Metadata = new ArtistMetadata { Name = "The Black Keys", Images = new List<NzbDrone.Core.MediaCover.MediaCover>() },
                Tags = new HashSet<int>()
            };

            return new Album
            {
                Id = id,
                Title = "Album " + id,
                Monitored = albumMonitored,
                Artist = new NzbDrone.Core.Datastore.LazyLoaded<Artist>(artist),
                Images = new List<NzbDrone.Core.MediaCover.MediaCover>(),
                SecondaryTypes = new List<SecondaryAlbumType>(),
                Genres = new List<string>()
            };
        }
    }
}
