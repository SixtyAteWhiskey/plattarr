using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Config;

namespace NzbDrone.Api.Test.v3.Config
{
    [TestFixture]
    public class IndexerConfigResourceFixture : TestBase
    {
        [TestCase(true)]
        [TestCase(false)]
        public void should_expose_the_anime_standard_format_search_value(bool value)
        {
            var config = new Mock<IConfigService>();
            config.SetupGet(c => c.AnimeStandardFormatSearch).Returns(value);

            var resource = IndexerConfigResourceMapper.ToResource(config.Object);

            resource.AnimeStandardFormatSearch.Should().Be(value);
        }

        [Test]
        public void should_still_map_the_movie_values()
        {
            var config = new Mock<IConfigService>();
            config.SetupGet(c => c.PreferIndexerFlags).Returns(true);
            config.SetupGet(c => c.AvailabilityDelay).Returns(7);
            config.SetupGet(c => c.AllowHardcodedSubs).Returns(true);
            config.SetupGet(c => c.WhitelistedHardcodedSubs).Returns("en,fr");

            var resource = IndexerConfigResourceMapper.ToResource(config.Object);

            resource.PreferIndexerFlags.Should().BeTrue();
            resource.AvailabilityDelay.Should().Be(7);
            resource.AllowHardcodedSubs.Should().BeTrue();
            resource.WhitelistedHardcodedSubs.Should().Be("en,fr");
            resource.AnimeStandardFormatSearch.Should().BeFalse();
        }
    }
}
