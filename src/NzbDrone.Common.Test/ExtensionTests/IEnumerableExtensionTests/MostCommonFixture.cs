using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Common.Test.ExtensionTests.IEnumerableExtensionTests
{
    [TestFixture]
    public class MostCommonFixture
    {
        [Test]
        public void should_return_the_most_frequent_item()
        {
            new[] { "a", "b", "b", "c", "b", "a" }.MostCommon().Should().Be("b");
        }

        [Test]
        public void should_return_the_only_item_of_a_single_item_list()
        {
            new[] { 7 }.MostCommon().Should().Be(7);
        }

        [Test]
        public void should_return_the_first_seen_item_on_a_tie()
        {
            new[] { "x", "y", "y", "x" }.MostCommon().Should().Be("x");
        }

        [Test]
        public void should_project_before_counting()
        {
            var items = new[] { "ab", "cd", "ef", "g" };

            items.MostCommon(s => s.Length).Should().Be(2);
        }

        [Test]
        public void should_throw_for_an_empty_list()
        {
            Action act = () => new List<int>().MostCommon();

            act.Should().Throw<InvalidOperationException>();
        }
    }
}
