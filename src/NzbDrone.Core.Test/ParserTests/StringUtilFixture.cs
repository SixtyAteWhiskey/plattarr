using System.Collections.Generic;
using System.Collections.Specialized;
using AngleSharp.Html.Parser;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class StringUtilFixture : CoreTest
    {
        [Test]
        public void should_build_query_string_and_url_encode_values()
        {
            var query = new List<KeyValuePair<string, string>>
            {
                new("t", "search"),
                new("q", "a b&c")
            };

            query.GetQueryString().Should().Be("t=search&q=a+b%26c");
        }

        [Test]
        public void should_use_custom_separator()
        {
            var query = new List<KeyValuePair<string, string>>
            {
                new("a", "1"),
                new("b", "2")
            };

            query.GetQueryString(separator: ";").Should().Be("a=1;b=2");
        }

        [Test]
        public void should_join_multi_value_keys_when_not_duplicating()
        {
            var collection = new NameValueCollection
            {
                { "cat", "2000" },
                { "cat", "5000" }
            };

            collection.GetQueryString().Should().Be("cat=2000%2C5000");
        }

        [Test]
        public void should_repeat_multi_value_keys_when_duplicating()
        {
            var collection = new NameValueCollection
            {
                { "cat", "2000" },
                { "cat", "5000" }
            };

            collection.GetQueryString(duplicateKeysIfMulti: true).Should().Be("cat=2000&cat=5000");
        }

        [TestCase("Movie: The Sequel", "Movie - The Sequel")]
        [TestCase("Movie: The Sequel?", "Movie - The Sequel!")]
        [TestCase("AC/DC", "AC+DC")]
        [TestCase(" .Hidden Name ", "Hidden Name")]
        public void should_clean_file_name(string name, string expected)
        {
            StringUtil.CleanFileName(name).Should().Be(expected);
        }

        [Test]
        public void should_strip_bad_characters_without_replacement()
        {
            StringUtil.CleanFileName("Movie: The Sequel?", false).Should().Be("Movie The Sequel");
        }

        [Test]
        public void should_replace_slash_with_fraction_slash_in_fancy_mode()
        {
            StringUtil.MakeValidFileName("AC/DC").Should().Be("AC⁄DC");
        }

        [Test]
        public void should_return_underscore_when_file_name_is_empty()
        {
            StringUtil.MakeValidFileName("/", null, false).Should().Be("_");
        }

        [TestCase("The Lord of the Rings: The Return of the King", "lord rings return king")]
        [TestCase("Some_Indexer-Name.Here", "some indexer name here")]
        public void should_normalize_sort_title(string title, string expected)
        {
            title.NormalizeSortTitle().Should().Be(expected);
        }

        [Test]
        public void should_return_null_marker_for_null_element()
        {
            StringUtil.ToHtmlPretty(null).Should().Be("<NULL>");
        }

        [Test]
        public void should_pretty_print_element()
        {
            var document = new HtmlParser().ParseDocument("<div><span>x</span></div>");
            var element = document.QuerySelector("div");

            var html = element.ToHtmlPretty().Trim();

            html.Should().StartWith("<div>");
            html.Should().Contain("<span>");
            html.Should().Contain("x");
        }
    }
}
