using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Profiles.Releases;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.TvTests.DecisionEngineTests
{
    // The air date and season pack settings are columns on the shared release profile table.
    [TestFixture]
    public class ReleaseProfileTvSettingsFixture : DbTest<ReleaseProfileRepository, ReleaseProfile>
    {
        [Test]
        public void should_default_the_tv_settings_to_off()
        {
            var profile = new ReleaseProfile();

            profile.AirDateRestriction.Should().BeFalse();
            profile.AirDateGracePeriod.Should().Be(0);
            profile.AllowSeasonPackWithoutAllEpisodesAired.Should().BeFalse();
        }

        [Test]
        public void should_round_trip_the_tv_settings()
        {
            Subject.Insert(new ReleaseProfile
            {
                Name = "Air date",
                Required = new List<string> { "x" },
                AirDateRestriction = true,
                AirDateGracePeriod = 12,
                AllowSeasonPackWithoutAllEpisodesAired = true
            });

            var stored = Subject.All().Single();

            stored.AirDateRestriction.Should().BeTrue();
            stored.AirDateGracePeriod.Should().Be(12);
            stored.AllowSeasonPackWithoutAllEpisodesAired.Should().BeTrue();
        }
    }
}
