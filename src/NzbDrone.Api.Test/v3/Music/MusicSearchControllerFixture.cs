using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.MetadataSource;
using NzbDrone.Core.Music.Organizer;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Music.Search;

namespace NzbDrone.Api.Test.v3.Music
{
    [TestFixture]
    public class MusicSearchControllerFixture : TestBase<MusicSearchController>
    {
        private static Artist NewArtist(string id, string name)
        {
            return new Artist
            {
                ForeignArtistId = id,
                Metadata = new ArtistMetadata
                {
                    ForeignArtistId = id,
                    Name = name,
                    Images = new List<NzbDrone.Core.MediaCover.MediaCover>
                    {
                        new NzbDrone.Core.MediaCover.MediaCover(MediaCoverTypes.Poster, "https://images.example/poster.jpg")
                    },
                    Links = new List<Links>(),
                    Genres = new List<string>()
                },
                Tags = new HashSet<int>()
            };
        }

        private static Album NewAlbum(string id, string title, Artist artist)
        {
            return new Album
            {
                ForeignAlbumId = id,
                Title = title,
                Artist = artist,
                Images = new List<NzbDrone.Core.MediaCover.MediaCover>
                {
                    new NzbDrone.Core.MediaCover.MediaCover(MediaCoverTypes.Cover, "https://images.example/cover.jpg")
                },
                SecondaryTypes = new List<SecondaryAlbumType>(),
                Genres = new List<string>(),
                Links = new List<Links>(),
                AlbumReleases = new List<AlbumRelease>()
            };
        }

        private void GivenResults(params object[] results)
        {
            Mocker.GetMock<ISearchForNewEntity>()
                  .Setup(s => s.SearchForNewEntity(It.IsAny<string>()))
                  .Returns(results.ToList());

            Mocker.GetMock<IBuildFileNames>()
                  .Setup(b => b.GetArtistFolder(It.IsAny<Artist>(), null))
                  .Returns<Artist, NamingConfig>((a, _) => "Folder of " + a.Metadata.Value.Name);
        }

        [Test]
        public void should_map_an_artist_with_its_remote_poster_and_folder()
        {
            GivenResults(NewArtist("artist-1", "The Black Keys"));

            var result = Subject.Search("black keys").Single();

            result.Artist.Should().NotBeNull();
            result.Album.Should().BeNull();
            result.ForeignId.Should().Be("artist-1");
            result.Artist.ArtistName.Should().Be("The Black Keys");
            result.Artist.RemotePoster.Should().Be("https://images.example/poster.jpg");
            result.Artist.Folder.Should().Be("Folder of The Black Keys");
        }

        [Test]
        public void should_map_an_album_with_its_remote_cover_and_the_artist_folder()
        {
            GivenResults(NewAlbum("album-1", "Brothers", NewArtist("artist-1", "The Black Keys")));

            var result = Subject.Search("brothers").Single();

            result.Album.Should().NotBeNull();
            result.Artist.Should().BeNull();
            result.ForeignId.Should().Be("album-1");
            result.Album.Title.Should().Be("Brothers");
            result.Album.RemoteCover.Should().Be("https://images.example/cover.jpg");
            result.Album.Artist.Folder.Should().Be("Folder of The Black Keys");
        }

        [Test]
        public void should_keep_the_order_of_the_service_and_number_the_entries_from_one()
        {
            var artist = NewArtist("artist-1", "The Black Keys");

            GivenResults(artist, NewAlbum("album-1", "Brothers", artist), NewArtist("artist-2", "Black Pumas"));

            var result = Subject.Search("black").ToList();

            result.Select(r => r.Id).Should().Equal(1, 2, 3);
            result.Select(r => r.ForeignId).Should().Equal("artist-1", "album-1", "artist-2");
        }

        [Test]
        public void should_pass_the_term_to_the_service()
        {
            GivenResults();

            Subject.Search("lidarr:abc").Should().BeEmpty();

            Mocker.GetMock<ISearchForNewEntity>().Verify(s => s.SearchForNewEntity("lidarr:abc"), Times.Once());
        }

        [Test]
        public void should_refuse_an_entry_that_is_neither_an_artist_nor_an_album()
        {
            GivenResults(new object());

            Action act = () => Subject.Search("anything");

            act.Should().Throw<NotImplementedException>();
        }
    }
}
