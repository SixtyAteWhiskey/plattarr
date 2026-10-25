using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.IndexerProxies;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.DecisionEngine.Specifications.RssSync;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Tv.Search;

namespace NzbDrone.Core.Test.TvTests.DecisionEngineTests.RssSync
{
    [TestFixture]
    public class IndexerTagSpecificationFixture : CoreTest<IndexerTagSpecification>
    {
        private IndexerTagSpecification _specification;

        private RemoteEpisode _parseResultMulti;
        private IndexerDefinition _fakeIndexerDefinition;
        private Series _fakeSeries;
        private Episode _firstEpisode;
        private Episode _secondEpisode;
        private ReleaseInfo _fakeRelease;
        private List<IndexerProxyDefinition> _proxies;

        [SetUp]
        public void Setup()
        {
            _proxies = new List<IndexerProxyDefinition>();
            _fakeIndexerDefinition = new IndexerDefinition
            {
                Tags = new HashSet<int>()
            };

            Mocker
                .GetMock<IIndexerFactory>()
                .Setup(m => m.Get(It.IsAny<int>()))
                .Throws(new ModelNotFoundException(typeof(IndexerDefinition), -1));

            Mocker
                .GetMock<IIndexerFactory>()
                .Setup(m => m.Get(1))
                .Returns(_fakeIndexerDefinition);

            Mocker
                .GetMock<IIndexerProxyFactory>()
                .Setup(m => m.All())
                .Returns(() => _proxies);

            Mocker.SetConstant<IIndexerMediaTagService>(Mocker.Resolve<IndexerMediaTagService>());

            _specification = Mocker.Resolve<IndexerTagSpecification>();

            _fakeSeries = Builder<Series>.CreateNew()
                .With(c => c.Monitored = true)
                .With(c => c.Tags = new HashSet<int>())
                .Build();

            _fakeRelease = new ReleaseInfo
            {
                IndexerId = 1
            };

            _firstEpisode = new Episode { Monitored = true };
            _secondEpisode = new Episode { Monitored = true };

            var doubleEpisodeList = new List<Episode> { _firstEpisode, _secondEpisode };

            _parseResultMulti = new RemoteEpisode
            {
                Series = _fakeSeries,
                Episodes = doubleEpisodeList,
                Release = _fakeRelease
            };
        }

        [Test]
        public void indexer_proxy_tag_should_not_restrict_tv()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int> { 7 };
            _proxies = new List<IndexerProxyDefinition> { new() { Id = 1, Tags = new HashSet<int> { 7 } } };
            _fakeSeries.Tags = new HashSet<int>();

            _specification.IsSatisfiedBy(_parseResultMulti, new()).Accepted.Should().BeTrue();
        }

        [Test]
        public void indexer_media_tag_should_still_restrict_tv_with_proxy_tag()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int> { 7, 8 };
            _proxies = new List<IndexerProxyDefinition> { new() { Id = 1, Tags = new HashSet<int> { 7 } } };
            _fakeSeries.Tags = new HashSet<int>();

            var decision = _specification.IsSatisfiedBy(_parseResultMulti, new());

            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(DownloadRejectionReason.NoMatchingTag);
        }

        [Test]
        public void indexer_and_series_without_tags_should_return_true()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int>();
            _fakeSeries.Tags = new HashSet<int>();

            _specification.IsSatisfiedBy(_parseResultMulti, new ReleaseDecisionInformation(false, new SingleEpisodeSearchRequest { MonitoredEpisodesOnly = true })).Accepted.Should().BeTrue();
        }

        [Test]
        public void indexer_with_tags_series_without_tags_should_return_false()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int> { 123 };
            _fakeSeries.Tags = new HashSet<int>();

            _specification.IsSatisfiedBy(_parseResultMulti, new ReleaseDecisionInformation(false, new SingleEpisodeSearchRequest { MonitoredEpisodesOnly = true })).Accepted.Should().BeFalse();
        }

        [Test]
        public void indexer_without_tags_series_with_tags_should_return_true()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int>();
            _fakeSeries.Tags = new HashSet<int> { 123 };

            _specification.IsSatisfiedBy(_parseResultMulti, new ReleaseDecisionInformation(false, new SingleEpisodeSearchRequest { MonitoredEpisodesOnly = true })).Accepted.Should().BeTrue();
        }

        [Test]
        public void indexer_with_tags_series_with_matching_tags_should_return_true()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int> { 123, 456 };
            _fakeSeries.Tags = new HashSet<int> { 123, 789 };

            _specification.IsSatisfiedBy(_parseResultMulti, new ReleaseDecisionInformation(false, new SingleEpisodeSearchRequest { MonitoredEpisodesOnly = true })).Accepted.Should().BeTrue();
        }

        [Test]
        public void indexer_with_tags_series_with_different_tags_should_return_false()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int> { 456 };
            _fakeSeries.Tags = new HashSet<int> { 123, 789 };

            _specification.IsSatisfiedBy(_parseResultMulti, new ReleaseDecisionInformation(false, new SingleEpisodeSearchRequest { MonitoredEpisodesOnly = true })).Accepted.Should().BeFalse();
        }

        [Test]
        public void release_without_indexerid_should_return_true()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int> { 456 };
            _fakeSeries.Tags = new HashSet<int> { 123, 789 };
            _fakeRelease.IndexerId = 0;

            _specification.IsSatisfiedBy(_parseResultMulti, new ReleaseDecisionInformation(false, new SingleEpisodeSearchRequest { MonitoredEpisodesOnly = true })).Accepted.Should().BeTrue();
        }

        [Test]
        public void release_with_invalid_indexerid_should_return_true()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int> { 456 };
            _fakeSeries.Tags = new HashSet<int> { 123, 789 };
            _fakeRelease.IndexerId = 2;

            _specification.IsSatisfiedBy(_parseResultMulti, new ReleaseDecisionInformation(false, new SingleEpisodeSearchRequest { MonitoredEpisodesOnly = true })).Accepted.Should().BeTrue();
        }
    }
}
