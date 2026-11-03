using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.IndexerProxies;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Overrides;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.IndexerSearchTests
{
    [TestFixture]
    public class IndexerReleaseFetcherFixture : CoreTest<IndexerReleaseFetcher>
    {
        private static readonly int[] MovieCategories = { NewznabStandardCategory.Movies.Id };

        private Mock<IIndexer> _indexer;
        private IndexerDefinition _definition;
        private IndexerCapabilities _capabilities;
        private List<SearchCriteriaBase> _searched;
        private Dictionary<string, List<ReleaseInfo>> _resultsByTerm;
        private TestTranslator _translator;
        private IndexerOverride _override;

        [SetUp]
        public void SetUp()
        {
            _definition = new IndexerDefinition { Id = 1, Name = "Test", Tags = new HashSet<int>() };

            _capabilities = new IndexerCapabilities();
            _capabilities.Categories.AddCategoryMapping(2000, NewznabStandardCategory.Movies);

            _indexer = new Mock<IIndexer>();
            _indexer.SetupGet(i => i.Definition).Returns(() => _definition);
            _indexer.SetupGet(i => i.SupportsRss).Returns(true);
            _indexer.SetupGet(i => i.SupportsSearch).Returns(true);
            _indexer.Setup(i => i.GetCapabilities()).Returns(() => _capabilities);

            Mocker.GetMock<IIndexerFactory>()
                  .Setup(f => f.Enabled(It.IsAny<bool>()))
                  .Returns(() => new List<IIndexer> { _indexer.Object });

            Mocker.GetMock<IIndexerProxyFactory>()
                  .Setup(f => f.All())
                  .Returns(new List<IndexerProxyDefinition>());

            Mocker.SetConstant<IIndexerMediaTagService>(Mocker.Resolve<IndexerMediaTagService>());

            _override = new IndexerOverride { IndexerId = 1, MediaKind = MediaKind.Movies };

            Mocker.GetMock<IIndexerOverrideService>()
                  .Setup(s => s.Get(1, MediaKind.Movies))
                  .Returns(() => _override);

            Mocker.SetConstant<IEnumerable<IMediaModule>>(new IMediaModule[] { new MoviesModule() });
            Mocker.SetConstant<IIndexerCategoryService>(Mocker.Resolve<IndexerCategoryService>());

            _searched = new List<SearchCriteriaBase>();
            _resultsByTerm = new Dictionary<string, List<ReleaseInfo>>();

            Mocker.GetMock<IReleaseSearchService>()
                  .Setup(s => s.Search(It.IsAny<IIndexer>(), It.IsAny<SearchCriteriaBase>()))
                  .Callback<IIndexer, SearchCriteriaBase>((_, c) => _searched.Add(c))
                  .Returns<IIndexer, SearchCriteriaBase>((_, c) =>
                      Task.FromResult<IList<ReleaseInfo>>(_resultsByTerm.TryGetValue(c.SearchTerm ?? "", out var r) ? r : new List<ReleaseInfo>()));

            _translator = new TestTranslator();
        }

        private Task<IndexerSearchResult> Search(bool interactive = false, params int[] mediaTags)
        {
            var request = new TestRequest { InteractiveSearch = interactive, SceneTitles = new List<string> { "Title" } };

            return Subject.Search(request, mediaTags, MediaKind.Movies, _translator);
        }

        [Test]
        public async Task should_search_indexer_without_tags_for_tagged_media()
        {
            await Search(false, 3);

            _searched.Should().NotBeEmpty();
        }

        [Test]
        public async Task should_skip_tagged_indexer_for_untagged_media()
        {
            _definition.Tags = new HashSet<int> { 3 };

            var result = await Search();

            _searched.Should().BeEmpty();
            result.IndexersSearched.Should().Be(0);
        }

        [Test]
        public async Task should_search_indexer_when_tags_intersect()
        {
            _definition.Tags = new HashSet<int> { 1, 2, 3 };

            await Search(false, 3, 4, 5);

            _searched.Should().NotBeEmpty();
        }

        [Test]
        public async Task should_skip_indexer_when_tags_do_not_intersect()
        {
            _definition.Tags = new HashSet<int> { 1, 2, 3 };

            await Search(false, 4, 5, 6);

            _searched.Should().BeEmpty();
        }

        [Test]
        public async Task should_not_treat_indexer_proxy_tags_as_media_restrictions()
        {
            _definition.Tags = new HashSet<int> { 7 };

            Mocker.GetMock<IIndexerProxyFactory>()
                  .Setup(f => f.All())
                  .Returns(new List<IndexerProxyDefinition> { new() { Id = 1, Tags = new HashSet<int> { 7 } } });

            await Search();

            _searched.Should().NotBeEmpty();
        }

        [Test]
        public async Task should_still_restrict_by_non_proxy_tags_on_a_proxied_indexer()
        {
            _definition.Tags = new HashSet<int> { 7, 8 };

            Mocker.GetMock<IIndexerProxyFactory>()
                  .Setup(f => f.All())
                  .Returns(new List<IndexerProxyDefinition> { new() { Id = 1, Tags = new HashSet<int> { 7 } } });

            await Search();

            _searched.Should().BeEmpty();
        }

        [Test]
        public async Task should_skip_indexer_with_automatic_search_disabled()
        {
            _definition.EnableAutomaticSearch = false;

            await Search(interactive: false);

            _searched.Should().BeEmpty();
        }

        [Test]
        public async Task should_use_interactive_flag_for_interactive_search()
        {
            _definition.EnableAutomaticSearch = false;

            await Search(interactive: true);

            _searched.Should().NotBeEmpty();
            _searched.Should().OnlyContain(c => c.InteractiveSearch);
        }

        [Test]
        public async Task should_reuse_interactive_provider_results_for_the_same_search()
        {
            _resultsByTerm["tier2a"] = new List<ReleaseInfo> { new() { Title = "Found" } };
            _resultsByTerm["tier2b"] = new List<ReleaseInfo> { new() { Title = "Other" } };

            await Search(interactive: true);
            var second = await Search(interactive: true);

            _searched.Select(c => c.SearchTerm).Should().Equal("tier1", "tier2a", "tier2b");
            second.Releases.Should().HaveCount(2);
            second.Releases[0].Should().NotBeSameAs(_resultsByTerm["tier2a"][0]);
        }

        [Test]
        public async Task should_bypass_interactive_cache_for_explicit_refresh()
        {
            await Search(interactive: true);

            var request = new TestRequest { InteractiveSearch = true, RefreshSearch = true, SceneTitles = new List<string> { "Title" } };
            await Subject.Search(request, Array.Empty<int>(), MediaKind.Movies, _translator);

            _searched.Select(c => c.SearchTerm).Should().Equal("tier1", "tier2a", "tier2b", "tier1", "tier2a", "tier2b");
        }

        [Test]
        public async Task should_invalidate_interactive_cache_when_indexer_configuration_changes()
        {
            _resultsByTerm["tier1"] = new List<ReleaseInfo> { new() { Title = "Found" } };
            await Search(interactive: true);

            _definition.Language = "de";
            await Search(interactive: true);

            _searched.Should().HaveCount(2);
        }

        [Test]
        public async Task should_invalidate_interactive_cache_when_scope_categories_change()
        {
            _resultsByTerm["tier1"] = new List<ReleaseInfo> { new() { Title = "Found" } };
            await Search(interactive: true);

            _capabilities.Categories.AddCategoryMapping(8000, NewznabStandardCategory.Other);
            _override.Categories = new List<int> { 8000 };
            await Search(interactive: true);

            _searched.Should().HaveCount(2);
            _searched[1].Categories.Should().Equal(8000);
        }

        [Test]
        public async Task should_not_reuse_interactive_results_for_automatic_search()
        {
            _resultsByTerm["tier1"] = new List<ReleaseInfo> { new() { Title = "Found" } };
            await Search(interactive: true);
            await Search();

            _searched.Should().HaveCount(2);
        }

        [Test]
        public async Task should_deliver_first_results_before_the_remaining_queries_complete_without_duplicate_requests()
        {
            var finalQuery = new TaskCompletionSource<IList<ReleaseInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
            Mocker.GetMock<IReleaseSearchService>()
                  .Setup(s => s.Search(It.IsAny<IIndexer>(), It.IsAny<SearchCriteriaBase>()))
                  .Callback<IIndexer, SearchCriteriaBase>((_, c) => _searched.Add(c))
                  .Returns<IIndexer, SearchCriteriaBase>((_, c) => c.SearchTerm switch
                  {
                      "tier2a" => Task.FromResult<IList<ReleaseInfo>>(new List<ReleaseInfo> { new() { Title = "First" } }),
                      "tier2b" => finalQuery.Task,
                      _ => Task.FromResult<IList<ReleaseInfo>>(new List<ReleaseInfo>())
                  });

            var request = new TestRequest { InteractiveSearch = true, SceneTitles = new List<string> { "Title", "Alternate" } };
            Task<IndexerSearchResult> preview;

            using (InteractiveSearchExecution.Begin(false, true, "search-session"))
            {
                preview = Subject.Search(request, Array.Empty<int>(), MediaKind.Movies, _translator);
            }

            var first = await preview.WaitAsync(TimeSpan.FromSeconds(2));
            first.Releases.Should().ContainSingle().Which.Title.Should().Be("First");
            _searched.Should().HaveCount(3);

            Task<IndexerSearchResult> complete;
            using (InteractiveSearchExecution.Begin(false, false, "search-session"))
            {
                var equivalentRequest = new TestRequest { InteractiveSearch = true, SceneTitles = new List<string> { "Alternate", "Title" } };
                complete = Subject.Search(equivalentRequest, Array.Empty<int>(), MediaKind.Movies, _translator);
            }

            complete.IsCompleted.Should().BeFalse();
            finalQuery.SetResult(new List<ReleaseInfo> { new() { Title = "Final" } });

            var all = await complete.WaitAsync(TimeSpan.FromSeconds(2));
            all.Releases.Select(r => r.Title).Should().Equal("First", "Final");
            _searched.Should().HaveCount(3);
        }

        [Test]
        public async Task should_skip_indexer_with_interactive_search_disabled()
        {
            _definition.EnableInteractiveSearch = false;

            await Search(interactive: true);

            _searched.Should().BeEmpty();
        }

        [Test]
        public async Task should_skip_indexer_without_supported_categories()
        {
            _capabilities = new IndexerCapabilities();
            _capabilities.Categories.AddCategoryMapping(5000, NewznabStandardCategory.TV);

            await Search();

            _searched.Should().BeEmpty();
        }

        [Test]
        public async Task should_not_run_second_tier_when_first_tier_has_results()
        {
            _resultsByTerm["tier1"] = new List<ReleaseInfo> { new() { Title = "Found" } };

            var result = await Search();

            _searched.Select(c => c.SearchTerm).Should().Equal("tier1");
            result.Releases.Should().HaveCount(1);
        }

        [Test]
        public async Task should_run_second_tier_when_first_tier_is_empty()
        {
            _resultsByTerm["tier2a"] = new List<ReleaseInfo> { new() { Title = "A" } };
            _resultsByTerm["tier2b"] = new List<ReleaseInfo> { new() { Title = "B" } };

            var result = await Search();

            _searched.Select(c => c.SearchTerm).Should().Equal("tier1", "tier2a", "tier2b");
            result.Releases.Should().HaveCount(2);
            result.IndexersSearched.Should().Be(1);
        }

        [Test]
        public async Task should_fetch_rss_only_from_rss_enabled_indexers()
        {
            _definition.EnableRss = false;

            var releases = await Subject.FetchRss(new[] { MediaKind.Movies });

            releases.Should().BeEmpty();
            _searched.Should().BeEmpty();
            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public async Task should_fetch_rss_with_requested_categories_and_no_term()
        {
            await Subject.FetchRss(new[] { MediaKind.Movies });

            _searched.Should().ContainSingle();
            _searched[0].Should().BeOfType<BasicSearchCriteria>();
            _searched[0].IsRssSearch.Should().BeTrue();
            _searched[0].Categories.Should().Equal(MovieCategories);
        }

        [Test]
        public async Task should_record_newest_rss_release_and_warn_on_gap()
        {
            var newest = new ReleaseInfo { Title = "Newest", DownloadUrl = "n", PublishDate = DateTime.UtcNow };
            var oldest = new ReleaseInfo { Title = "Oldest", DownloadUrl = "o", PublishDate = DateTime.UtcNow.AddHours(-1) };
            _resultsByTerm[""] = new List<ReleaseInfo> { oldest, newest };

            Mocker.GetMock<IIndexerStatusService>()
                  .Setup(s => s.GetLastRssSyncReleaseInfo(1))
                  .Returns(new ReleaseInfo { DownloadUrl = "previous", PublishDate = DateTime.UtcNow.AddDays(-1) });

            await Subject.FetchRss(new[] { MediaKind.Movies });

            Mocker.GetMock<IIndexerStatusService>()
                  .Verify(s => s.UpdateRssSyncStatus(1, newest), Times.Once());

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public async Task should_not_warn_when_rss_overlaps_previous_fetch()
        {
            var previous = new ReleaseInfo { Title = "Previous", DownloadUrl = "previous", PublishDate = DateTime.UtcNow.AddHours(-2) };
            _resultsByTerm[""] = new List<ReleaseInfo> { new() { Title = "New", DownloadUrl = "n", PublishDate = DateTime.UtcNow }, previous };

            Mocker.GetMock<IIndexerStatusService>()
                  .Setup(s => s.GetLastRssSyncReleaseInfo(1))
                  .Returns(previous);

            await Subject.FetchRss(new[] { MediaKind.Movies });

            ExceptionVerification.ExpectedWarns(0);
        }

        [Test]
        public async Task should_use_override_categories_for_rss()
        {
            _capabilities.Categories.AddCategoryMapping(8000, NewznabStandardCategory.Other);
            _override.Categories = new List<int> { 8000 };

            await Subject.FetchRss(new[] { MediaKind.Movies });

            _searched.Should().ContainSingle();
            _searched[0].Categories.Should().Equal(8000);
        }

        [Test]
        public async Task should_pass_override_categories_to_the_translator()
        {
            _capabilities.Categories.AddCategoryMapping(8000, NewznabStandardCategory.Other);
            _override.Categories = new List<int> { 8000 };

            await Search();

            _translator.Categories.Should().Equal(8000);
        }

        [Test]
        public async Task should_skip_indexer_when_override_categories_are_unsupported()
        {
            _override.Categories = new List<int> { 7000 };

            await Search();

            _searched.Should().BeEmpty();
        }

        public class TestRequest : ModuleSearchRequest
        {
        }

        private class TestTranslator : ISearchTranslator<TestRequest>
        {
            public int[] Categories { get; private set; }

            public IEnumerable<IReadOnlyList<SearchCriteriaBase>> Translate(TestRequest request, IndexerDefinition indexer, int[] categories)
            {
                Categories = categories;

                yield return new SearchCriteriaBase[]
                {
                    new BasicSearchCriteria { SearchTerm = "tier1", Categories = categories }
                };

                yield return new SearchCriteriaBase[]
                {
                    new BasicSearchCriteria { SearchTerm = "tier2a", Categories = categories },
                    new BasicSearchCriteria { SearchTerm = "tier2b", Categories = categories }
                };
            }
        }
    }
}
