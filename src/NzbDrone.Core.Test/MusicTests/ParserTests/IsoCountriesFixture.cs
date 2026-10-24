using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Music.Parser;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.ParserTests
{
    [TestFixture]
    public class IsoCountriesFixture : CoreTest
    {
        [TestCase("US", "United States")]
        [TestCase("us", "United States")]
        [TestCase("GB", "United Kingdom")]
        [TestCase("Germany", "Germany")]
        [TestCase("germany", "Germany")]
        [TestCase("USA", "United States")]
        [TestCase("GBR", "United Kingdom")]
        [TestCase("deu", "Germany")]
        public void should_find_a_country_by_code_or_name(string value, string expectedName)
        {
            IsoCountries.Find(value).Name.Should().Be(expectedName);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        [TestCase("ZZ")]
        [TestCase("Atlantis")]
        public void should_not_find_an_unknown_country(string value)
        {
            IsoCountries.Find(value).Should().BeNull();
        }
    }
}
