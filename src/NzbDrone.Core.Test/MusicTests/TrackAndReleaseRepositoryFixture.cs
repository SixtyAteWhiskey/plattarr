using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Music;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class TrackAndReleaseRepositoryFixture : DbTest<TrackRepository, Track>
    {
        private Artist _artist;
        private Album _album;
        private AlbumRelease _release;

        [SetUp]
        public void Setup()
        {
            var metadata = MusicTestData.NewMetadata();
            Mocker.Resolve<ArtistMetadataRepository>().Insert(metadata);

            _artist = MusicTestData.NewArtist(metadata.Id);
            Mocker.Resolve<ArtistRepository>().Insert(_artist);

            _album = MusicTestData.NewAlbum(metadata.Id);
            Mocker.Resolve<AlbumRepository>().Insert(_album);

            _release = MusicTestData.NewRelease(_album.Id);
            Mocker.Resolve<ReleaseRepository>().Insert(_release);
        }

        [Test]
        public void should_round_trip_a_release_with_media_and_lists()
        {
            var stored = Mocker.Resolve<ReleaseRepository>().Get(_release.Id);

            stored.Should().BeEquivalentTo(_release, o => o.WithDateTolerance().Excluding(r => r.Album).Excluding(r => r.Tracks));
            stored.Media.Single().Format.Should().Be("CD");
            stored.Country.Should().Equal("US", "CA");
        }

        [Test]
        public void should_round_trip_a_track_with_every_column()
        {
            var track = MusicTestData.NewTrack(_release.Id, _artist.ArtistMetadataId, trackFileId: 0);

            Subject.Insert(track);

            Subject.Get(track.Id).Should().BeEquivalentTo(track, o => o.Excluding(t => t.AlbumRelease).Excluding(t => t.ArtistMetadata).Excluding(t => t.TrackFile).Excluding(t => t.Artist).Excluding(t => t.Album).Excluding(t => t.AlbumId));
        }

        [Test]
        public void should_find_tracks_by_release_album_and_artist()
        {
            var track = MusicTestData.NewTrack(_release.Id, _artist.ArtistMetadataId);
            Subject.Insert(track);

            Subject.GetTracksByRelease(_release.Id).Should().ContainSingle().Which.Id.Should().Be(track.Id);
            Subject.GetTracksByAlbum(_album.Id).Should().ContainSingle();
            Subject.GetTracks(_artist.Id).Should().ContainSingle();
            Subject.TracksWithoutFiles(_album.Id).Should().ContainSingle();
            Subject.TracksWithFiles(_artist.Id).Should().BeEmpty();
        }

        [Test]
        public void should_link_and_detach_a_track_file()
        {
            var track = MusicTestData.NewTrack(_release.Id, _artist.ArtistMetadataId);
            Subject.Insert(track);

            track.TrackFileId = 42;
            Subject.SetFileId(new System.Collections.Generic.List<Track> { track });

            Subject.GetTracksByFileId(42).Should().ContainSingle().Which.Id.Should().Be(track.Id);
            Subject.Get(track.Id).HasFile.Should().BeTrue();

            Subject.DetachTrackFile(42);

            Subject.GetTracksByFileId(42).Should().BeEmpty();
            Subject.Get(track.Id).HasFile.Should().BeFalse();
        }

        [Test]
        public void should_find_a_release_by_foreign_id_and_by_old_id()
        {
            var releases = Mocker.Resolve<ReleaseRepository>();

            releases.FindByForeignReleaseId(_release.ForeignReleaseId).Id.Should().Be(_release.Id);
            releases.FindByForeignReleaseId("old-release-1", checkRedirect: true).Id.Should().Be(_release.Id);
            releases.FindByForeignReleaseId("old-release-1").Should().BeNull();
        }

        [Test]
        public void should_keep_foreign_ids_unique()
        {
            var duplicate = MusicTestData.NewTrack(_release.Id, _artist.ArtistMetadataId);
            Subject.Insert(MusicTestData.NewTrack(_release.Id, _artist.ArtistMetadataId));

            System.Action insert = () => Subject.Insert(duplicate);

            insert.Should().Throw<System.Exception>();
        }
    }
}
