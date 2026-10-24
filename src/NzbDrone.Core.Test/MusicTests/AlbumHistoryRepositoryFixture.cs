using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.History;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using MovieHistory = NzbDrone.Core.Movies.History.MovieHistory;
using MovieHistoryRepository = NzbDrone.Core.Movies.History.HistoryRepository;
using TvHistoryRepository = NzbDrone.Core.Tv.History.HistoryRepository;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class AlbumHistoryRepositoryFixture : DbTest<AlbumHistoryRepository, AlbumHistory>
    {
        private Artist _artist;
        private Artist _other;
        private Album _album1;
        private Album _album2;
        private Album _otherAlbum;

        [SetUp]
        public void Setup()
        {
            _artist = AddArtist("The Black Keys", "artist-1");
            _other = AddArtist("Alien Ant Farm", "artist-2");

            _album1 = AddAlbum(_artist, "Brothers", "album-1");
            _album2 = AddAlbum(_artist, "El Camino", "album-2");
            _otherAlbum = AddAlbum(_other, "ANThology", "album-3");
        }

        private Artist AddArtist(string name, string foreignId)
        {
            var metadata = MusicTestData.NewMetadata(name, foreignId);
            metadata.OldForeignArtistIds = new List<string>();
            Mocker.Resolve<ArtistMetadataRepository>().Insert(metadata);

            var artist = MusicTestData.NewArtist(metadata.Id, name: name);
            artist.Path = "/data/music/" + foreignId;
            Mocker.Resolve<ArtistRepository>().Insert(artist);

            return artist;
        }

        private Album AddAlbum(Artist artist, string title, string foreignId)
        {
            var album = MusicTestData.NewAlbum(artist.ArtistMetadataId, title, foreignId);
            Mocker.Resolve<AlbumRepository>().Insert(album);

            return album;
        }

        private static AlbumHistory NewHistory(Artist artist, Album album, AlbumHistoryEventType type = AlbumHistoryEventType.Grabbed, DateTime? date = null, string downloadId = null, AudioQuality quality = null)
        {
            return new AlbumHistory
            {
                MediaKind = MediaKind.Music,
                ParentId = artist.Id,
                ItemId = album.Id,
                SourceTitle = "Artist-Album-2010-FLAC-GRP",
                Quality = new AudioQualityModel(quality ?? AudioQuality.FLAC),
                Date = date ?? DateTime.UtcNow,
                EventType = type,
                DownloadId = downloadId,
                Data = new Dictionary<string, string> { { "indexer", "idx" }, { "releaseGroup", "GRP" } }
            };
        }

        [Test]
        public void should_round_trip_a_history_row_with_artist_and_album_ids()
        {
            var history = NewHistory(_artist, _album1, AlbumHistoryEventType.DownloadImported, downloadId: "dl-1");

            Subject.Insert(history);

            var stored = Subject.Get(history.Id);

            stored.MediaKind.Should().Be(MediaKind.Music);
            stored.ParentId.Should().Be(_artist.Id);
            stored.ItemId.Should().Be(_album1.Id);
            stored.EventType.Should().Be(AlbumHistoryEventType.DownloadImported);
            stored.DownloadId.Should().Be("dl-1");
            stored.Data.Should().Contain("indexer", "idx").And.Contain("releaseGroup", "GRP");
            stored.Quality.Quality.Should().Be(AudioQuality.FLAC);
        }

        [Test]
        public void should_store_lidarr_event_type_values()
        {
            ((int)AlbumHistoryEventType.Grabbed).Should().Be(1);
            ((int)AlbumHistoryEventType.TrackFileImported).Should().Be(3);
            ((int)AlbumHistoryEventType.DownloadFailed).Should().Be(4);
            ((int)AlbumHistoryEventType.DownloadImported).Should().Be(8);
            ((int)AlbumHistoryEventType.DownloadIgnored).Should().Be(10);
        }

        [Test]
        public void should_refuse_a_row_of_another_media_kind()
        {
            var history = NewHistory(_artist, _album1);
            history.MediaKind = MediaKind.Movies;

            var action = () => Subject.Insert(history);

            action.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void should_not_see_movie_rows_that_share_a_parent_id_and_ids_stay_unique()
        {
            var movieRepository = Mocker.Resolve<MovieHistoryRepository>();

            var movieRow = new MovieHistory
            {
                MediaKind = MediaKind.Movies,
                ParentId = _artist.Id,
                SourceTitle = "Movie.2024",
                Quality = new QualityModel(Quality.Bluray1080p),
                Date = DateTime.UtcNow,
                EventType = Core.Movies.History.MovieHistoryEventType.Grabbed,
                DownloadId = "shared-download-id",
                Languages = new List<Language> { Language.English }
            };

            movieRepository.Insert(movieRow);

            var musicRow = NewHistory(_artist, _album1, downloadId: "shared-download-id");
            Subject.Insert(musicRow);

            musicRow.Id.Should().NotBe(movieRow.Id);
            Subject.All().Should().ContainSingle().Which.Id.Should().Be(musicRow.Id);
            Subject.Count().Should().Be(1);
            Subject.FindByDownloadId("shared-download-id").Should().ContainSingle().Which.Id.Should().Be(musicRow.Id);
            Subject.MostRecentForDownloadId("shared-download-id").Id.Should().Be(musicRow.Id);
            Subject.GetByArtist(_artist.Id, null).Should().ContainSingle();
            Subject.Since(DateTime.UtcNow.AddDays(-1), null).Should().ContainSingle();

            movieRepository.All().Should().ContainSingle().Which.Id.Should().Be(movieRow.Id);
        }

        [Test]
        public void should_not_see_or_delete_tv_rows_and_leave_music_rows_alone_when_tv_purges()
        {
            Subject.Insert(NewHistory(_artist, _album1));
            Mocker.Resolve<TvHistoryRepository>().Purge();

            Subject.All().Should().ContainSingle();

            Subject.Purge();
            Subject.All().Should().BeEmpty();
        }

        [Test]
        public void should_not_delete_other_kinds_when_deleting_for_an_artist()
        {
            var movieRepository = Mocker.Resolve<MovieHistoryRepository>();

            movieRepository.Insert(new MovieHistory
            {
                MediaKind = MediaKind.Movies,
                ParentId = _artist.Id,
                SourceTitle = "Movie.2024",
                Quality = new QualityModel(),
                Date = DateTime.UtcNow,
                Languages = new List<Language>()
            });
            Subject.Insert(NewHistory(_artist, _album1));
            Subject.Insert(NewHistory(_other, _otherAlbum));

            Subject.DeleteForArtists(new List<int> { _artist.Id });

            Subject.All().Single().ParentId.Should().Be(_other.Id);
            movieRepository.All().Should().HaveCount(1);
        }

        [Test]
        public void should_find_history_by_album_newest_first()
        {
            var old = NewHistory(_artist, _album1, date: DateTime.UtcNow.AddDays(-2));
            var recent = NewHistory(_artist, _album1, date: DateTime.UtcNow.AddDays(-1));
            Subject.Insert(old);
            Subject.Insert(recent);
            Subject.Insert(NewHistory(_artist, _album2));

            Subject.FindByAlbumId(_album1.Id).Select(h => h.Id).Should().Equal(recent.Id, old.Id);
            Subject.MostRecentForAlbum(_album1.Id).Id.Should().Be(recent.Id);
            Subject.MostRecentForAlbum(99999).Should().BeNull();
        }

        [Test]
        public void should_join_the_artist_and_album_for_artist_and_album_queries()
        {
            Subject.Insert(NewHistory(_artist, _album1, AlbumHistoryEventType.Grabbed));
            Subject.Insert(NewHistory(_artist, _album2, AlbumHistoryEventType.DownloadFailed));
            Subject.Insert(NewHistory(_other, _otherAlbum));

            var byArtist = Subject.GetByArtist(_artist.Id, null);
            byArtist.Should().HaveCount(2);
            byArtist.Should().OnlyContain(h => h.Artist.Id == _artist.Id && h.Album.ArtistMetadataId == _artist.ArtistMetadataId);

            Subject.GetByArtist(_artist.Id, AlbumHistoryEventType.DownloadFailed).Single().Album.Id.Should().Be(_album2.Id);
            Subject.GetByAlbum(_album1.Id, null).Single().Artist.Metadata.Value.Name.Should().Be("The Black Keys");
            Subject.GetByAlbum(_album1.Id, AlbumHistoryEventType.DownloadFailed).Should().BeEmpty();
        }

        [Test]
        public void should_return_history_since_a_date_oldest_first()
        {
            var old = NewHistory(_artist, _album1, date: DateTime.UtcNow.AddDays(-10));
            var middle = NewHistory(_artist, _album1, AlbumHistoryEventType.DownloadFailed, DateTime.UtcNow.AddDays(-2));
            var recent = NewHistory(_artist, _album2, date: DateTime.UtcNow.AddDays(-1));
            Subject.Insert(old);
            Subject.Insert(recent);
            Subject.Insert(middle);

            Subject.Since(DateTime.UtcNow.AddDays(-5), null).Select(h => h.Id).Should().Equal(middle.Id, recent.Id);
            Subject.Since(DateTime.UtcNow.AddDays(-5), AlbumHistoryEventType.DownloadFailed).Select(h => h.Id).Should().Equal(middle.Id);
            Subject.Since(DateTime.UtcNow.AddDays(-5), null).First().Artist.Should().NotBeNull();
        }

        [Test]
        public void should_find_download_history_by_quality_for_the_artist()
        {
            Subject.Insert(NewHistory(_artist, _album1, AlbumHistoryEventType.Grabbed, quality: AudioQuality.FLAC));
            Subject.Insert(NewHistory(_artist, _album1, AlbumHistoryEventType.Grabbed, quality: AudioQuality.MP3_320));
            Subject.Insert(NewHistory(_artist, _album1, AlbumHistoryEventType.TrackFileDeleted, quality: AudioQuality.FLAC));
            Subject.Insert(NewHistory(_other, _otherAlbum, AlbumHistoryEventType.Grabbed, quality: AudioQuality.FLAC));

            var found = Subject.FindDownloadHistory(_artist.Id, new AudioQualityModel(AudioQuality.FLAC));

            found.Should().ContainSingle().Which.EventType.Should().Be(AlbumHistoryEventType.Grabbed);
        }
    }
}
