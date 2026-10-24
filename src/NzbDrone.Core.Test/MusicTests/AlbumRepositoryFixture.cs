using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using FluentAssertions.Equivalency;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class AlbumRepositoryFixture : DbTest<AlbumRepository, Album>
    {
        private Artist _artist;
        private Album _album;
        private AlbumRelease _release;
        private ReleaseRepository _releaseRepo;
        private List<Album> _albums;

        [SetUp]
        public void Setup()
        {
            var metadata = MusicTestData.NewMetadata("Alien Ant Farm", "this is a fake id");
            Mocker.Resolve<ArtistMetadataRepository>().Insert(metadata);

            _artist = MusicTestData.NewArtist(metadata.Id, name: "Alien Ant Farm");
            Mocker.Resolve<ArtistRepository>().Insert(_artist);

            _releaseRepo = Mocker.Resolve<ReleaseRepository>();

            _album = MusicTestData.NewAlbum(_artist.ArtistMetadataId, "ANThology", "1");
            Subject.Insert(_album);

            _release = MusicTestData.NewRelease(_album.Id, "e00e40a3-5ed5-4ed3-9c22-0a8ff4119bdf");
            _releaseRepo.Insert(_release);
        }

        [Test]
        public void should_round_trip_every_column()
        {
            var stored = Subject.Get(_album.Id);

            stored.Should().BeEquivalentTo(_album, o => o.WithDateTolerance().Excluding(a => a.Artist).Excluding(a => a.ArtistMetadata).Excluding(a => a.AlbumReleases).Excluding(a => a.ArtistId));
            stored.SecondaryTypes.Should().Equal(SecondaryAlbumType.Studio, SecondaryAlbumType.Live);
            stored.AddOptions.AddType.Should().Be(AlbumAddType.Manual);
        }

        [Test]
        public void should_lazyload_releases_and_artist_metadata()
        {
            var stored = Subject.Get(_album.Id);

            stored.AlbumReleases.Value.Should().ContainSingle().Which.ForeignReleaseId.Should().Be(_release.ForeignReleaseId);
            stored.ArtistMetadata.Value.Name.Should().Be("Alien Ant Farm");
            stored.Artist.Value.Id.Should().Be(_artist.Id);
        }

        [Test]
        public void should_find_album_in_db_by_foreign_id()
        {
            Subject.FindById("1").Title.Should().Be("ANThology");
            Subject.FindById("nope").Should().BeNull();
        }

        [Test]
        public void should_find_album_in_db_by_releaseid()
        {
            var album = Subject.FindAlbumByRelease("e00e40a3-5ed5-4ed3-9c22-0a8ff4119bdf");

            album.Should().NotBeNull();
            album.Title.Should().Be(_album.Title);
        }

        [Test]
        public void should_not_find_album_in_db_by_partial_releaseid()
        {
            Subject.FindAlbumByRelease("e00e40a3-5ed5-4ed3-9c22").Should().BeNull();
        }

        private void GivenMultipleAlbums()
        {
            _albums = Builder<Album>.CreateListOfSize(4)
                .All()
                .With(x => x.Id = 0)
                .With(x => x.Artist = _artist)
                .With(x => x.ArtistMetadataId = _artist.ArtistMetadataId)
                .TheFirst(1)

                // next
                .With(x => x.ReleaseDate = DateTime.UtcNow.AddDays(1))
                .TheNext(1)

                // another future one
                .With(x => x.ReleaseDate = DateTime.UtcNow.AddDays(2))
                .TheNext(1)

                // most recent
                .With(x => x.ReleaseDate = DateTime.UtcNow.AddDays(-1))
                .TheNext(1)

                // an older one
                .With(x => x.ReleaseDate = DateTime.UtcNow.AddDays(-2))
                .BuildList();

            Subject.InsertMany(_albums);
        }

        [Test]
        public void get_next_albums_should_return_next_album()
        {
            GivenMultipleAlbums();

            var result = Subject.GetNextAlbums(new[] { _artist.ArtistMetadataId });
            result.Should().BeEquivalentTo(_albums.Skip(1).Take(1), AlbumComparerOptions);
        }

        [Test]
        public void get_last_albums_should_return_last_album()
        {
            GivenMultipleAlbums();

            var result = Subject.GetLastAlbums(new[] { _artist.ArtistMetadataId });
            result.Should().BeEquivalentTo(_albums.Skip(3).Take(1), AlbumComparerOptions);
        }

        [Test]
        public void should_list_the_albums_of_an_artist_and_its_metadata_id()
        {
            Subject.GetAlbums(_artist.Id).Should().ContainSingle().Which.Id.Should().Be(_album.Id);
            Subject.GetAlbumsByArtistMetadataId(_artist.ArtistMetadataId).Should().ContainSingle();
            Subject.GetAlbums(9999).Should().BeEmpty();
        }

        [Test]
        public void should_find_albums_without_files()
        {
            var track = MusicTestData.NewTrack(_release.Id, _artist.ArtistMetadataId);
            Mocker.Resolve<TrackRepository>().Insert(track);
            _album.ReleaseDate = DateTime.UtcNow.AddDays(-5);
            Subject.Update(_album);

            var page = Subject.AlbumsWithoutFiles(new PagingSpec<Album> { Page = 1, PageSize = 10, SortKey = "ReleaseDate", SortDirection = SortDirection.Descending });

            page.TotalRecords.Should().Be(1);
            page.Records.Single().Id.Should().Be(_album.Id);
        }

        [Test]
        public void should_find_albums_whose_file_is_below_the_cutoff()
        {
            var fileRepo = Mocker.Resolve<TrackFileRepository>();
            var file = MusicTestData.NewTrackFile(_album.Id, quality: AudioQuality.MP3_192);
            fileRepo.Insert(file);
            Mocker.Resolve<TrackRepository>().Insert(MusicTestData.NewTrack(_release.Id, _artist.ArtistMetadataId, trackFileId: file.Id));

            var below = new List<QualitiesBelowCutoff> { new QualitiesBelowCutoff(_artist.QualityProfileId, new[] { AudioQuality.MP3_192.Id }) };
            var above = new List<QualitiesBelowCutoff> { new QualitiesBelowCutoff(_artist.QualityProfileId, new[] { AudioQuality.MP3_320.Id }) };
            var spec = new PagingSpec<Album> { Page = 1, PageSize = 10, SortKey = "ReleaseDate", SortDirection = SortDirection.Descending };

            Subject.AlbumsWhereCutoffUnmet(spec, below).Records.Should().ContainSingle().Which.Id.Should().Be(_album.Id);

            var spec2 = new PagingSpec<Album> { Page = 1, PageSize = 10, SortKey = "ReleaseDate", SortDirection = SortDirection.Descending };
            Subject.AlbumsWhereCutoffUnmet(spec2, above).Records.Should().BeEmpty();
        }

        private EquivalencyAssertionOptions<Album> AlbumComparerOptions(EquivalencyAssertionOptions<Album> opts) => opts.ComparingByMembers<Album>()
                .WithDateTolerance()
                .Excluding(ctx => ctx.Type.IsGenericType && ctx.Type.GetGenericTypeDefinition() == typeof(LazyLoaded<>))
                .Excluding(x => x.ArtistId);
    }
}
