using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.ArtistStats;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.ArtistStatsTests
{
    [TestFixture]
    public class ArtistStatisticsFixture : DbTest<ArtistStatisticsRepository, Artist>
    {
        private Artist _artist;
        private Artist _other;

        [SetUp]
        public void Setup()
        {
            _artist = GivenArtist("The Black Keys", "d15721d8-56b4-453d-b506-fc915b14cba2");
            _other = GivenArtist("The Black Eyed Peas", "d5be5333-4171-427e-8e12-732087c6b78e");
        }

        private Artist GivenArtist(string name, string foreignId)
        {
            var metadata = Db.Insert(MusicTestData.NewMetadata(name, foreignId));

            return Db.Insert(MusicTestData.NewArtist(metadata.Id, name: name));
        }

        private Album GivenAlbum(Artist artist, string foreignId, DateTime releaseDate, bool monitored = true)
        {
            var album = MusicTestData.NewAlbum(artist.ArtistMetadataId, foreignId, foreignId);
            album.ReleaseDate = releaseDate;
            album.Monitored = monitored;

            return Db.Insert(album);
        }

        private AlbumRelease GivenRelease(Album album, string foreignId, bool monitored = true)
        {
            return Db.Insert(MusicTestData.NewRelease(album.Id, foreignId, monitored));
        }

        private Track GivenTrack(AlbumRelease release, Artist artist, string foreignId, int fileId = 0)
        {
            return Db.Insert(MusicTestData.NewTrack(release.Id, artist.ArtistMetadataId, foreignId, fileId));
        }

        private int GivenFile(Album album, string path, long size)
        {
            var file = MusicTestData.NewTrackFile(album.Id, path);
            file.Size = size;

            return Db.Insert(file).Id;
        }

        [Test]
        public void should_count_tracks_per_album()
        {
            var past = DateTime.UtcNow.AddDays(-10);
            var future = DateTime.UtcNow.AddDays(10);

            var released = GivenAlbum(_artist, "released", past);
            var releasedRelease = GivenRelease(released, "released-r");
            var fileId = GivenFile(released, "/music/released/01.flac", 1000);
            GivenTrack(releasedRelease, _artist, "released-t1", fileId);
            GivenTrack(releasedRelease, _artist, "released-t2");
            GivenTrack(releasedRelease, _artist, "released-t3");

            var unmonitored = GivenAlbum(_artist, "unmonitored", past, false);
            var unmonitoredRelease = GivenRelease(unmonitored, "unmonitored-r");
            GivenTrack(unmonitoredRelease, _artist, "unmonitored-t1");
            GivenTrack(unmonitoredRelease, _artist, "unmonitored-t2");

            var upcoming = GivenAlbum(_artist, "upcoming", future);
            var upcomingRelease = GivenRelease(upcoming, "upcoming-r");
            GivenTrack(upcomingRelease, _artist, "upcoming-t1");
            GivenTrack(upcomingRelease, _artist, "upcoming-t2");

            var stats = Subject.ArtistStatistics(_artist.Id);

            stats.Should().HaveCount(3);

            var a = stats.Single(s => s.AlbumId == released.Id);
            a.TotalTrackCount.Should().Be(3);
            a.AvailableTrackCount.Should().Be(3);
            a.TrackCount.Should().Be(3);
            a.TrackFileCount.Should().Be(1);

            // Released but not monitored: available, but not counted unless it has a file.
            var b = stats.Single(s => s.AlbumId == unmonitored.Id);
            b.TotalTrackCount.Should().Be(2);
            b.AvailableTrackCount.Should().Be(2);
            b.TrackCount.Should().Be(0);
            b.TrackFileCount.Should().Be(0);

            // Monitored but not out yet: neither available nor counted.
            var c = stats.Single(s => s.AlbumId == upcoming.Id);
            c.TotalTrackCount.Should().Be(2);
            c.AvailableTrackCount.Should().Be(0);
            c.TrackCount.Should().Be(0);
        }

        [Test]
        public void should_count_a_file_on_an_unreleased_unmonitored_album()
        {
            var future = DateTime.UtcNow.AddDays(10);
            var album = GivenAlbum(_artist, "early", future, false);
            var release = GivenRelease(album, "early-r");
            var fileId = GivenFile(album, "/music/early/01.flac", 10);
            GivenTrack(release, _artist, "early-t1", fileId);
            GivenTrack(release, _artist, "early-t2");

            var stats = Subject.ArtistStatistics(_artist.Id).Single();

            stats.TotalTrackCount.Should().Be(2);
            stats.AvailableTrackCount.Should().Be(1);
            stats.TrackCount.Should().Be(1);
            stats.TrackFileCount.Should().Be(1);
        }

        [Test]
        public void should_only_count_the_tracks_of_monitored_releases()
        {
            var past = DateTime.UtcNow.AddDays(-10);
            var album = GivenAlbum(_artist, "two-releases", past);
            var chosen = GivenRelease(album, "chosen", true);
            var other = GivenRelease(album, "other", false);

            GivenTrack(chosen, _artist, "chosen-t1");
            GivenTrack(chosen, _artist, "chosen-t2");
            GivenTrack(other, _artist, "other-t1");
            GivenTrack(other, _artist, "other-t2");
            GivenTrack(other, _artist, "other-t3");

            var stats = Subject.ArtistStatistics(_artist.Id).Single();

            stats.TotalTrackCount.Should().Be(2);
            stats.TrackCount.Should().Be(2);
        }

        [Test]
        public void should_sum_the_size_on_disk_per_album()
        {
            var past = DateTime.UtcNow.AddDays(-10);
            var album = GivenAlbum(_artist, "sized", past);
            var release = GivenRelease(album, "sized-r");
            var first = GivenFile(album, "/music/sized/01.flac", 1000);
            var second = GivenFile(album, "/music/sized/02.flac", 500);
            GivenTrack(release, _artist, "sized-t1", first);
            GivenTrack(release, _artist, "sized-t2", second);

            var stats = Subject.ArtistStatistics(_artist.Id).Single();

            stats.SizeOnDisk.Should().Be(1500);
            stats.TrackFileCount.Should().Be(2);
        }

        [Test]
        public void should_only_return_the_requested_artist()
        {
            var past = DateTime.UtcNow.AddDays(-10);

            var mine = GivenAlbum(_artist, "mine", past);
            GivenTrack(GivenRelease(mine, "mine-r"), _artist, "mine-t1");

            var theirs = GivenAlbum(_other, "theirs", past);
            GivenTrack(GivenRelease(theirs, "theirs-r"), _other, "theirs-t1");
            GivenTrack(GivenRelease(theirs, "theirs-r2"), _other, "theirs-t2");

            var one = Subject.ArtistStatistics(_artist.Id);
            var all = Subject.ArtistStatistics();

            one.Should().ContainSingle().Which.ArtistId.Should().Be(_artist.Id);
            all.Select(s => s.ArtistId).Should().BeEquivalentTo(new[] { _artist.Id, _other.Id });
            all.Single(s => s.ArtistId == _other.Id).TotalTrackCount.Should().Be(2);
        }
    }
}
