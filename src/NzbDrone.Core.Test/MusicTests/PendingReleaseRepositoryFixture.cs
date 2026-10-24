using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Pending;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using MoviePendingRelease = NzbDrone.Core.Movies.Pending.PendingRelease;
using MoviePendingReleaseRepository = NzbDrone.Core.Movies.Pending.PendingReleaseRepository;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class MusicPendingReleaseRepositoryFixture : DbTest<PendingReleaseRepository, PendingRelease>
    {
        private Artist _artist;
        private Artist _other;

        [SetUp]
        public void Setup()
        {
            _artist = AddArtist("The Black Keys", "artist-1");
            _other = AddArtist("Alien Ant Farm", "artist-2");
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

        private static PendingRelease NewPending(Artist artist, string title = "The Black Keys - El Camino - 2011 - FLAC-GRP", PendingReleaseReason reason = PendingReleaseReason.Delay)
        {
            return new PendingRelease
            {
                MediaKind = MediaKind.Music,
                ParentId = artist.Id,
                Title = title,
                Added = DateTime.UtcNow,
                Reason = reason,
                ParsedInfo = new ParsedAlbumInfo
                {
                    ArtistName = "The Black Keys",
                    AlbumTitle = "El Camino",
                    ReleaseTitle = title,
                    ReleaseYear = 2011,
                    Quality = new AudioQualityModel(AudioQuality.FLAC, new Revision(2)),
                    ReleaseGroup = "GRP"
                },
                Release = new ReleaseInfo { Title = title, Indexer = "idx", Size = 1234 },
                AdditionalInfo = new PendingReleaseAdditionalInfo
                {
                    ReleaseSource = ReleaseSourceType.Rss
                }
            };
        }

        [Test]
        public void should_round_trip_the_parsed_album_info_the_release_and_where_it_came_from()
        {
            var pending = NewPending(_artist);

            Subject.Insert(pending);

            var stored = Subject.Get(pending.Id);

            stored.MediaKind.Should().Be(MediaKind.Music);
            stored.ParentId.Should().Be(_artist.Id);
            stored.Reason.Should().Be(PendingReleaseReason.Delay);
            stored.ParsedInfo.ArtistName.Should().Be("The Black Keys");
            stored.ParsedInfo.AlbumTitle.Should().Be("El Camino");
            stored.ParsedInfo.ReleaseYear.Should().Be(2011);
            stored.ParsedInfo.Quality.Quality.Should().Be(AudioQuality.FLAC);
            stored.ParsedInfo.Quality.Revision.Version.Should().Be(2);
            stored.ParsedInfo.ReleaseGroup.Should().Be("GRP");
            stored.Release.Title.Should().Be("The Black Keys - El Camino - 2011 - FLAC-GRP");
            stored.Release.Size.Should().Be(1234);
            stored.AdditionalInfo.ReleaseSource.Should().Be(ReleaseSourceType.Rss);
            stored.RemoteAlbum.Should().BeNull();
        }

        [Test]
        public void should_refuse_a_row_of_another_media_kind()
        {
            var pending = NewPending(_artist);
            pending.MediaKind = MediaKind.Movies;

            var action = () => Subject.Insert(pending);

            action.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void should_get_and_delete_by_artist_id()
        {
            Subject.Insert(NewPending(_artist, "A"));
            Subject.Insert(NewPending(_artist, "B"));
            Subject.Insert(NewPending(_other, "C"));

            Subject.AllByArtistId(_artist.Id).Select(p => p.Title).Should().BeEquivalentTo("A", "B");

            Subject.DeleteByArtistIds(new List<int> { _artist.Id });

            Subject.AllByArtistId(_artist.Id).Should().BeEmpty();
            Subject.AllByArtistId(_other.Id).Should().ContainSingle();
        }

        [Test]
        public void should_not_return_fallback_releases_or_releases_without_an_artist()
        {
            Subject.Insert(NewPending(_artist, "A", PendingReleaseReason.Delay));
            Subject.Insert(NewPending(_artist, "B", PendingReleaseReason.Fallback));
            Subject.Insert(NewPending(_artist, "C", PendingReleaseReason.DownloadClientUnavailable));

            var orphan = NewPending(_artist, "D");
            orphan.ParentId = 9999;
            Subject.Insert(orphan);

            Subject.WithoutFallback().Select(p => p.Title).Should().BeEquivalentTo("A", "C");
        }

        [Test]
        public void should_not_see_or_delete_movie_rows_that_share_a_parent_id()
        {
            var movieRepository = Mocker.Resolve<MoviePendingReleaseRepository>();

            movieRepository.Insert(new MoviePendingRelease
            {
                MediaKind = MediaKind.Movies,
                ParentId = _artist.Id,
                Title = "Movie",
                Added = DateTime.UtcNow,
                Release = new ReleaseInfo()
            });
            Subject.Insert(NewPending(_artist));

            Subject.All().Should().ContainSingle();
            Subject.AllByArtistId(_artist.Id).Should().ContainSingle();
            Subject.WithoutFallback().Should().ContainSingle();

            Subject.DeleteByArtistIds(new List<int> { _artist.Id });

            Subject.All().Should().BeEmpty();
            movieRepository.All().Should().ContainSingle();
        }
    }
}
