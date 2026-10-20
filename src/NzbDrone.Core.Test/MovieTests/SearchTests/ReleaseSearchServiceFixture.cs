using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Search;
using NzbDrone.Core.Movies.Translations;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MovieTests.SearchTests
{
    // Indexer selection (tags, enable flags, categories) moved to IndexerReleaseFetcher in
    // porting step 7; see IndexerSearchTests/IndexerReleaseFetcherFixture.
    [TestFixture]
    public class ReleaseSearchServiceFixture : CoreTest<Movies.Search.ReleaseSearchService>
    {
        private Movie _movie;
        private int _indexersSearched;

        [SetUp]
        public void SetUp()
        {
            _movie = Builder<Movie>.CreateNew()
                .With(v => v.Monitored = true)
                .With(v => v.Tags = new HashSet<int> { 3 })
                .Build();

            _indexersSearched = 1;

            Mocker.GetMock<IIndexerReleaseFetcher>()
                  .Setup(f => f.Search(It.IsAny<MovieSearchRequest>(), It.IsAny<IEnumerable<int>>(), It.IsAny<int[]>(), It.IsAny<MovieSearchTranslator>()))
                  .Returns(() => Task.FromResult(new IndexerSearchResult(new List<ReleaseInfo>(), _indexersSearched)));

            Mocker.GetMock<IMakeDownloadDecision>()
                  .Setup(s => s.GetSearchDecision(It.IsAny<List<ReleaseInfo>>(), It.IsAny<MovieSearchRequest>()))
                  .Returns(new List<DownloadDecision>());

            Mocker.GetMock<IMovieTranslationService>()
                  .Setup(s => s.GetAllTranslationsForMovieMetadata(It.IsAny<int>()))
                  .Returns(new List<MovieTranslation>());
        }

        [Test]
        public async Task should_pass_movie_tags_and_movie_categories_to_the_indexer_layer()
        {
            await Subject.MovieSearch(_movie, true, false);

            Mocker.GetMock<IIndexerReleaseFetcher>()
                  .Verify(f => f.Search(It.Is<MovieSearchRequest>(r => r.Movie == _movie && !r.InteractiveSearch),
                                        It.Is<IEnumerable<int>>(t => t.SequenceEqual(new[] { 3 })),
                                        It.Is<int[]>(c => c.SequenceEqual(new[] { 2000 })),
                                        It.IsAny<MovieSearchTranslator>()),
                          Times.Once());
        }

        [Test]
        public async Task should_update_last_search_time_when_an_indexer_was_searched()
        {
            await Subject.MovieSearch(_movie, true, false);

            Mocker.GetMock<IMovieService>().Verify(s => s.UpdateLastSearchTime(_movie), Times.Once());
        }

        [Test]
        public async Task should_not_update_last_search_time_when_no_indexer_was_searched()
        {
            _indexersSearched = 0;

            await Subject.MovieSearch(_movie, true, false);

            Mocker.GetMock<IMovieService>().Verify(s => s.UpdateLastSearchTime(It.IsAny<Movie>()), Times.Never());
        }
    }
}
