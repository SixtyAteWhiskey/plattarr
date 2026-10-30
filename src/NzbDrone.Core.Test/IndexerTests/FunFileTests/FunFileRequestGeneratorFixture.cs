using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Definitions;
using NzbDrone.Core.Indexers.Settings;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace NzbDrone.Core.Test.IndexerTests.FunFileTests
{
    [TestFixture]
    public class FunFileRequestGeneratorFixture
    {
        private FunFileRequestGenerator _generator;

        [SetUp]
        public void SetUp()
        {
            var capabilities = new IndexerCapabilities();
            capabilities.Categories.AddCategoryMapping(19, NewznabStandardCategory.Movies, "Movies");

            _generator = new FunFileRequestGenerator(
                new UserPassTorrentBaseSettings { BaseUrl = "https://www.funfile.org/" },
                capabilities);
        }

        [Test]
        public void should_keep_title_search_and_category_for_movie_terms()
        {
            var request = _generator.GetSearchRequests(new MovieSearchCriteria
            {
                SearchTerm = "Example Film",
                Categories = new[] { NewznabStandardCategory.Movies.Id }
            }).GetAllTiers().Single().Single();

            request.Url.FullUri.Should().StartWith("https://www.funfile.org/browse.php?");
            request.Url.Query.Should().Contain("search=Example+Film");
            request.Url.Query.Should().Contain("c19=1");
            request.Url.Query.Should().Contain("s_title=1");
            request.Url.Query.Should().NotContain("s_desc=");
        }

        [Test]
        public void should_keep_title_and_description_search_for_imdb_lookups()
        {
            var request = _generator.GetSearchRequests(new MovieSearchCriteria
            {
                SearchTerm = "Example Film",
                ImdbId = "0076759",
                Categories = new[] { NewznabStandardCategory.Movies.Id }
            }).GetAllTiers().Single().Single();

            request.Url.Query.Should().Contain("search=tt0076759");
            request.Url.Query.Should().Contain("c19=1");
            request.Url.Query.Should().Contain("s_title=1");
            request.Url.Query.Should().Contain("s_desc=1");
        }
    }
}
