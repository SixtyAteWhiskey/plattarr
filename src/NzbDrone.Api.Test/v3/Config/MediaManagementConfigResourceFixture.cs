using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Config;

namespace NzbDrone.Api.Test.v3.Config
{
    [TestFixture]
    public class MediaManagementConfigResourceFixture : TestBase
    {
        [Test]
        public void should_expose_the_tv_media_management_values()
        {
            var config = new Mock<IConfigService>();
            config.SetupGet(c => c.AutoUnmonitorPreviouslyDownloadedEpisodes).Returns(true);
            config.SetupGet(c => c.CreateEmptySeriesFolders).Returns(true);
            config.SetupGet(c => c.EpisodeTitleRequired).Returns(EpisodeTitleRequiredType.BulkSeasonReleases);
            config.SetupGet(c => c.SeasonPackUpgrade).Returns(SeasonPackUpgradeType.Threshold);
            config.SetupGet(c => c.SeasonPackUpgradeThreshold).Returns(75);
            config.SetupGet(c => c.UserRejectedExtensions).Returns("exe,bat");
            config.SetupGet(c => c.EpisodeFileDate).Returns(EpisodeFileDateType.UtcAirDate);

            var resource = MediaManagementConfigResourceMapper.ToResource(config.Object);

            resource.AutoUnmonitorPreviouslyDownloadedEpisodes.Should().BeTrue();
            resource.CreateEmptySeriesFolders.Should().BeTrue();
            resource.EpisodeTitleRequired.Should().Be(EpisodeTitleRequiredType.BulkSeasonReleases);
            resource.SeasonPackUpgrade.Should().Be(SeasonPackUpgradeType.Threshold);
            resource.SeasonPackUpgradeThreshold.Should().Be(75);
            resource.UserRejectedExtensions.Should().Be("exe,bat");
            resource.EpisodeFileDate.Should().Be(EpisodeFileDateType.UtcAirDate);
        }

        [Test]
        public void should_still_map_the_movie_values()
        {
            var config = new Mock<IConfigService>();
            config.SetupGet(c => c.AutoUnmonitorPreviouslyDownloadedMovies).Returns(true);
            config.SetupGet(c => c.CreateEmptyMovieFolders).Returns(true);

            var resource = MediaManagementConfigResourceMapper.ToResource(config.Object);

            resource.AutoUnmonitorPreviouslyDownloadedMovies.Should().BeTrue();
            resource.CreateEmptyMovieFolders.Should().BeTrue();
            resource.AutoUnmonitorPreviouslyDownloadedEpisodes.Should().BeFalse();
        }
    }
}
