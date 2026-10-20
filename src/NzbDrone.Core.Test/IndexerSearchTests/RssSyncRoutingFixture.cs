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
    public class RssSyncRoutingFixture : TestBase
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
            RssSyncService.BelongsTo(WithCategories(ids), Movies).Should().BeTrue();
        }

        [TestCase(5000)]
        [TestCase(5070)]
        [TestCase(3010)]
        [TestCase(1999)]
        [TestCase(3000)]
        public void should_not_route_other_categories_to_movies(int id)
        {
            RssSyncService.BelongsTo(WithCategories(id), Movies).Should().BeFalse();
        }

        [Test]
        public void should_offer_uncategorized_release_to_every_media_type()
        {
            RssSyncService.BelongsTo(new ReleaseInfo(), Movies).Should().BeTrue();
            RssSyncService.BelongsTo(WithCategories(), Movies).Should().BeTrue();
        }

        [Test]
        public void should_treat_custom_categories_only_as_uncategorized()
        {
            RssSyncService.BelongsTo(WithCategories(100001, 100002), Movies).Should().BeTrue();
        }

        [Test]
        public void should_route_multi_category_release_when_any_matches()
        {
            RssSyncService.BelongsTo(WithCategories(5000, 2000), Movies).Should().BeTrue();
        }
    }
}
