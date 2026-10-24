using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Common.Test.ExtensionTests.IEnumerableExtensionTests
{
    [TestFixture]
    public class ExclusiveOrDefaultFixture
    {
        [Test]
        public void should_return_the_only_item()
        {
            new[] { "a" }.ExclusiveOrDefault().Should().Be("a");
        }

        [Test]
        public void should_return_default_for_an_empty_sequence()
        {
            Array.Empty<string>().ExclusiveOrDefault().Should().BeNull();
            Array.Empty<int>().ExclusiveOrDefault().Should().Be(0);
        }

        [Test]
        public void should_return_default_when_there_is_more_than_one_item()
        {
            new[] { "a", "b" }.ExclusiveOrDefault().Should().BeNull();
        }

        [Test]
        public void should_return_the_only_item_that_matches_the_predicate()
        {
            new[] { 1, 2, 3 }.ExclusiveOrDefault(x => x > 2).Should().Be(3);
        }

        [Test]
        public void should_return_default_when_no_item_or_several_items_match_the_predicate()
        {
            new[] { 1, 2, 3 }.ExclusiveOrDefault(x => x > 5).Should().Be(0);
            new[] { 1, 2, 3 }.ExclusiveOrDefault(x => x > 1).Should().Be(0);
        }

        [Test]
        public void should_throw_for_a_null_source_or_predicate()
        {
            IEnumerable<string> source = null;

            Assert.Throws<ArgumentNullException>(() => source.ExclusiveOrDefault());
            Assert.Throws<ArgumentNullException>(() => source.ExclusiveOrDefault(x => true));
            Assert.Throws<ArgumentNullException>(() => new[] { "a" }.ExclusiveOrDefault(null));
        }
    }
}
