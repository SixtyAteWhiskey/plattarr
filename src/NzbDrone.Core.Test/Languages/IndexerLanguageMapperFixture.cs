using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Languages
{
    [TestFixture]
    public class IndexerLanguageMapperFixture : CoreTest
    {
        [Test]
        public void language_names_should_be_unique()
        {
            Language.All.Select(l => l.Name.ToLowerInvariant()).Should().OnlyHaveUniqueItems();
        }

        [Test]
        public void should_round_trip_every_language()
        {
            var names = IndexerLanguageMapper.ToNames(Language.All);

            IndexerLanguageMapper.Map(names).Should().Equal(Language.All);
        }

        [TestCase("English")]
        [TestCase("english")]
        [TestCase(" English ")]
        public void should_map_by_name_ignoring_case_and_whitespace(string name)
        {
            IndexerLanguageMapper.Map(name).Should().Be(Language.English);
        }

        [Test]
        public void should_fall_back_to_iso_english_name()
        {
            // Not a Language.Name; IsoLanguages lists it as the English name for "nb".
            IndexerLanguageMapper.Map("Norwegian Bokmal").Should().Be(Language.Norwegian);
        }

        [TestCase("Klingon")]
        [TestCase("")]
        [TestCase(null)]
        public void should_return_null_for_unknown_name(string name)
        {
            IndexerLanguageMapper.Map(name).Should().BeNull();
        }

        [Test]
        public void should_drop_unknown_and_duplicate_names()
        {
            IndexerLanguageMapper.Map(new List<string> { "German", "Klingon", "german" })
                .Should().Equal(Language.German);
        }

        [Test]
        public void should_return_empty_list_for_null()
        {
            IndexerLanguageMapper.Map((IEnumerable<string>)null).Should().BeEmpty();
            IndexerLanguageMapper.ToNames(null).Should().BeEmpty();
        }
    }
}
