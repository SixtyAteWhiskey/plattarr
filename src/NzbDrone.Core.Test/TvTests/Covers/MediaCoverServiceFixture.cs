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
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Covers;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.Test.TvTests.Covers
{
    // Series covers live in MediaCover/Series/{id}. Movies keep MediaCover/{id}, so a series and a movie with the
    // same id must never resolve to the same file.
    [TestFixture]
    public class MediaCoverServiceFixture : CoreTest<MediaCoverService>
    {
        private Series _series;
        private string _seriesFolder;

        [SetUp]
        public void Setup()
        {
            var appFolderInfo = new AppFolderInfo(Mocker.Resolve<IStartupContext>());

            Mocker.SetConstant<IAppFolderInfo>(appFolderInfo);

            _seriesFolder = Path.Combine(appFolderInfo.GetAppDataPath(), "MediaCover", "Series", "2");

            _series = Builder<Series>.CreateNew()
                .With(v => v.Id = 2)
                .With(v => v.Added = DateTime.UtcNow)
                .With(v => v.Images = new List<MediaCover.MediaCover>
                {
                    new(MediaCoverTypes.Poster, "https://artworks.examples.com/posters/1.jpg")
                })
                .Build();
        }

        [Test]
        public void should_convert_cover_urls_to_the_series_folder()
        {
            var covers = new List<MediaCover.MediaCover>
            {
                new() { CoverType = MediaCoverTypes.Banner, RemoteUrl = "https://artworks.examples.com/banners/1.jpg" }
            };

            Mocker.GetMock<IDiskProvider>()
                  .Setup(v => v.FileExists(It.IsAny<string>()))
                  .Returns(true);

            Subject.ConvertToLocalUrls(12, covers, DateTime.UtcNow);

            covers.Single().Url.Should().Be("/MediaCover/Series/12/banner.jpg?h=a6210a45e2b93963ad9e");
        }

        [Test]
        public void should_convert_clearlogo_url_to_png_in_the_series_folder()
        {
            var covers = new List<MediaCover.MediaCover>
            {
                new() { CoverType = MediaCoverTypes.Clearlogo }
            };

            Subject.ConvertToLocalUrls(12, covers, DateTime.UtcNow);

            covers.Single().Url.Should().Be("/MediaCover/Series/12/clearlogo.png");
        }

        [Test]
        public void should_map_cover_urls_through_the_proxy_for_a_series_that_is_not_added_yet()
        {
            var covers = new List<MediaCover.MediaCover>
            {
                new() { CoverType = MediaCoverTypes.Poster, RemoteUrl = "https://artworks.examples.com/posters/1.jpg" }
            };

            Mocker.GetMock<IMediaCoverProxy>()
                  .Setup(v => v.RegisterUrl("https://artworks.examples.com/posters/1.jpg"))
                  .Returns("/MediaCoverProxy/abc/1.jpg");

            Subject.ConvertToLocalUrls(0, covers);

            covers.Single().Url.Should().Be("/MediaCoverProxy/abc/1.jpg");
        }

        [Test]
        public void should_put_cover_files_in_the_series_folder()
        {
            Subject.GetCoverPath(2, MediaCoverTypes.Poster)
                   .Should().Be(Path.Combine(_seriesFolder, "poster.jpg"));

            Subject.GetCoverPath(2, MediaCoverTypes.Clearlogo)
                   .Should().Be(Path.Combine(_seriesFolder, "clearlogo.png"));

            Subject.GetCoverPath(2, MediaCoverTypes.Poster, 250)
                   .Should().Be(Path.Combine(_seriesFolder, "poster-250.jpg"));
        }

        [Test]
        public void should_download_and_resize_covers_in_the_series_folder()
        {
            Mocker.GetMock<ICoverExistsSpecification>()
                  .Setup(v => v.AlreadyExists(It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(false);

            Subject.HandleAsync(new SeriesUpdatedEvent(_series));

            Mocker.GetMock<IHttpClient>()
                  .Verify(v => v.DownloadFile("https://artworks.examples.com/posters/1.jpg", Path.Combine(_seriesFolder, "poster.jpg")), Times.Once());

            Mocker.GetMock<IImageResizer>()
                  .Verify(v => v.Resize(Path.Combine(_seriesFolder, "poster.jpg"), Path.Combine(_seriesFolder, "poster-500.jpg"), 500), Times.Once());

            Mocker.GetMock<IImageResizer>()
                  .Verify(v => v.Resize(Path.Combine(_seriesFolder, "poster.jpg"), Path.Combine(_seriesFolder, "poster-250.jpg"), 250), Times.Once());
        }

        [Test]
        public void should_check_for_an_existing_cover_in_the_series_folder()
        {
            Subject.HandleAsync(new SeriesUpdatedEvent(_series));

            Mocker.GetMock<ICoverExistsSpecification>()
                  .Verify(v => v.AlreadyExists("https://artworks.examples.com/posters/1.jpg", Path.Combine(_seriesFolder, "poster.jpg")), Times.Once());
        }

        [Test]
        public void should_delete_only_the_series_cover_folder_when_a_series_is_deleted()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(v => v.FolderExists(It.IsAny<string>()))
                  .Returns(true);

            Subject.HandleAsync(new SeriesDeletedEvent(new List<Series> { _series }, false, false));

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.DeleteFolder(_seriesFolder, true), Times.Once());

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.DeleteFolder(Path.GetDirectoryName(_seriesFolder), It.IsAny<bool>()), Times.Never());

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.DeleteFolder(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(_seriesFolder)), "2"), It.IsAny<bool>()), Times.Never());
        }
    }
}
