using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Music.Organizer;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.OrganizerTests
{
    [TestFixture]
    public class CleanFixture : CoreTest
    {
        [TestCase("Law & Order: Criminal Intent - S10E07 - Icarus [HDTV-720p]",
            "Law & Order - Criminal Intent - S10E07 - Icarus [HDTV-720p]")]
        public void CleanFileName(string name, string expectedName)
        {
            FileNameBuilder.CleanFileName(name).Should().Be(expectedName);
        }
    }
}
