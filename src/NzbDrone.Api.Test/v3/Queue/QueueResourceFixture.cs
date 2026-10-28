using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Queue;

namespace NzbDrone.Api.Test.v3.Queue
{
    [TestFixture]
    public class QueueResourceFixture : TestBase
    {
        private CustomFormat _format;
        private QualityProfile _profile;
        private Series _series;
        private Episode _episode;

        [SetUp]
        public void Setup()
        {
            _format = new CustomFormat("Test") { Id = 7 };

            _profile = new QualityProfile
            {
                Name = "Test",
                Items = new List<QualityProfileQualityItem>(),
                FormatItems = new List<ProfileFormatItem> { new ProfileFormatItem { Format = _format, Score = 25 } }
            };

            _series = new Series
            {
                Id = 3,
                Title = "Series",
                QualityProfileId = 1,
                QualityProfile = new LazyLoaded<QualityProfile>(_profile)
            };

            _episode = new Episode { Id = 40, SeriesId = 3, SeasonNumber = 2, EpisodeNumber = 5, EpisodeFileId = 12 };
        }

        private NzbDrone.Core.Queue.Queue GivenTvItem()
        {
            return new NzbDrone.Core.Queue.Queue
            {
                Id = 9,
                Title = "Series.S02E05.1080p",
                Quality = new QualityModel(Quality.WEBDL1080p),
                Languages = new List<NzbDrone.Core.Languages.Language>(),
                RemoteRelease = new RemoteEpisode
                {
                    Series = _series,
                    Episodes = new List<Episode> { _episode },
                    CustomFormats = new List<CustomFormat> { _format }
                }
            };
        }

        [Test]
        public void should_expose_routed_media_kind_without_a_metadata_match()
        {
            var queue = new NzbDrone.Core.Queue.Queue
            {
                Kind = MediaKind.Music,
                Quality = new QualityModel(Quality.Unknown),
                Languages = new List<NzbDrone.Core.Languages.Language>()
            };

            var resource = queue.ToResource(false);

            resource.MediaKind.Should().Be(MediaKind.Music);
            resource.ArtistId.Should().BeNull();
            STJson.ToJson(resource).Should().Contain("\"mediaKind\": \"music\"");
        }

        [Test]
        public void should_map_the_series_and_episode_of_a_tv_download()
        {
            var resource = GivenTvItem().ToResource(false);

            resource.SeriesId.Should().Be(3);
            resource.EpisodeId.Should().Be(40);
            resource.SeasonNumber.Should().Be(2);
            resource.EpisodeHasFile.Should().BeTrue();
            resource.MovieId.Should().BeNull();
            resource.MediaKind.Should().Be(MediaKind.Tv);
        }

        [Test]
        public void should_score_a_tv_download_with_the_series_quality_profile()
        {
            var resource = GivenTvItem().ToResource(false);

            resource.CustomFormatScore.Should().Be(25);
            resource.CustomFormats.Should().HaveCount(1);
        }

        [Test]
        public void should_include_series_and_episode_only_when_asked()
        {
            GivenTvItem().ToResource(false).Series.Should().BeNull();
            GivenTvItem().ToResource(false).Episode.Should().BeNull();

            var resource = GivenTvItem().ToResource(false, true, true);

            resource.Series.Should().NotBeNull();
            resource.Series.Title.Should().Be("Series");
            resource.Episode.Should().NotBeNull();
            resource.Episode.EpisodeNumber.Should().Be(5);
        }

        [Test]
        public void should_not_write_tv_fields_for_a_movie_download()
        {
            var item = new NzbDrone.Core.Queue.Queue
            {
                Id = 4,
                Title = "Movie.2020.1080p",
                Quality = new QualityModel(Quality.WEBDL1080p),
                Languages = new List<NzbDrone.Core.Languages.Language>(),
                RemoteRelease = new RemoteMovie { Movie = new Movie { Id = 8 } }
            };

            var json = STJson.ToJson(item.ToResource(false));

            json.Should().Contain("\"movieId\"");
            json.Should().Contain("\"mediaKind\": \"movies\"");
            json.Should().NotContain("seriesId");
            json.Should().NotContain("episodeId");
            json.Should().NotContain("seasonNumber");
            json.Should().NotContain("episodeHasFile");
        }

        private NzbDrone.Core.Queue.Queue GivenMusicItem()
        {
            return new NzbDrone.Core.Queue.Queue
            {
                Id = 21,
                Title = "The Black Keys - Brothers [FLAC]",
                Quality = new QualityModel(Quality.Unknown),
                Languages = new List<NzbDrone.Core.Languages.Language>(),
                RemoteRelease = new RemoteAlbum
                {
                    Artist = new Artist
                    {
                        Id = 12,
                        SortName = "black keys",
                        Metadata = new ArtistMetadata { Name = "The Black Keys", Images = new List<NzbDrone.Core.MediaCover.MediaCover>() },
                        Tags = new HashSet<int>(),
                        QualityProfile = new AudioQualityProfile
                        {
                            Name = "Lossless",
                            FormatItems = new List<ProfileFormatItem> { new ProfileFormatItem { Format = _format, Score = 40 } }
                        }
                    },
                    Albums = new List<Album>
                    {
                        new Album { Id = 101, Title = "Brothers", Images = new List<NzbDrone.Core.MediaCover.MediaCover>(), SecondaryTypes = new List<SecondaryAlbumType>(), Genres = new List<string>() },
                        new Album { Id = 102, Title = "El Camino", Images = new List<NzbDrone.Core.MediaCover.MediaCover>(), SecondaryTypes = new List<SecondaryAlbumType>(), Genres = new List<string>() }
                    },
                    CustomFormats = new List<CustomFormat> { _format }
                }
            };
        }

