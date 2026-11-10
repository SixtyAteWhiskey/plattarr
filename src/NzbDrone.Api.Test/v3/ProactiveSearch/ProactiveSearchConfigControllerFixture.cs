using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using Radarr.Api.V3.ProactiveSearch;
using Radarr.Http.REST;

namespace NzbDrone.Api.Test.v3.ProactiveSearch
{
    [TestFixture]
    public class ProactiveSearchConfigControllerFixture
    {
        [Test]
        public void enabled_only_update_preserves_movie_pacing()
        {
            var config = new Mock<IConfigService>();
            config.SetupProperty(x => x.ProactiveMovieSearchEnabled, true);
            config.SetupProperty(x => x.ProactiveMovieSearchIntervalMinutes, 60);
            config.SetupProperty(x => x.ProactiveMovieActiveDownloadLimit, 5);
            config.SetupProperty(x => x.ProactiveMovieRecentGrabHours, 24);
            var controller = new ProactiveMovieSearchConfigController(config.Object);

            var result = controller.Update(1, new ProactiveMovieSearchConfigUpdateResource { Enabled = false }).Value;

            result.Enabled.Should().BeFalse();
            result.IntervalMinutes.Should().Be(60);
            result.ActiveDownloadLimit.Should().Be(5);
            result.RecentGrabHours.Should().Be(24);
        }

        [Test]
        public void invalid_fast_tv_pacing_is_rejected_before_settings_change()
        {
            var config = new Mock<IConfigService>();
            config.SetupProperty(x => x.ProactiveTvSearchEnabled, false);
            config.SetupProperty(x => x.ProactiveTvSearchIntervalMinutes, 60);
            config.SetupProperty(x => x.ProactiveTvActiveDownloadLimit, 5);
            config.SetupProperty(x => x.ProactiveTvRecentGrabHours, 24);
            var controller = new ProactiveTvSearchConfigController(config.Object);

            FluentActions.Invoking(() => controller.Update(1, new ProactiveTvSearchConfigUpdateResource
            {
                Enabled = true,
                IntervalMinutes = 1,
                ActiveDownloadLimit = 0,
                RecentGrabHours = 0
            })).Should().Throw<BadRequestException>();

            config.Object.ProactiveTvSearchEnabled.Should().BeFalse();
            config.Object.ProactiveTvSearchIntervalMinutes.Should().Be(60);
        }
    }
}
