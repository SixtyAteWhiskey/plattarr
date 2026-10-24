using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.History;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Search;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class ArtistParentFixture
    {
        [Test]
        public void should_be_a_music_parent_titled_with_the_artist_name()
        {
            var artist = new Artist { Metadata = new ArtistMetadata { Name = "The Black Keys" } };
            IMediaParent parent = artist;

            parent.Kind.Should().Be(MediaKind.Music);
            parent.Title.Should().Be("The Black Keys");
        }

        // Equ compares non-public members, so IMediaParent.Title must stay out of the equality or two
        // equal artists differ by whether their metadata is loaded.
        [Test]
        public void should_be_equal_to_a_copy_whose_metadata_is_not_loaded()
        {
            var loaded = new Artist { Id = 1, ArtistMetadataId = 2, CleanName = "blackkeys", Metadata = new ArtistMetadata { Name = "The Black Keys" } };
            var bare = new Artist { Id = 1, ArtistMetadataId = 2, CleanName = "blackkeys" };

            loaded.Should().Be(bare);
        }
    }

    [TestFixture]
    public class RemoteAlbumFixture
    {
        private static RemoteAlbum Build(params DateTime?[] releaseDates)
        {
            var albums = new List<Album>();
            var id = 10;

            foreach (var date in releaseDates)
            {
                albums.Add(new Album { Id = id++, ReleaseDate = date });
            }

            return new RemoteAlbum
            {
                Artist = new Artist { Id = 3, Metadata = new ArtistMetadata { Name = "Artist" } },
                Albums = albums,
                ParsedAlbumInfo = new ParsedAlbumInfo
                {
                    Quality = new AudioQualityModel(AudioQuality.FLAC),
                    ReleaseGroup = "GRP",
                    ReleaseYear = 2011
                }
            };
        }

        [Test]
        public void should_expose_the_artist_as_parent_and_the_albums_as_items()
        {
            var release = Build(new DateTime(2010, 1, 1), new DateTime(2011, 1, 1));

            release.Kind.Should().Be(MediaKind.Music);
            release.Parent.Should().BeSameAs(release.Artist);
            release.ItemIds.Should().Equal(10, 11);
        }

        [Test]
        public void should_show_the_audio_quality_as_a_display_model()
        {
            var quality = Build().Quality;

            quality.Quality.Id.Should().Be(AudioQuality.FLAC.Id);
            quality.Quality.Name.Should().Be("FLAC");
        }

        [Test]
        public void should_show_unknown_quality_when_the_release_was_not_parsed()
        {
            var release = Build();
            release.ParsedAlbumInfo = null;

            release.Quality.Quality.Id.Should().Be(AudioQuality.Unknown.Id);
            release.ReleaseGroup.Should().BeNull();
        }

        [Test]
        public void should_take_the_release_group_and_year_from_the_parsed_info()
        {
            var release = Build(new DateTime(2005, 1, 1));

            release.ReleaseGroup.Should().Be("GRP");
            release.Year.Should().Be(2011);
        }

        [Test]
        public void should_fall_back_to_the_first_albums_year()
        {
            var release = Build(new DateTime(2005, 6, 1));
            release.ParsedAlbumInfo.ReleaseYear = null;

            release.Year.Should().Be(2005);
        }

        [Test]
        public void should_be_recent_when_an_album_came_out_in_the_last_two_weeks()
        {
            Build(DateTime.UtcNow.AddDays(-3)).IsRecent.Should().BeTrue();
            Build(DateTime.UtcNow.AddDays(-30)).IsRecent.Should().BeFalse();
            Build(new DateTime?[] { null }).IsRecent.Should().BeFalse();
        }

        [Test]
        public void should_have_no_items_without_albums()
        {
            var release = Build();
            release.Albums = null;

            release.ItemIds.Should().BeEmpty();
            release.IsRecent.Should().BeFalse();
        }
    }

    [TestFixture]
    public class MusicSearchRequestFixture
    {
        [TestCase("Various Artists", "VA")]
        [TestCase("The Black Keys", "Black+Keys")]
        [TestCase("AC/DC", "AC+DC")]
        [TestCase("Beyoncé", "Beyonce")]
        [TestCase("Guns N' Roses", "Guns+N'+Roses")]
        [TestCase("Guns N’ Roses", "Guns+N'+Roses")]
        [TestCase("Florence + the Machine", "Florence+the+Machine")]
        [TestCase("+", "+")]
        public void should_build_a_clean_query_title(string title, string expected)
        {
            MusicSearchRequest.GetQueryTitle(title).Should().Be(expected);
        }

        [Test]
        public void should_query_by_the_artist_name()
        {
            var request = new MusicSearchRequest { Artist = new Artist { Metadata = new ArtistMetadata { Name = "The Black Keys" } } };

            request.ArtistQuery.Should().Be("The Black Keys");
            request.CleanArtistQuery.Should().Be("Black+Keys");
            request.ToString().Should().Be("[The Black Keys]");
        }
    }

    [TestFixture]
    public class MusicHistoryServiceFixture : CoreTest<HistoryService>
    {
        [Test]
        public void should_read_an_artists_history_from_the_repository()
        {
            var rows = new List<AlbumHistory> { new AlbumHistory { ParentId = 3 } };
            Mocker.GetMock<IAlbumHistoryRepository>().Setup(r => r.GetByArtist(3, AlbumHistoryEventType.Grabbed)).Returns(rows);

            Subject.GetByArtist(3, AlbumHistoryEventType.Grabbed).Should().BeSameAs(rows);
        }

        [Test]
        public void should_read_an_albums_history_from_the_repository()
        {
            var rows = new List<AlbumHistory> { new AlbumHistory { ItemId = 8 } };
            Mocker.GetMock<IAlbumHistoryRepository>().Setup(r => r.GetByAlbum(8, null)).Returns(rows);

            Subject.GetByAlbum(8, null).Should().BeSameAs(rows);
        }

        [Test]
        public void should_update_many_rows_through_the_repository()
        {
            var rows = new List<AlbumHistory> { new AlbumHistory { Id = 1 } };

            Subject.UpdateMany(rows);

            Mocker.GetMock<IAlbumHistoryRepository>().Verify(r => r.UpdateMany(rows), Times.Once());
        }
    }

    [TestFixture]
    public class MusicConfigFixture : CoreTest<ConfigService>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IConfigRepository>().Setup(c => c.All()).Returns(new List<Config>());
        }

        [Test]
        public void should_default_to_the_lidarr_metadata_service_and_no_empty_artist_folders()
        {
            Subject.MusicMetadataSource.Should().BeEmpty();
            Subject.CreateEmptyArtistFolders.Should().BeFalse();
        }

        [Test]
        public void should_store_the_music_metadata_source_separately_from_the_movie_one()
        {
            Mocker.GetMock<IConfigRepository>().Setup(c => c.Get("musicmetadatasource")).Returns((Config)null);

            Subject.MusicMetadataSource = "https://metadata.example/api";

            Mocker.GetMock<IConfigRepository>().Verify(c => c.Upsert("musicmetadatasource", "https://metadata.example/api"), Times.Once());
            Mocker.GetMock<IConfigRepository>().Verify(c => c.Upsert("metadatasource", It.IsAny<string>()), Times.Never());
        }
    }
}
