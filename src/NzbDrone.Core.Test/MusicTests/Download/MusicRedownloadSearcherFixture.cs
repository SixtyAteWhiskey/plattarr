using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Downloads;
using NzbDrone.Core.Music.Search;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.Download
{
    [TestFixture]
    public class MusicRedownloadSearcherFixture : CoreTest<MusicRedownloadSearcher>
    {
        private static DownloadFailedEvent Failure(params int[] albumIds)
        {
            return new DownloadFailedEvent { Kind = MediaKind.Music, ParentId = 5, ItemIds = albumIds.ToList() };
        }

        private void GivenArtistAlbums(int albumCount)
        {
            Mocker.GetMock<IAlbumService>()
                  .Setup(s => s.GetAlbumsByArtist(5))
                  .Returns(Enumerable.Range(1, albumCount).Select(i => new Album { Id = i }).ToList());
        }

        [Test]
        public void should_be_the_music_searcher()
        {
            Subject.Kind.Should().Be(MediaKind.Music);
        }

        [Test]
        public void should_not_search_when_the_failure_has_no_albums()
        {
            Subject.Search(Failure());

            Mocker.GetMock<IManageCommandQueue>().Verify(c => c.Push(It.IsAny<Command>()), Times.Never());
        }

        [Test]
        public void should_search_again_for_a_single_album_without_asking_for_the_artist_albums()
        {
            Subject.Search(Failure(11));

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(c => c.Push(It.Is<AlbumSearchCommand>(a => a.AlbumIds.SequenceEqual(new List<int> { 11 }))), Times.Once());
            Mocker.GetMock<IAlbumService>().Verify(s => s.GetAlbumsByArtist(It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_search_the_artist_when_every_album_failed()
        {
            GivenArtistAlbums(3);

            Subject.Search(Failure(1, 2, 3));

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(c => c.Push(It.Is<ArtistSearchCommand>(a => a.ArtistId == 5)), Times.Once());
            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(c => c.Push(It.IsAny<AlbumSearchCommand>()), Times.Never());
        }

        [Test]
        public void should_search_the_albums_when_the_failure_names_more_albums_than_the_artist_has()
        {
            GivenArtistAlbums(2);

            Subject.Search(Failure(1, 2, 3));

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(c => c.Push(It.Is<AlbumSearchCommand>(a => a.AlbumIds.SequenceEqual(new List<int> { 1, 2, 3 }))), Times.Once());
            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(c => c.Push(It.IsAny<ArtistSearchCommand>()), Times.Never());
        }

        [Test]
        public void should_search_the_albums_when_only_some_of_the_artist_albums_failed()
        {
            GivenArtistAlbums(10);

            Subject.Search(Failure(1, 2));

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(c => c.Push(It.Is<AlbumSearchCommand>(a => a.AlbumIds.SequenceEqual(new List<int> { 1, 2 }))), Times.Once());
            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(c => c.Push(It.IsAny<ArtistSearchCommand>()), Times.Never());
        }
    }
}
