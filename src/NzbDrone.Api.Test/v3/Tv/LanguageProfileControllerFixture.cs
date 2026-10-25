using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Tv.LanguageProfiles;

namespace NzbDrone.Api.Test.v3.Tv
{
    [TestFixture]
    public class LanguageProfileControllerFixture : TestBase<LanguageProfileController>
    {
        [Test]
        public void should_return_the_language_profile_used_by_series()
        {
            var profile = Subject.GetLanguageProfiles().Should().ContainSingle().Which;

            profile.Id.Should().Be(1);
            profile.Name.Should().Be("English");
        }
    }
}
