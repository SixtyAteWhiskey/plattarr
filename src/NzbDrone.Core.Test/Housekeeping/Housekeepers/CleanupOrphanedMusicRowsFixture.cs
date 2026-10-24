using System.Collections.Generic;
using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Housekeepers;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Test.MusicTests;

namespace NzbDrone.Core.Test.Housekeeping.Housekeepers
{
    // The five orphan housekeepers of the Music tables (Lidarr's CleanupOrphanedAlbums, Releases, Tracks,
    // TrackFiles and ArtistMetadata). One fixture, since they share the same graph of rows.
    [TestFixture]
    public class CleanupOrphanedMusicRowsFixture : DbTest<CleanupOrphanedAlbums, Album>
    {
        private ArtistMetadata _metadata;
        private Artist _artist;
        private Album _album;
        private AlbumRelease _release;

        [SetUp]
        public void Setup()
        {
            // Takes the first id, so that no artist id equals the id of its metadata and a join on the wrong
            // column cannot pass by coincidence.
            Insert("Spare", "mb-spare");

            _metadata = Insert("The Black Keys", "mb-artist");

            _artist = MusicTestData.NewArtist(_metadata.Id, name: "The Black Keys");
            Mocker.Resolve<ArtistRepository>().Insert(_artist);

            _album = MusicTestData.NewAlbum(_metadata.Id, "Brothers", "mb-album");
            Mocker.Resolve<AlbumRepository>().Insert(_album);

            _release = MusicTestData.NewRelease(_album.Id, "mb-release");
            Mocker.Resolve<ReleaseRepository>().Insert(_release);
        }

        private ArtistMetadata Insert(string name, string foreignId)
        {
            var metadata = MusicTestData.NewMetadata(name, foreignId);
            metadata.OldForeignArtistIds = new List<string>();
            Mocker.Resolve<ArtistMetadataRepository>().Insert(metadata);

            return metadata;
        }

        private Track InsertTrack(int releaseId, int metadataId, string foreignId, int trackFileId = 0)
        {
            var track = MusicTestData.NewTrack(releaseId, metadataId, foreignId, trackFileId);
            Mocker.Resolve<TrackRepository>().Insert(track);

            return track;
        }

        private int Count(string table)
        {
            using var mapper = Mocker.Resolve<IMainDatabase>().OpenConnection();

            return mapper.ExecuteScalar<int>($@"SELECT COUNT(*) FROM ""{table}""");
        }

        private T Housekeeper<T>()
            where T : class
        {
            return Mocker.Resolve<T>();
        }

        [Test]
        public void should_delete_albums_whose_artist_is_gone_and_keep_the_rest()
        {
            var gone = Insert("Gone", "mb-gone");
            Mocker.Resolve<AlbumRepository>().Insert(MusicTestData.NewAlbum(gone.Id, "Orphan", "mb-orphan"));

            Subject.Clean();

            Count("Music_Albums").Should().Be(1);
            Mocker.Resolve<AlbumRepository>().All().Should().ContainSingle(a => a.Id == _album.Id);
        }

        [Test]
        public void should_delete_releases_whose_album_is_gone_and_keep_the_rest()
        {
            Mocker.Resolve<ReleaseRepository>().Insert(MusicTestData.NewRelease(_album.Id + 100, "mb-orphan-release"));

            Housekeeper<CleanupOrphanedReleases>().Clean();

            Count("Music_AlbumReleases").Should().Be(1);
            Mocker.Resolve<ReleaseRepository>().All().Should().ContainSingle(r => r.Id == _release.Id);
        }

        [Test]
        public void should_delete_tracks_whose_release_is_gone_and_keep_the_rest()
        {
            var kept = InsertTrack(_release.Id, _metadata.Id, "mb-track-1");
            InsertTrack(_release.Id + 100, _metadata.Id, "mb-track-orphan");

            Housekeeper<CleanupOrphanedTracks>().Clean();

            Count("Music_Tracks").Should().Be(1);
            Mocker.Resolve<TrackRepository>().All().Should().ContainSingle(t => t.Id == kept.Id);
        }

        [Test]
        public void should_delete_metadata_that_nothing_uses()
        {
            var unused = Insert("Unused", "mb-unused");

            Housekeeper<CleanupOrphanedArtistMetadata>().Clean();

            Mocker.Resolve<ArtistMetadataRepository>().All().Should().NotContain(m => m.Id == unused.Id);
            Count("Music_ArtistMetadata").Should().Be(1);
        }

        [Test]
        public void should_keep_metadata_that_only_an_album_uses()
        {
            var albumOnly = Insert("Album only", "mb-album-only");
            Mocker.Resolve<AlbumRepository>().Insert(MusicTestData.NewAlbum(albumOnly.Id, "Guest", "mb-guest"));

            Housekeeper<CleanupOrphanedArtistMetadata>().Clean();

            Mocker.Resolve<ArtistMetadataRepository>().All().Should().Contain(m => m.Id == albumOnly.Id);
        }

        [Test]
        public void should_keep_metadata_that_only_a_track_uses()
        {
            var featured = Insert("Featured", "mb-featured");
            InsertTrack(_release.Id, featured.Id, "mb-featured-track");

            Housekeeper<CleanupOrphanedArtistMetadata>().Clean();

            Mocker.Resolve<ArtistMetadataRepository>().All().Should().Contain(m => m.Id == featured.Id);
        }

        [Test]
        public void should_keep_metadata_that_only_an_artist_uses()
        {
            var second = Insert("Second", "mb-second");
            Mocker.Resolve<ArtistRepository>().Insert(MusicTestData.NewArtist(second.Id, name: "Second"));

            Housekeeper<CleanupOrphanedArtistMetadata>().Clean();

            Mocker.Resolve<ArtistMetadataRepository>().All().Should().Contain(m => m.Id == second.Id);
        }

        [Test]
        public void should_unlink_a_track_file_that_no_track_uses_and_keep_the_row()
        {
            var file = MusicTestData.NewTrackFile(_album.Id);
            Mocker.Resolve<TrackFileRepository>().Insert(file);

            Housekeeper<CleanupOrphanedTrackFiles>().Clean();

            Count("Music_TrackFiles").Should().Be(1);
            Mocker.Resolve<TrackFileRepository>().Get(file.Id).AlbumId.Should().Be(0);
        }

        [Test]
        public void should_keep_the_album_of_a_track_file_a_track_uses()
        {
            var file = MusicTestData.NewTrackFile(_album.Id);
            Mocker.Resolve<TrackFileRepository>().Insert(file);
            var track = InsertTrack(_release.Id, _metadata.Id, "mb-track-1", file.Id);

            Housekeeper<CleanupOrphanedTrackFiles>().Clean();

            Mocker.Resolve<TrackFileRepository>().Get(file.Id).AlbumId.Should().Be(_album.Id);
            Mocker.Resolve<TrackRepository>().Get(track.Id).TrackFileId.Should().Be(file.Id);
        }

        [Test]
        public void should_unlink_a_track_whose_file_is_gone()
        {
            var track = InsertTrack(_release.Id, _metadata.Id, "mb-track-1", 4242);
            var free = InsertTrack(_release.Id, _metadata.Id, "mb-track-2");

            Housekeeper<CleanupOrphanedTrackFiles>().Clean();

            Mocker.Resolve<TrackRepository>().Get(track.Id).TrackFileId.Should().Be(0);
            Mocker.Resolve<TrackRepository>().Get(free.Id).TrackFileId.Should().Be(0);
            Count("Music_Tracks").Should().Be(2);
        }
    }
}
