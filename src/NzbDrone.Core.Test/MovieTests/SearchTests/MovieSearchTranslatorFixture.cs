using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Search;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MovieTests.SearchTests
{
    [TestFixture]
    public class MovieSearchTranslatorFixture : CoreTest<MovieSearchTranslator>
    {
        private MovieSearchRequest _request;
        private IndexerDefinition _indexer;

        [SetUp]
        public void SetUp()
        {
            var metadata = new MovieMetadata { TmdbId = 603, ImdbId = "tt0133093", Year = 1999, Title = "The Matrix" };

            _request = new MovieSearchRequest
            {
                Movie = new Movie { MovieMetadata = new LazyLoaded<MovieMetadata>(metadata) },
                SceneTitles = new List<string> { "The Matrix", "Matrix" }
            };

            _indexer = new IndexerDefinition { Capabilities = new IndexerCapabilities() };
        }

        private List<IReadOnlyList<SearchCriteriaBase>> Translate()
        {
            return Subject.Translate(_request, _indexer).ToList();
        }

        [Test]
        public void should_search_supported_ids_first_then_titles_with_year()
        {
            _indexer.Capabilities.MovieSearchParams = new List<MovieSearchParam> { MovieSearchParam.Q, MovieSearchParam.ImdbId, MovieSearchParam.TmdbId };

            var tiers = Translate();

            tiers.Should().HaveCount(2);

            var id = tiers[0].Should().ContainSingle().Which.Should().BeOfType<MovieSearchCriteria>().Subject;
            id.TmdbId.Should().Be(603);
            id.ImdbId.Should().Be("0133093");
            id.SearchTerm.Should().BeNull();
            id.Categories.Should().Equal(2000);

            tiers[1].Select(c => c.SearchTerm).Should().Equal("The Matrix 1999", "Matrix 1999");
            tiers[1].Should().OnlyContain(c => c is BasicSearchCriteria && c.Categories.SequenceEqual(new[] { 2000 }));
        }

        [Test]
        public void should_only_send_ids_the_indexer_supports()
        {
            _indexer.Capabilities.MovieSearchParams = new List<MovieSearchParam> { MovieSearchParam.Q, MovieSearchParam.ImdbId };

            var id = (MovieSearchCriteria)Translate()[0].Single();

            id.ImdbId.Should().Be("0133093");
            id.TmdbId.Should().BeNull();
        }

        [Test]
        public void should_skip_id_tier_when_indexer_has_no_id_search()
        {
            var tiers = Translate();

            tiers.Should().ContainSingle();
            tiers[0].Should().OnlyContain(c => c is BasicSearchCriteria);
        }

        [Test]
        public void should_skip_title_tier_without_year()
        {
            _request.Movie.MovieMetadata.Value.Year = 0;
            _indexer.Capabilities.MovieSearchParams = new List<MovieSearchParam> { MovieSearchParam.TmdbId };

            var tiers = Translate();

            tiers.Should().ContainSingle();
            tiers[0].Single().Should().BeOfType<MovieSearchCriteria>();
        }
    }
}
