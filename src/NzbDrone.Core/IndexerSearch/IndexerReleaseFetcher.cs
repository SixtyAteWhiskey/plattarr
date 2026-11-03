using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.IndexerProxies;
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
        Task<List<ReleaseInfo>> FetchRss(IReadOnlyCollection<MediaKind> kinds);

        // A module search. The translator turns the request into tiers of criteria per indexer.
        Task<IndexerSearchResult> Search<TRequest>(TRequest request,
                                                   IEnumerable<int> mediaTags,
                                                   MediaKind kind,
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
        private readonly IIndexerProxyFactory _indexerProxyFactory;
        private readonly Logger _logger;
        private readonly ConcurrentDictionary<string, CachedQuery> _interactiveQueries = new();
        private readonly ConcurrentDictionary<string, SearchSession> _searchSessions = new();
        private static readonly TimeSpan InteractiveQueryLifetime = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan SearchSessionLifetime = TimeSpan.FromMinutes(10);

        private sealed class CachedQuery
        {
            public DateTime Expires { get; init; }
            public IList<ReleaseInfo> Releases { get; init; }
        }

        private sealed class SearchSession
        {
            private readonly TaskCompletionSource<IndexerSearchResult> _first = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly Lazy<Task<IndexerSearchResult>> _completion;
            private readonly int _indexerCount;
            private readonly Stopwatch _wallTime = Stopwatch.StartNew();

            public SearchSession(int indexerCount, Func<Action<IList<ReleaseInfo>>, Task<IndexerSearchResult>> run)
            {
                _indexerCount = indexerCount;
                _completion = new Lazy<Task<IndexerSearchResult>>(() => Execute(run));
                Expires = DateTime.UtcNow.Add(SearchSessionLifetime);
            }

            public Task<IndexerSearchResult> First => _first.Task;
            public Task<IndexerSearchResult> Completion => _completion.Value;
            public DateTime Expires { get; }
            public long ElapsedMilliseconds => _wallTime.ElapsedMilliseconds;

            private void Report(IList<ReleaseInfo> releases)
            {
                if (releases.Count > 0)
                {
                    _first.TrySetResult(new IndexerSearchResult(releases.Select(r => (ReleaseInfo)r.Clone()).ToList(), _indexerCount));
                }
            }

            private async Task<IndexerSearchResult> Execute(Func<Action<IList<ReleaseInfo>>, Task<IndexerSearchResult>> run)
            {
                try
                {
                    var result = await run(Report);
                    _first.TrySetResult(result);
                    return result;
                }
                catch (Exception ex)
                {
                    _first.TrySetException(ex);
                    throw;
                }
            }
        }

        public IndexerReleaseFetcher(IIndexerFactory indexerFactory,
                                     IReleaseSearchService releaseSearchService,
                                     IIndexerStatusService indexerStatusService,
                                     IIndexerMediaTagService indexerMediaTagService,
                                     IIndexerCategoryService indexerCategoryService,
                                     IIndexerProxyFactory indexerProxyFactory,
                                     Logger logger)
        {
            _indexerFactory = indexerFactory;
            _releaseSearchService = releaseSearchService;
            _indexerStatusService = indexerStatusService;
            _indexerMediaTagService = indexerMediaTagService;
            _indexerCategoryService = indexerCategoryService;
            _indexerProxyFactory = indexerProxyFactory;
            _logger = logger;
        }

        public async Task<List<ReleaseInfo>> FetchRss(IReadOnlyCollection<MediaKind> kinds)
        {
            var indexers = _indexerFactory.RssEnabled();

            if (!indexers.Any())
            {
                _logger.Warn("No available indexers. check your configuration.");

                return new List<ReleaseInfo>();
            }

            var targets = indexers
                .Select(i => (Indexer: i, Categories: kinds.SelectMany(k => _indexerCategoryService.GetCategories(i.Definition.Id, k)).Distinct().ToArray()))
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
                                                                MediaKind kind,
                                                                ISearchTranslator<TRequest> translator)
            where TRequest : ModuleSearchRequest
        {
            var tags = mediaTags.ToList();

            var indexers = (request.InteractiveSearch ? _indexerFactory.InteractiveSearchEnabled() : _indexerFactory.AutomaticSearchEnabled())
                .Where(i => _indexerMediaTagService.Allows((IndexerDefinition)i.Definition, tags))
                .Select(i => (Indexer: i, Categories: _indexerCategoryService.GetCategories(i.Definition.Id, kind)))
                .Where(t => SupportsCategories(t.Indexer, t.Categories))
                .ToList();

            _logger.ProgressInfo("Searching indexers for {0}. {1} active indexers", request, indexers.Count);

            var searchId = InteractiveSearchExecution.SearchId;
            if (request.InteractiveSearch && !string.IsNullOrWhiteSpace(searchId) && searchId.Length <= 80)
            {
                var key = Hashing.SHA256Hash(JsonConvert.SerializeObject(new
                {
                    SearchId = searchId,
                    Refresh = request.RefreshSearch || InteractiveSearchExecution.Refresh,
                    Kind = kind,
                    RequestType = request.GetType().FullName,
                    SceneTitles = request.SceneTitles.OrderBy(t => t, StringComparer.InvariantCultureIgnoreCase).ToList(),
                    Tags = tags.OrderBy(t => t).ToList(),
                    Indexers = indexers.OrderBy(t => t.Indexer.Definition.Id).Select(t => new
                    {
                        Configuration = GetIndexerFingerprint(t.Indexer),
                        Categories = t.Categories.OrderBy(c => c).ToArray(),
                        Criteria = translator.Translate(request, (IndexerDefinition)t.Indexer.Definition, t.Categories)
                            .Select(tier => tier.Select(JsonConvert.SerializeObject).OrderBy(criteria => criteria).ToList())
                            .ToList()
                    }).ToList()
                }));

                if (_searchSessions.TryGetValue(key, out var expired) && expired.Expires <= DateTime.UtcNow)
                {
                    _searchSessions.TryRemove(key, out _);
                }

                var session = _searchSessions.GetOrAdd(key,
                    _ => new SearchSession(indexers.Count, report => RunSearch(indexers, request, kind, translator, report)));
                var completion = session.Completion;

                if (_searchSessions.Count > 64)
                {
                    foreach (var item in _searchSessions.Where(item => item.Value.Expires <= DateTime.UtcNow))
                    {
                        _searchSessions.TryRemove(item.Key, out _);
                    }
                }

                if (InteractiveSearchExecution.Preview)
                {
                    var first = await session.First;
                    _logger.Info("Interactive search preview: kind={0}, elapsedMs={1}, rawResults={2}", kind, session.ElapsedMilliseconds, first.Releases.Count);
                    return first;
                }

                return await completion;
            }

            return await RunSearch(indexers, request, kind, translator, null);
        }

        private async Task<IndexerSearchResult> RunSearch<TRequest>(List<(IIndexer Indexer, int[] Categories)> indexers,
            TRequest request,
            MediaKind kind,
            ISearchTranslator<TRequest> translator,
            Action<IList<ReleaseInfo>> report)
            where TRequest : ModuleSearchRequest
        {
            var wallTime = Stopwatch.StartNew();

            var batch = await Task.WhenAll(indexers.Select(t => SearchIndexer(t.Indexer, t.Categories, request, kind, translator, report)));

            var releases = batch.SelectMany(x => x).ToList();

            _logger.ProgressDebug("Total of {0} reports were found for {1} from {2} indexers", releases.Count, request, indexers.Count);
            if (request.InteractiveSearch)
            {
                _logger.Info("Interactive search summary: kind={0}, elapsedMs={1}, indexers={2}, rawResults={3}", kind, wallTime.ElapsedMilliseconds, indexers.Count, releases.Count);
            }

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

        private async Task<IList<ReleaseInfo>> SearchIndexer<TRequest>(IIndexer indexer,
            int[] categories,
            TRequest request,
            MediaKind kind,
            ISearchTranslator<TRequest> translator,
            Action<IList<ReleaseInfo>> report)
            where TRequest : ModuleSearchRequest
        {
            var wallTime = Stopwatch.StartNew();
            var requests = 0;
            var cacheHits = 0;
            var providerRequests = 0;
            var rawResults = 0;
            try
            {
                foreach (var tier in translator.Translate(request, (IndexerDefinition)indexer.Definition, categories))
                {
                    var releases = new List<ReleaseInfo>();

                    foreach (var criteria in tier)
                    {
                        criteria.InteractiveSearch = request.InteractiveSearch;
                        criteria.Source ??= SearchSource;

                        var queryTime = Stopwatch.StartNew();
                        var (found, cached) = await SearchQuery(indexer, criteria, kind, request.RefreshSearch || InteractiveSearchExecution.Refresh);
                        foreach (var release in found)
                        {
                            release.SearchTitleVariant = criteria.TitleVariant;
                        }

                        requests += cached ? 0 : 1;
                        cacheHits += cached ? 1 : 0;
                        providerRequests += criteria.ProviderRequestCount;
                        rawResults += found.Count;
                        report?.Invoke(found);
                        if (request.InteractiveSearch)
                        {
                            _logger.Info(
                                "Interactive search query: indexerId={0}, variant={1}, elapsedMs={2}, requests={3}, cacheHit={4}, rawResults={5}",
                                indexer.Definition.Id,
                                criteria.TitleVariant?.ToString() ?? (criteria.IsIdSearch ? "id" : "other"),
                                queryTime.ElapsedMilliseconds,
                                cached ? 0 : 1,
                                cached,
                                found.Count);
                        }

                        releases.AddRange(found);
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
            finally
            {
                if (request.InteractiveSearch)
                {
                    _logger.Info(
                        "Interactive search indexer: indexerId={0}, elapsedMs={1}, criteriaRequests={2}, providerRequests={3}, cacheHits={4}, rawResults={5}",
                        indexer.Definition.Id,
                        wallTime.ElapsedMilliseconds,
                        requests,
                        providerRequests,
                        cacheHits,
                        rawResults);
                }
            }

            return Array.Empty<ReleaseInfo>();
        }

        private async Task<(IList<ReleaseInfo> Releases, bool Cached)> SearchQuery(IIndexer indexer, SearchCriteriaBase criteria, MediaKind kind, bool refresh)
        {
            if (!criteria.InteractiveSearch)
            {
                return (await _releaseSearchService.Search(indexer, criteria), false);
            }

            // Hash configuration in memory: the key includes credentials, capabilities, routing,
            // scoped categories, and every criterion field without writing them to logs.
            var key = Hashing.SHA256Hash(JsonConvert.SerializeObject(new
            {
                Kind = kind,
                Configuration = GetIndexerFingerprint(indexer),
                Criteria = criteria
            }));

            if (!refresh && _interactiveQueries.TryGetValue(key, out var hit) && hit.Expires > DateTime.UtcNow)
            {
                return (hit.Releases.Select(r => (ReleaseInfo)r.Clone()).ToList(), true);
            }

            var releases = await _releaseSearchService.Search(indexer, criteria);
            _interactiveQueries[key] = new CachedQuery
            {
                Expires = DateTime.UtcNow.Add(InteractiveQueryLifetime),
                Releases = releases.Select(r => (ReleaseInfo)r.Clone()).ToList()
            };

            if (_interactiveQueries.Count > 1024)
            {
                foreach (var item in _interactiveQueries.Where(item => item.Value.Expires <= DateTime.UtcNow))
                {
                    _interactiveQueries.TryRemove(item.Key, out _);
                }
            }

            return (releases, false);
        }

        private string GetIndexerFingerprint(IIndexer indexer)
        {
            var definition = (IndexerDefinition)indexer.Definition;
            return Hashing.SHA256Hash(JsonConvert.SerializeObject(new
            {
                definition.Id,
                definition.Implementation,
                definition.EnableInteractiveSearch,
                definition.Priority,
                definition.Redirect,
                definition.DownloadClientId,
                definition.IndexerUrls,
                definition.LegacyUrls,
                definition.Language,
                definition.Protocol,
                Tags = definition.Tags.OrderBy(t => t).ToList(),
                Settings = definition.Settings,
                Capabilities = indexer.GetCapabilities(),
                Proxies = _indexerProxyFactory.All().OrderBy(p => p.Id).ToList()
            }));
        }
    }
}
