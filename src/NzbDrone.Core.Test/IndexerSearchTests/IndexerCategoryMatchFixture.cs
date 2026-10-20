using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.IndexerSearchTests
{
    [TestFixture]
    public class IndexerCategoryMatchFixture : TestBase
    {
        private static readonly int[] Movies = { 2000 };

        private static ReleaseInfo WithCategories(params int[] ids)
        {
            return new ReleaseInfo { Categories = ids.Select(id => new IndexerCategory(id, id.ToString())).ToList() };
        }

        [TestCase(2000)]
        [TestCase(2040)]
        [TestCase(2045, 100045)]
        public void should_route_movie_categories_to_movies(params int[] ids)
        {
            IndexerCategoryService.Matches(WithCategories(ids), Movies).Should().BeTrue();
        }

        [TestCase(5000)]
        [TestCase(5070)]
        [TestCase(3010)]
        [TestCase(1999)]
        [TestCase(3000)]
        public void should_not_route_other_categories_to_movies(int id)
        {
            IndexerCategoryService.Matches(WithCategories(id), Movies).Should().BeFalse();
        }

        [Test]
        public void should_offer_uncategorized_release_to_every_media_type()
        {
            IndexerCategoryService.Matches(new ReleaseInfo(), Movies).Should().BeTrue();
            IndexerCategoryService.Matches(WithCategories(), Movies).Should().BeTrue();
        }

        [Test]
        public void should_treat_custom_categories_only_as_uncategorized()
        {
            IndexerCategoryService.Matches(WithCategories(100001, 100002), Movies).Should().BeTrue();
        }

        [Test]
        public void should_not_route_release_with_only_other_standard_and_custom_categories()
        {
            IndexerCategoryService.Matches(WithCategories(5000, 100045), Movies).Should().BeFalse();
        }

        [Test]
        public void should_match_exact_custom_category_from_an_override()
        {
            IndexerCategoryService.Matches(WithCategories(5000, 100045), new[] { 100045 }).Should().BeTrue();
        }

        [Test]
        public void should_match_exact_subcategory_override_without_its_siblings()
        {
            IndexerCategoryService.Matches(WithCategories(2040), new[] { 2045 }).Should().BeFalse();
            IndexerCategoryService.Matches(WithCategories(2045), new[] { 2045 }).Should().BeTrue();
        }

        [Test]
        public void should_route_multi_category_release_when_any_matches()
        {
            IndexerCategoryService.Matches(WithCategories(5000, 2000), Movies).Should().BeTrue();
        }
    }
}
