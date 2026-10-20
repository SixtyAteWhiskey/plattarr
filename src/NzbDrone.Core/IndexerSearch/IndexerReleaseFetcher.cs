using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.IndexerSearch
{
    public class IndexerSearchResult
    {
        public IndexerSearchResult(List<ReleaseInfo> releases, int indexersSearched)
        {
            Releases = releases;
            IndexersSearched = indexersSearched;
        }

        public List<ReleaseInfo> Releases { get; }
        public int IndexersSearched { get; }
    }

    public interface IIndexerReleaseFetcher
    {
        // One RSS request per RSS-enabled indexer, for the categories of every given media type.
        Task<List<ReleaseInfo>> FetchRss(IReadOnlyCollection<IReleaseMatcher> matchers);

        // A module search. The translator turns the request into tiers of criteria per indexer.
        Task<IndexerSearchResult> Search<TRequest>(TRequest request,
                                                   IEnumerable<int> mediaTags,
                                                   IReleaseMatcher matcher,
                                                   ISearchTranslator<TRequest> translator)
            where TRequest : ModuleSearchRequest;
    }

    // The in-process adapter between the media modules and Prowlarr's indexer layer
    // (porting step 7). Every request goes through Prowlarr's ReleaseSearchService, so
    // query limits, indexer history, and stats apply exactly as they do in Prowlarr.
    public class IndexerReleaseFetcher : IIndexerReleaseFetcher
    {
        public const string SearchSource = "Plattarr";

        private readonly IIndexerFactory _indexerFactory;
        private readonly IReleaseSearchService _releaseSearchService;
        private readonly IIndexerStatusService _indexerStatusService;
        private readonly IIndexerMediaTagService _indexerMediaTagService;
        private readonly IIndexerCategoryService _indexerCategoryService;
        private readonly Logger _logger;

        public IndexerReleaseFetcher(IIndexerFactory indexerFactory,
                                     IReleaseSearchService releaseSearchService,
                                     IIndexerStatusService indexerStatusService,
                                     IIndexerMediaTagService indexerMediaTagService,
                                     IIndexerCategoryService indexerCategoryService,
                                     Logger logger)
        {
            _indexerFactory = indexerFactory;
            _releaseSearchService = releaseSearchService;
            _indexerStatusService = indexerStatusService;
            _indexerMediaTagService = indexerMediaTagService;
            _indexerCategoryService = indexerCategoryService;
            _logger = logger;
        }

        public async Task<List<ReleaseInfo>> FetchRss(IReadOnlyCollection<IReleaseMatcher> matchers)
        {
            var indexers = _indexerFactory.Enabled()
                .Where(i => i.SupportsRss && ((IndexerDefinition)i.Definition).EnableRss)
                .ToList();

            if (!indexers.Any())
            {
                _logger.Warn("No available indexers. check your configuration.");

                return new List<ReleaseInfo>();
            }

            var targets = indexers
                .Select(i => (Indexer: i, Categories: matchers.SelectMany(m => _indexerCategoryService.GetCategories(i.Definition.Id, m)).Distinct().ToArray()))
                .Where(t => SupportsCategories(t.Indexer, t.Categories))
                .ToList();

            _logger.Debug("Available indexers {0}", targets.Count);

            var batch = await Task.WhenAll(targets.Select(t => FetchRssIndexer(t.Indexer, t.Categories)));

            var result = batch.SelectMany(x => x).ToList();

            _logger.Debug("Found {0} reports", result.Count);

            return result;
        }

        public async Task<IndexerSearchResult> Search<TRequest>(TRequest request,
                                                                IEnumerable<int> mediaTags,
                                                                IReleaseMatcher matcher,
                                                                ISearchTranslator<TRequest> translator)
            where TRequest : ModuleSearchRequest
        {
            var tags = mediaTags.ToList();

            var indexers = _indexerFactory.Enabled()
                .Where(i => i.SupportsSearch)
                .Where(i =>
                {
                    var definition = (IndexerDefinition)i.Definition;

                    return request.InteractiveSearch ? definition.EnableInteractiveSearch : definition.EnableAutomaticSearch;
                })
                .Where(i => _indexerMediaTagService.Allows((IndexerDefinition)i.Definition, tags))
                .Select(i => (Indexer: i, Categories: _indexerCategoryService.GetCategories(i.Definition.Id, matcher)))
                .Where(t => SupportsCategories(t.Indexer, t.Categories))
                .ToList();

            _logger.ProgressInfo("Searching indexers for {0}. {1} active indexers", request, indexers.Count);

            var batch = await Task.WhenAll(indexers.Select(t => SearchIndexer(t.Indexer, t.Categories, request, translator)));

            var releases = batch.SelectMany(x => x).ToList();

            _logger.ProgressDebug("Total of {0} reports were found for {1} from {2} indexers", releases.Count, request, indexers.Count);

            return new IndexerSearchResult(releases, indexers.Count);
        }

        private bool SupportsCategories(IIndexer indexer, int[] categories)
        {
            if (indexer.GetCapabilities().Categories.SupportedCategories(categories).Any())
            {
                return true;
            }

            _logger.Debug("Indexer {0} has no categories in [{1}], skipping", indexer.Definition.Name, string.Join(", ", categories));

            return false;
        }

        private async Task<IList<ReleaseInfo>> FetchRssIndexer(IIndexer indexer, int[] categories)
        {
            var criteria = new BasicSearchCriteria
            {
                Categories = categories,
                SearchType = "search",
                Source = SearchSource
            };

            var releases = await _releaseSearchService.Search(indexer, criteria);

            if (releases.Any())
            {
                TrackRssCoverage(indexer, releases);
            }

            return releases;
        }

        // Radarr warned when consecutive RSS fetches left a gap. Prowlarr keeps the status
        // field but never fills it, so the adapter does.
        private void TrackRssCoverage(IIndexer indexer, IList<ReleaseInfo> releases)
        {
            try
            {
                var ordered = releases.OrderByDescending(r => r.PublishDate).ToList();
                var last = _indexerStatusService.GetLastRssSyncReleaseInfo(indexer.Definition.Id);

                if (last != null &&
                    ordered.Last().PublishDate > last.PublishDate &&
                    !releases.Any(r => r.DownloadUrl == last.DownloadUrl))
                {
                    _logger.Warn("Indexer {0} rss sync didn't cover the period between {1} and {2} UTC. Search may be required.", indexer.Definition.Name, last.PublishDate, ordered.Last().PublishDate);
                }

                _indexerStatusService.UpdateRssSyncStatus(indexer.Definition.Id, ordered.First());
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to record RSS sync status for {0}", indexer.Definition.Name);
            }
        }

        private async Task<IList<ReleaseInfo>> SearchIndexer<TRequest>(IIndexer indexer, int[] categories, TRequest request, ISearchTranslator<TRequest> translator)
            where TRequest : ModuleSearchRequest
        {
            try
            {
                foreach (var tier in translator.Translate(request, (IndexerDefinition)indexer.Definition, categories))
                {
                    var releases = new List<ReleaseInfo>();

                    foreach (var criteria in tier)
                    {
                        criteria.InteractiveSearch = request.InteractiveSearch;
                        criteria.Source ??= SearchSource;

                        releases.AddRange(await _releaseSearchService.Search(indexer, criteria));
                    }

                    if (releases.Any())
                    {
                        return releases;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error while searching for {0}", request);
            }

            return Array.Empty<ReleaseInfo>();
        }
    }
}
