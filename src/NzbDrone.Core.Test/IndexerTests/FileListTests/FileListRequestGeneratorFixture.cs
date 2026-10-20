using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Movies.Indexers.Legacy.FileList;
using NzbDrone.Core.Movies.Search;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerTests.FileListTests
{
    public class FileListRequestGeneratorFixture : CoreTest<FileListRequestGenerator>
    {
        private MovieSearchRequest _movieSearchCriteria;

        [SetUp]
        public void Setup()
        {
            Subject.Settings = new FileListSettings()
            {
                BaseUrl = "http://127.0.0.1:1234/",
                Categories = new[] { 1, 2 },
                Passkey = "abcd",
                Username = "somename"
            };

            _movieSearchCriteria = new MovieSearchRequest
            {
                Movie = new Movies.Movie { ImdbId = "tt0076759", Title = "Star Wars", Year = 1977 },
                SceneTitles = new List<string> { "Star Wars" }
            };
        }

        private void MovieWithoutIMDB()
        {
            _movieSearchCriteria.Movie.ImdbId = null;
        }

        [Test]
        public void should_use_categories_for_feed()
        {
            var results = Subject.GetRecentRequests();

            results.GetAllTiers().Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("&category=1,2");
        }

        [Test]
        public void should_not_search_by_imdbid_if_not_supported()
        {
            var results = Subject.GetSearchRequests(_movieSearchCriteria);

            results.GetAllTiers().Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("type=imdb");
            page.Url.Query.Should().Contain("query=tt0076759");
        }

        [Test]
        public void should_search_by_name_and_year_if_missing_imdbid()
        {
            MovieWithoutIMDB();

            var results = Subject.GetSearchRequests(_movieSearchCriteria);

            results.GetAllTiers().Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("type=name");
            page.Url.Query.Should().Contain("query=Star+Wars+1977");
        }
    }
}
