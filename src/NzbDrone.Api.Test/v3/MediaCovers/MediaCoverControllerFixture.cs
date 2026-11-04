using System.IO;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Test.Common;
using Radarr.Api.V3.MediaCovers;

namespace NzbDrone.Api.Test.v3.MediaCovers
{
    [TestFixture]
    public class MediaCoverControllerFixture : TestBase<MediaCoverController>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IAppFolderInfo>()
                  .SetupGet(x => x.AppDataFolder)
                  .Returns("/fixture/config");
        }

        [TestCase(@"..\private.jpg")]
        [TestCase(@"C:\private.jpg")]
        [TestCase("../private.jpg")]
        [TestCase("/private.jpg")]
        public void should_reject_cover_filename_that_can_escape_on_windows(string filename)
        {
            Subject.GetMediaCover(1, filename).Result.Should().BeOfType<NotFound>();
            Subject.GetSeriesMediaCover(1, filename).Result.Should().BeOfType<NotFound>();
            Subject.GetArtistMediaCover(1, filename).Result.Should().BeOfType<NotFound>();
            Subject.GetAlbumMediaCover(1, filename).Result.Should().BeOfType<NotFound>();

            Mocker.GetMock<IDiskProvider>().Verify(x => x.FileExists(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_serve_a_cover_in_its_expected_folder()
        {
            var path = Path.Combine("/fixture/config", "MediaCover", "Series", "1", "poster.jpg");
            Mocker.GetMock<IDiskProvider>().Setup(x => x.FileExists(path)).Returns(true);
            Mocker.GetMock<IDiskProvider>().Setup(x => x.GetFileSize(path)).Returns(100);

            Subject.GetSeriesMediaCover(1, "poster.jpg").Result.Should().BeOfType<PhysicalFileHttpResult>();
        }

        [TestCase("jpg")]
        [TestCase("png")]
        [TestCase("webp")]
        public void should_serve_original_when_resized_cover_is_unavailable(string extension)
        {
            var original = Path.Combine("/fixture/config", "MediaCover", "Artists", "1", "logo." + extension);
            Mocker.GetMock<IDiskProvider>().Setup(x => x.FileExists(original)).Returns(true);

            var result = Subject.GetArtistMediaCover(1, "logo-250." + extension).Result;

            result.Should().BeOfType<PhysicalFileHttpResult>()
                  .Which.FileName.Should().Be(original);
        }
    }
}
