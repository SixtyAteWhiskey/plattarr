using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Common.Test.ExtensionTests
{
    [TestFixture]
    public class FuzzyMatchFixture
    {
        [TestCase(null, "abc")]
        [TestCase("abc", null)]
        [TestCase("", "abc")]
        [TestCase("abc", "  ")]
        public void should_not_match_blank_input(string a, string b)
        {
            a.FuzzyMatch(b).Should().Be(0);
        }

        [Test]
        public void should_match_identical_strings_fully()
        {
            "The Black Keys".FuzzyMatch("The Black Keys").Should().Be(1);
        }

        [Test]
        public void should_score_a_single_word_by_its_levenshtein_distance()
        {
            // one substitution in three characters
            "abc".FuzzyMatch("abd").Should().BeApproximately(1.0 - (1.0 / 3), 1e-9);
        }

        [Test]
        public void should_penalise_reordered_words_by_their_distance()
        {
            // Every word matches exactly; "the" moves two places, "black" and "keys" one each:
            // (1 - 2/3) + (1 - 1/3) + (1 - 1/3) = 5/3 in both directions, over six words = 5/9.
            // That beats the whole-string Levenshtein coefficient (6/14).
            "the black keys".FuzzyMatch("black keys the").Should().BeApproximately(5.0 / 9, 1e-9);
        }

        [Test]
        public void should_use_the_whole_string_score_when_it_is_higher_than_the_word_score()
        {
            "black keys".FuzzyMatch("black keyz").Should().BeApproximately(0.9, 1e-9);
        }
    }
}
