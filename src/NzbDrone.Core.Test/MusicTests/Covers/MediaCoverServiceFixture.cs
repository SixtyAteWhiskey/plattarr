using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Http;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Covers;
using NzbDrone.Core.Music.Events;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.MediaCoverTests
{
    [TestFixture]
    public class MediaCoverServiceFixture : CoreTest<MediaCoverService>
    {
        private Artist _artist;
        private Album _album;
        private HttpResponse _httpResponse;

        [SetUp]
        public void Setup()
        {
            Mocker.SetConstant<IAppFolderInfo>(new AppFolderInfo(Mocker.Resolve<IStartupContext>()));

            _artist = Builder<Artist>.CreateNew()
                .With(v => v.Id = 2)
                .With(v => v.Metadata.Value.Images = new List<MediaCover.MediaCover> { new MediaCover.MediaCover(MediaCoverTypes.Poster, "") })
                .Build();

            _album = Builder<Album>.CreateNew()
                .With(v => v.Id = 4)
                .With(v => v.Images = new List<MediaCover.MediaCover> { new MediaCover.MediaCover(MediaCoverTypes.Cover, "") })
                .Build();

            _httpResponse = new HttpResponse(null, new HttpHeader(), "");
            Mocker.GetMock<IHttpClient>().Setup(c => c.Head(It.IsAny<HttpRequest>())).Returns(_httpResponse);
        }

        [TestCase(".png")]
        [TestCase(".jpg")]
        public void should_convert_cover_urls_to_local(string extension)
        {
            var covers = new List<MediaCover.MediaCover>
                {
                    new MediaCover.MediaCover
                    {
                        RemoteUrl = "http://dummy.com/test" + extension,
                        CoverType = MediaCoverTypes.Banner
                    }
                };

            Mocker.GetMock<IDiskProvider>().Setup(c => c.FileGetLastWrite(It.IsAny<string>()))
                  .Returns(new DateTime(1234));

            Mocker.GetMock<IDiskProvider>().Setup(c => c.FileExists(It.IsAny<string>()))
                  .Returns(true);

            Subject.ConvertToLocalUrls(12, MediaCoverEntity.Artist, covers);

            covers.Single().Url.Should().Be("/MediaCover/Artists/12/banner" + extension + "?lastWrite=1234");
        }

        [TestCase(".png")]
        [TestCase(".jpg")]
        public void should_convert_album_cover_urls_to_local(string extension)
        {
            var covers = new List<MediaCover.MediaCover>
                {
                    new MediaCover.MediaCover
                    {
                        RemoteUrl = "http://dummy.com/test" + extension,
                        CoverType = MediaCoverTypes.Disc
                    }
                };

            Mocker.GetMock<IDiskProvider>().Setup(c => c.FileGetLastWrite(It.IsAny<string>()))
                  .Returns(new DateTime(1234));

            Mocker.GetMock<IDiskProvider>().Setup(c => c.FileExists(It.IsAny<string>()))
                  .Returns(true);

            Subject.ConvertToLocalUrls(6, MediaCoverEntity.Album, covers);

            covers.Single().Url.Should().Be("/MediaCover/Albums/6/disc" + extension + "?lastWrite=1234");
        }

        [TestCase(".png")]
        [TestCase(".jpg")]
        public void should_convert_media_urls_to_local_without_time_if_file_doesnt_exist(string extension)
        {
            var covers = new List<MediaCover.MediaCover>
                {
                    new MediaCover.MediaCover
                    {
                        RemoteUrl = "http://dummy.com/test" + extension,
                        CoverType = MediaCoverTypes.Banner
                    }
                };

            Mocker.GetMock<IDiskProvider>()
                .Setup(c => c.FileExists(It.IsAny<string>()))
                .Returns(false);

            Subject.ConvertToLocalUrls(12, MediaCoverEntity.Artist, covers);

            covers.Single().Url.Should().Be("/MediaCover/Artists/12/banner" + extension);
        }

        [Test]
        public void should_rebuild_covers_from_disk_when_the_stored_list_is_empty_without_exposing_their_file_paths()
        {
            var covers = new List<MediaCover.MediaCover>();

            Mocker.GetMock<IDiskProvider>().Setup(c => c.FolderExists(It.IsAny<string>())).Returns(true);
            Mocker.GetMock<IDiskProvider>()
                  .Setup(c => c.GetFileInfos(It.IsAny<string>(), false))
                  .Returns(new List<FileInfo>
                  {
                      new FileInfo("/cache/Artists/12/poster.jpg"),
                      new FileInfo("/cache/Artists/12/poster-250.jpg"),
                      new FileInfo("/cache/Artists/12/clearlogo.png")
                  });
            Mocker.GetMock<IDiskProvider>().Setup(c => c.FileExists(It.IsAny<string>())).Returns(false);

            Subject.ConvertToLocalUrls(12, MediaCoverEntity.Artist, covers);

            covers.Select(c => (c.CoverType, c.Url)).Should().BeEquivalentTo(new[]
            {
                (MediaCoverTypes.Poster, "/MediaCover/Artists/12/poster.jpg"),
                (MediaCoverTypes.Clearlogo, "/MediaCover/Artists/12/clearlogo.png")
            });

            // The file path read the extension from is not a remote URL: it must not reach the API.
            covers.Should().OnlyContain(c => c.RemoteUrl == null);
        }

        private void GivenCoverUrls()
        {
            _artist.Metadata.Value.Images = new List<MediaCover.MediaCover> { new MediaCover.MediaCover(MediaCoverTypes.Poster, "https://img.example/artist.jpg") };
            _album.Images = new List<MediaCover.MediaCover> { new MediaCover.MediaCover(MediaCoverTypes.Cover, "https://img.example/album.jpg") };

            Mocker.GetMock<IAlbumService>()
                  .Setup(v => v.GetAlbumsByArtist(It.IsAny<int>()))
                  .Returns(new List<Album> { _album });

            Mocker.GetMock<IDiskProvider>()
                  .Setup(v => v.FileExists(It.IsAny<string>()))
                  .Returns(true);
        }

        [Test]
        public void should_download_artist_and_album_covers_into_their_own_folders_when_missing()
        {
            GivenCoverUrls();

            Mocker.GetMock<ICoverExistsSpecification>()
                  .Setup(v => v.AlreadyExists(It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(false);

            Subject.HandleAsync(new ArtistRefreshCompleteEvent(_artist));

            var artistFolder = Path.Combine("Artists", "2");
            var albumFolder = Path.Combine("Albums", "4");

            Mocker.GetMock<IHttpClient>()
                  .Verify(c => c.DownloadFile("https://img.example/artist.jpg", It.Is<string>(f => f.Contains(artistFolder))), Times.Once());

            Mocker.GetMock<IHttpClient>()
                  .Verify(c => c.DownloadFile("https://img.example/album.jpg", It.Is<string>(f => f.Contains(albumFolder))), Times.Once());
        }

        [Test]
        public void should_not_download_covers_that_already_exist()
        {
            GivenCoverUrls();

            Mocker.GetMock<ICoverExistsSpecification>()
                  .Setup(v => v.AlreadyExists(It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(true);

            Subject.HandleAsync(new ArtistRefreshCompleteEvent(_artist));

            Mocker.GetMock<IHttpClient>()
                  .Verify(c => c.DownloadFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_check_the_existing_file_against_the_remote_url()
        {
            GivenCoverUrls();

            Mocker.GetMock<ICoverExistsSpecification>()
                  .Setup(v => v.AlreadyExists(It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(true);

            Subject.HandleAsync(new ArtistRefreshCompleteEvent(_artist));

            Mocker.GetMock<ICoverExistsSpecification>()
                  .Verify(v => v.AlreadyExists("https://img.example/artist.jpg", It.Is<string>(f => f.Contains(Path.Combine("Artists", "2")))), Times.Once());
            Mocker.GetMock<ICoverExistsSpecification>()
                  .Verify(v => v.AlreadyExists("https://img.example/album.jpg", It.Is<string>(f => f.Contains(Path.Combine("Albums", "4")))), Times.Once());
        }

        [Test]
        public void should_resize_covers_if_main_downloaded()
        {
            Mocker.GetMock<ICoverExistsSpecification>()
                  .Setup(v => v.AlreadyExists(It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(false);

            Mocker.GetMock<IAlbumService>()
                  .Setup(v => v.GetAlbumsByArtist(It.IsAny<int>()))
                  .Returns(new List<Album> { _album });

            Mocker.GetMock<IDiskProvider>()
                  .Setup(v => v.FileExists(It.IsAny<string>()))
                  .Returns(true);

            Subject.HandleAsync(new ArtistRefreshCompleteEvent(_artist));

            Mocker.GetMock<IImageResizer>()
                  .Verify(v => v.Resize(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Exactly(2));
        }

        [Test]
        public void should_resize_covers_if_missing()
        {
            Mocker.GetMock<ICoverExistsSpecification>()
                  .Setup(v => v.AlreadyExists(It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(true);

            Mocker.GetMock<IAlbumService>()
                  .Setup(v => v.GetAlbumsByArtist(It.IsAny<int>()))
                  .Returns(new List<Album> { _album });

            Mocker.GetMock<IDiskProvider>()
                  .Setup(v => v.FileExists(It.IsAny<string>()))
                  .Returns(false);

            Subject.HandleAsync(new ArtistRefreshCompleteEvent(_artist));

            Mocker.GetMock<IImageResizer>()
                  .Verify(v => v.Resize(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Exactly(2));
        }

        [Test]
        public void should_not_resize_covers_if_exists()
        {
            Mocker.GetMock<ICoverExistsSpecification>()
                  .Setup(v => v.AlreadyExists(It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(v => v.FileExists(It.IsAny<string>()))
                  .Returns(true);

            Mocker.GetMock<IAlbumService>()
                  .Setup(v => v.GetAlbumsByArtist(It.IsAny<int>()))
                  .Returns(new List<Album> { _album });

            Mocker.GetMock<IDiskProvider>()
                  .Setup(v => v.GetFileSize(It.IsAny<string>()))
                  .Returns(1000);

            Subject.HandleAsync(new ArtistRefreshCompleteEvent(_artist));

            Mocker.GetMock<IImageResizer>()
                  .Verify(v => v.Resize(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_resize_covers_if_existing_is_empty()
        {
            Mocker.GetMock<ICoverExistsSpecification>()
                  .Setup(v => v.AlreadyExists(It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(v => v.FileExists(It.IsAny<string>()))
                  .Returns(true);

            Mocker.GetMock<IAlbumService>()
                  .Setup(v => v.GetAlbumsByArtist(It.IsAny<int>()))
                  .Returns(new List<Album> { _album });

            Mocker.GetMock<IDiskProvider>()
                  .Setup(v => v.GetFileSize(It.IsAny<string>()))
                  .Returns(0);

            Subject.HandleAsync(new ArtistRefreshCompleteEvent(_artist));

            Mocker.GetMock<IImageResizer>()
                  .Verify(v => v.Resize(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Exactly(2));
        }

        [Test]
        public void should_log_error_if_resize_failed()
        {
            Mocker.GetMock<ICoverExistsSpecification>()
                  .Setup(v => v.AlreadyExists(It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(v => v.FileExists(It.IsAny<string>()))
                  .Returns(false);

            Mocker.GetMock<IAlbumService>()
                  .Setup(v => v.GetAlbumsByArtist(It.IsAny<int>()))
                  .Returns(new List<Album> { _album });

            Mocker.GetMock<IImageResizer>()
                  .Setup(v => v.Resize(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                  .Throws<ApplicationException>();

            Subject.HandleAsync(new ArtistRefreshCompleteEvent(_artist));

            Mocker.GetMock<IImageResizer>()
                  .Verify(v => v.Resize(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Exactly(2));
        }
    }
}