        [Test]
        public void should_map_the_artist_and_first_album_of_a_music_download()
        {
            var resource = GivenMusicItem().ToResource(false);

            resource.ArtistId.Should().Be(12);
            resource.AlbumId.Should().Be(101);
            resource.MovieId.Should().BeNull();
            resource.SeriesId.Should().BeNull();
        }

        [Test]
        public void should_score_a_music_download_with_the_artist_quality_profile()
        {
            var resource = GivenMusicItem().ToResource(false);

            resource.CustomFormatScore.Should().Be(40);
            resource.CustomFormats.Should().HaveCount(1);
        }

        private static AlbumRelease GivenRelease(bool monitored, params bool[] trackHasFile)
        {
            return new AlbumRelease
            {
                Monitored = monitored,
                Tracks = trackHasFile.Select(hasFile => new Track { TrackFileId = hasFile ? 5 : 0 }).ToList()
            };
        }

        [Test]
        public void should_count_the_tracks_of_the_monitored_release_and_those_with_a_file()
        {
            var item = GivenMusicItem();
            var album = ((RemoteAlbum)item.RemoteRelease).Albums[0];

            album.AlbumReleases = new List<AlbumRelease>
            {
                GivenRelease(false, true, true, true, true, true, true, true, true, true, true),
                GivenRelease(true, true, false, true)
            };

            var resource = item.ToResource(false);

            resource.TrackFileCount.Should().Be(3);
            resource.TrackHasFileCount.Should().Be(2);
        }

        [Test]
        public void should_count_no_tracks_when_the_album_has_no_monitored_release()
        {
            var item = GivenMusicItem();
            var album = ((RemoteAlbum)item.RemoteRelease).Albums[0];

            album.AlbumReleases = new List<AlbumRelease> { GivenRelease(false, true, true) };

            var resource = item.ToResource(false);

            resource.TrackFileCount.Should().Be(0);
            resource.TrackHasFileCount.Should().Be(0);
        }

        [Test]
        public void should_write_the_track_counts_for_a_music_download()
        {
            var item = GivenMusicItem();
            var album = ((RemoteAlbum)item.RemoteRelease).Albums[0];

            album.AlbumReleases = new List<AlbumRelease> { GivenRelease(true, true) };

            var json = STJson.ToJson(item.ToResource(false));

            json.Should().Contain("\"trackFileCount\": 1");
            json.Should().Contain("\"trackHasFileCount\": 1");
        }

        [Test]
        public void should_include_artist_and_album_only_when_asked()
        {
            GivenMusicItem().ToResource(false).Artist.Should().BeNull();
            GivenMusicItem().ToResource(false).Album.Should().BeNull();

            var resource = GivenMusicItem().ToResource(false, false, false, true, true);

            resource.Artist.Should().NotBeNull();
            resource.Artist.ArtistName.Should().Be("The Black Keys");
            resource.Album.Should().NotBeNull();
            resource.Album.Title.Should().Be("Brothers");
        }

        [Test]
        public void should_not_write_music_fields_for_a_movie_or_tv_download()
        {
            var movie = new NzbDrone.Core.Queue.Queue
            {
                Id = 4,
                Title = "Movie.2020.1080p",
                Quality = new QualityModel(Quality.WEBDL1080p),
                Languages = new List<NzbDrone.Core.Languages.Language>(),
                RemoteRelease = new RemoteMovie { Movie = new Movie { Id = 8 } }
            };

            foreach (var json in new[] { STJson.ToJson(movie.ToResource(false)), STJson.ToJson(GivenTvItem().ToResource(false)) })
            {
                json.Should().NotContain("trackFileCount");
                json.Should().NotContain("trackHasFileCount");
                json.Should().NotContain("artistId");
                json.Should().NotContain("albumId");
                json.Should().NotContain("\"artist\"");
                json.Should().NotContain("\"album\"");
            }
        }

        [Test]
        public void is_matched_should_be_true_for_an_artist_and_false_for_an_unmatched_album()
        {
            GivenMusicItem().IsMatched().Should().BeTrue();
            new NzbDrone.Core.Queue.Queue { RemoteRelease = new RemoteAlbum() }.IsMatched().Should().BeFalse();
        }

        [Test]
        public void is_matched_should_be_true_for_a_series_or_a_movie_and_false_for_neither()
        {
            GivenTvItem().IsMatched().Should().BeTrue();

            new NzbDrone.Core.Queue.Queue { RemoteRelease = new RemoteMovie { Movie = new Movie { Id = 1 } } }.IsMatched().Should().BeTrue();
            new NzbDrone.Core.Queue.Queue { RemoteRelease = new RemoteEpisode() }.IsMatched().Should().BeFalse();
            new NzbDrone.Core.Queue.Queue { RemoteRelease = new RemoteMovie() }.IsMatched().Should().BeFalse();
            new NzbDrone.Core.Queue.Queue().IsMatched().Should().BeFalse();
        }
    }
}
