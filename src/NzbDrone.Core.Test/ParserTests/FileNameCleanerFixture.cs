using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class FileNameCleanerFixture
    {
        [TestCase("Mission: Impossible", "Mission - Impossible")]
        [TestCase("Mission:Impossible", "Mission-Impossible")]
        [TestCase("Who/What\\Where", "Who+What+Where")]
        [TestCase("Is it? Yes*|\"<>", "Is it! Yes-")]
        public void smart_clean_should_replace_illegal_characters(string name, string expected)
        {
            FileNameCleaner.Clean(name).Should().Be(expected);
        }

        [TestCase(null, "Mission: Impossible", "Mission - Impossible")]
        [TestCase("-", "Mission: Impossible", "Mission- Impossible")]
        [TestCase(" -", "Mission: Impossible", "Mission - Impossible")]
        [TestCase(" - ", "Mission: Impossible", "Mission -  Impossible")]
        [TestCase("", "Mission: Impossible", "Mission Impossible")]
        public void should_use_the_requested_colon_replacement(string colonReplacement, string name, string expected)
        {
            FileNameCleaner.Clean(name, true, colonReplacement).Should().Be(expected);
        }

        [Test]
        public void should_remove_instead_of_replace_when_replacing_is_off()
        {
            FileNameCleaner.Clean("Mission: Impossible/Fallout?", false, " - ").Should().Be("Mission ImpossibleFallout");
        }

        [TestCase(".45 (2006)", "45 (2006)")]
        [TestCase(" The Title ", "The Title")]
        public void should_trim_leading_periods_and_surrounding_spaces(string name, string expected)
        {
            FileNameCleaner.Clean(name).Should().Be(expected);
        }
    }
}
