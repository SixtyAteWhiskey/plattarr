using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.IndexerProxies;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.DecisionEngine.Specifications.RssSync;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Movies.Search;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.DecisionEngineTests.RssSync
{
    [TestFixture]
    public class IndexerTagSpecificationFixture : CoreTest<IndexerTagSpecification>
    {
        private IndexerTagSpecification _specification;

        private RemoteMovie _parseResultMulti;
        private IndexerDefinition _fakeIndexerDefinition;
        private Movie _fakeMovie;
        private ReleaseInfo _fakeRelease;
        private List<IndexerProxyDefinition> _proxies = new();

        [SetUp]
        public void Setup()
        {
            _fakeIndexerDefinition = new IndexerDefinition
            {
                Tags = new HashSet<int>()
            };

            Mocker
                .GetMock<IIndexerFactory>()
                .Setup(m => m.Find(1))
                .Returns(_fakeIndexerDefinition);

            Mocker
                .GetMock<IIndexerProxyFactory>()
                .Setup(m => m.All())
                .Returns(() => _proxies);

            Mocker.SetConstant<IIndexerMediaTagService>(Mocker.Resolve<IndexerMediaTagService>());

            _specification = Mocker.Resolve<IndexerTagSpecification>();

            _fakeMovie = Builder<Movie>.CreateNew()
                .With(c => c.Monitored = true)
                .With(c => c.Tags = new HashSet<int>())
                .Build();

            _fakeRelease = new ReleaseInfo
            {
                IndexerId = 1
            };

            _parseResultMulti = new RemoteMovie
            {
                Movie = _fakeMovie,
                Release = _fakeRelease
            };
        }

        [Test]
        public void indexer_proxy_tag_should_not_restrict_movies()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int> { 7 };
            _proxies = new List<IndexerProxyDefinition> { new() { Id = 1, Tags = new HashSet<int> { 7 } } };
            _fakeMovie.Tags = new HashSet<int>();

            _specification.IsSatisfiedBy(_parseResultMulti, new()).Accepted.Should().BeTrue();
        }

        [Test]
        public void indexer_and_movie_without_tags_should_return_true()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int>();
            _fakeMovie.Tags = new HashSet<int>();

            _specification.IsSatisfiedBy(_parseResultMulti, new ReleaseDecisionInformation(false, new MovieSearchRequest())).Accepted.Should().BeTrue();
        }

        [Test]
        public void indexer_with_tags_movie_without_tags_should_return_false()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int> { 123 };
            _fakeMovie.Tags = new HashSet<int>();

            _specification.IsSatisfiedBy(_parseResultMulti, new ReleaseDecisionInformation(false, new MovieSearchRequest())).Accepted.Should().BeFalse();
        }

        [Test]
        public void indexer_without_tags_movie_with_tags_should_return_true()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int>();
            _fakeMovie.Tags = new HashSet<int> { 123 };

            _specification.IsSatisfiedBy(_parseResultMulti, new ReleaseDecisionInformation(false, new MovieSearchRequest())).Accepted.Should().BeTrue();
        }

        [Test]
        public void indexer_with_tags_movie_with_matching_tags_should_return_true()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int> { 123, 456 };
            _fakeMovie.Tags = new HashSet<int> { 123, 789 };

            _specification.IsSatisfiedBy(_parseResultMulti, new ReleaseDecisionInformation(false, new MovieSearchRequest())).Accepted.Should().BeTrue();
        }

        [Test]
        public void indexer_with_tags_movie_with_different_tags_should_return_false()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int> { 456 };
            _fakeMovie.Tags = new HashSet<int> { 123, 789 };

            _specification.IsSatisfiedBy(_parseResultMulti, new ReleaseDecisionInformation(false, new MovieSearchRequest())).Accepted.Should().BeFalse();
        }

        [Test]
        public void release_without_indexerid_should_return_true()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int> { 456 };
            _fakeMovie.Tags = new HashSet<int> { 123, 789 };
            _fakeRelease.IndexerId = 0;

            _specification.IsSatisfiedBy(_parseResultMulti, new ReleaseDecisionInformation(false, new MovieSearchRequest { MonitoredEpisodesOnly = true })).Accepted.Should().BeTrue();
        }

        [Test]
        public void release_with_invalid_indexerid_should_return_true()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int> { 456 };
            _fakeMovie.Tags = new HashSet<int> { 123, 789 };
            _fakeRelease.IndexerId = 2;

            _specification.IsSatisfiedBy(_parseResultMulti, new ReleaseDecisionInformation(false, new MovieSearchRequest { MonitoredEpisodesOnly = true })).Accepted.Should().BeTrue();
        }
    }
}
