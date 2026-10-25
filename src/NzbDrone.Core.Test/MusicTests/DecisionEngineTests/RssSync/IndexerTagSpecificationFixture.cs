using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.IndexerProxies;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.DecisionEngine.Specifications.RssSync;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Search;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.DecisionEngineTests.RssSync
{
    [TestFixture]
    public class IndexerTagSpecificationFixture : CoreTest<IndexerTagSpecification>
    {
        private IndexerTagSpecification _specification;

        private RemoteAlbum _parseResultMulti;
        private IndexerDefinition _fakeIndexerDefinition;
        private Artist _fakeArtist;
        private Album _firstAlbum;
        private Album _secondAlbum;
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
                .Setup(m => m.Find(It.IsAny<int>()))
                .Returns((IndexerDefinition)null);

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

            _fakeArtist = Builder<Artist>.CreateNew()
                .With(c => c.Monitored = true)
                .With(c => c.Tags = new HashSet<int>())
                .Build();

            _fakeRelease = new ReleaseInfo
            {
                IndexerId = 1
            };

            _firstAlbum = new Album { Monitored = true };
            _secondAlbum = new Album { Monitored = true };

            var doubleEpisodeList = new List<Album> { _firstAlbum, _secondAlbum };

            _parseResultMulti = new RemoteAlbum
            {
                Artist = _fakeArtist,
                Albums = doubleEpisodeList,
                Release = _fakeRelease
            };
        }

        [Test]
        public void indexer_proxy_tag_should_not_restrict_music()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int> { 7 };
            _proxies = new List<IndexerProxyDefinition> { new() { Id = 1, Tags = new HashSet<int> { 7 } } };
            _fakeArtist.Tags = new HashSet<int>();

            _specification.IsSatisfiedBy(_parseResultMulti, new()).Accepted.Should().BeTrue();
        }

        [Test]
        public void indexer_media_tag_should_still_restrict_music_with_proxy_tag()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int> { 7, 8 };
            _proxies = new List<IndexerProxyDefinition> { new() { Id = 1, Tags = new HashSet<int> { 7 } } };
            _fakeArtist.Tags = new HashSet<int>();

            var decision = _specification.IsSatisfiedBy(_parseResultMulti, new());

            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(DownloadRejectionReason.NoMatchingTag);
        }

        [Test]
        public void indexer_and_series_without_tags_should_return_true()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int>();
            _fakeArtist.Tags = new HashSet<int>();

            _specification.IsSatisfiedBy(_parseResultMulti, new ReleaseDecisionInformation(false, new AlbumSearchRequest { MonitoredEpisodesOnly = true })).Accepted.Should().BeTrue();
        }

        [Test]
        public void indexer_with_tags_series_without_tags_should_return_false()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int> { 123 };
            _fakeArtist.Tags = new HashSet<int>();

            var rejection = _specification.IsSatisfiedBy(_parseResultMulti, new ReleaseDecisionInformation(false, new AlbumSearchRequest { MonitoredEpisodesOnly = true }));

            rejection.Accepted.Should().BeFalse();

            rejection.Reason.Should().Be(DownloadRejectionReason.NoMatchingTag);
        }

        [Test]
        public void indexer_without_tags_series_with_tags_should_return_true()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int>();
            _fakeArtist.Tags = new HashSet<int> { 123 };

            _specification.IsSatisfiedBy(_parseResultMulti, new ReleaseDecisionInformation(false, new AlbumSearchRequest { MonitoredEpisodesOnly = true })).Accepted.Should().BeTrue();
        }

        [Test]
        public void indexer_with_tags_series_with_matching_tags_should_return_true()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int> { 123, 456 };
            _fakeArtist.Tags = new HashSet<int> { 123, 789 };

            _specification.IsSatisfiedBy(_parseResultMulti, new ReleaseDecisionInformation(false, new AlbumSearchRequest { MonitoredEpisodesOnly = true })).Accepted.Should().BeTrue();
        }

        [Test]
        public void indexer_with_tags_series_with_different_tags_should_return_false()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int> { 456 };
            _fakeArtist.Tags = new HashSet<int> { 123, 789 };

            var rejection = _specification.IsSatisfiedBy(_parseResultMulti, new ReleaseDecisionInformation(false, new AlbumSearchRequest { MonitoredEpisodesOnly = true }));

            rejection.Accepted.Should().BeFalse();

            rejection.Reason.Should().Be(DownloadRejectionReason.NoMatchingTag);
        }

        [Test]
        public void release_without_indexerid_should_return_true()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int> { 456 };
            _fakeArtist.Tags = new HashSet<int> { 123, 789 };
            _fakeRelease.IndexerId = 0;

            _specification.IsSatisfiedBy(_parseResultMulti, new ReleaseDecisionInformation(false, new AlbumSearchRequest { MonitoredEpisodesOnly = true })).Accepted.Should().BeTrue();
        }

        [Test]
        public void release_with_invalid_indexerid_should_return_true()
        {
            _fakeIndexerDefinition.Tags = new HashSet<int> { 456 };
            _fakeArtist.Tags = new HashSet<int> { 123, 789 };
            _fakeRelease.IndexerId = 2;

            _specification.IsSatisfiedBy(_parseResultMulti, new ReleaseDecisionInformation(false, new AlbumSearchRequest { MonitoredEpisodesOnly = true })).Accepted.Should().BeTrue();
        }
    }
}
