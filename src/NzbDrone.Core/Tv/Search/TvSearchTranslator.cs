using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Tv.DataAugmentation.Scene;

namespace NzbDrone.Core.Tv.Search
{
    // Contract 3 for TV: the request shapes of Sonarr's NewznabRequestGenerator (b3d4bb8) as ordered
    // tiers of the indexer layer's criteria.
    //
    // - Standard, daily and season requests search by the IDs the indexer supports first and by the
    //   scene titles only when that finds nothing (SearchMode.Default). A scene mapping can ask for
    //   the ID search, the title search or both in the first tier.
    // - Anime requests are one tier: absolute number by ID, absolute number by title, and the
    //   standard numbering when AnimeStandardFormatSearch is on.
    // - Special requests search "series title + episode title".
    //
    // The indexer's capabilities gate each shape as they did in Sonarr: season and episode searches
    // need the season and ep parameters, anime and special searches need free text search. All
    // supported IDs go in one request (Sonarr's aggregated search); the indexer layer drops the
    // ones a given tracker ignores. Daily episodes use Newznab's year plus "MM/dd" (season and ep),
    // which TvSearchCriteria turns into "yyyy.MM.dd" for trackers that take a search string.
    public class TvSearchTranslator : ISearchTranslator<TvSearchRequest>
    {
        public const string Source = "Tv";

        private readonly IConfigService _configService;

        public TvSearchTranslator(IConfigService configService)
        {
            _configService = configService;
        }

        public IEnumerable<IReadOnlyList<SearchCriteriaBase>> Translate(TvSearchRequest request, IndexerDefinition indexer, int[] categories)
        {
            var capabilities = indexer.Capabilities;

            switch (request)
            {
                case SingleEpisodeSearchRequest single:
                    return Standard(single, capabilities, categories, single.SeasonNumber, single.EpisodeNumber.ToString(), true);

                case SeasonSearchRequest season:
                    return Standard(season, capabilities, categories, season.SeasonNumber, null, false);

                case DailyEpisodeSearchRequest dailyEpisode:
                    return Standard(dailyEpisode, capabilities, categories, dailyEpisode.AirDate.Year, $"{dailyEpisode.AirDate:MM}/{dailyEpisode.AirDate:dd}", true);

                case DailySeasonSearchRequest dailySeason:
                    return Standard(dailySeason, capabilities, categories, dailySeason.Year, null, true);

                case AnimeEpisodeSearchRequest animeEpisode:
                    return AnimeEpisode(animeEpisode, capabilities, categories);

                case AnimeSeasonSearchRequest animeSeason:
                    return AnimeSeason(animeSeason, capabilities, categories);

                case SpecialEpisodeSearchRequest special:
                    return Special(special, capabilities, categories);

                default:
                    return Array.Empty<IReadOnlyList<SearchCriteriaBase>>();
            }
        }

        private static IEnumerable<IReadOnlyList<SearchCriteriaBase>> Standard(TvSearchRequest request, IndexerCapabilities capabilities, int[] categories, int season, string episode, bool needsEpisodeSearch)
        {
            if (capabilities != null)
            {
                var supportsSeasonSearch = needsEpisodeSearch
                    ? capabilities.TvSearchSeasonAvailable && capabilities.TvSearchEpAvailable
                    : capabilities.TvSearchSeasonAvailable;

                if (!supportsSeasonSearch || (!SupportsTitleSearch(capabilities) && !SupportsIdSearch(capabilities)))
                {
                    yield break;
                }
            }

            var tier = new List<SearchCriteriaBase>();

            if (request.SearchMode.HasFlag(SearchMode.SearchID) || request.SearchMode == SearchMode.Default)
            {
                var idCriteria = IdCriteria(request, capabilities, categories, season, episode, null);

                if (idCriteria != null)
                {
                    tier.Add(idCriteria);
                }
            }

            if (request.SearchMode.HasFlag(SearchMode.SearchTitle))
            {
                tier.AddRange(TitleCriteria(request, capabilities, categories, season, episode));
            }

            if (tier.Any())
            {
                yield return tier;
            }

            if (request.SearchMode == SearchMode.Default)
            {
                var titles = TitleCriteria(request, capabilities, categories, season, episode);

                if (titles.Any())
                {
                    yield return titles;
                }
            }
        }

        private IEnumerable<IReadOnlyList<SearchCriteriaBase>> AnimeEpisode(AnimeEpisodeSearchRequest request, IndexerCapabilities capabilities, int[] categories)
        {
            if (capabilities != null && !capabilities.SearchAvailable)
            {
                yield break;
            }

            var absolute = $"{request.AbsoluteEpisodeNumber:00}";
            var includeStandardFormat = _configService.AnimeStandardFormatSearch &&
                                        request.SeasonNumber > 0 &&
                                        request.EpisodeNumber > 0 &&
                                        (capabilities == null || (capabilities.TvSearchSeasonAvailable && capabilities.TvSearchEpAvailable));

            var criteria = new List<SearchCriteriaBase>();

            AddIfNotNull(criteria, IdCriteria(request, capabilities, categories, null, null, absolute));

            if (includeStandardFormat)
            {
                AddIfNotNull(criteria, IdCriteria(request, capabilities, categories, request.SeasonNumber, request.EpisodeNumber.ToString(), null));
            }

            foreach (var queryTitle in QueryTitles(request, capabilities))
            {
                criteria.Add(new BasicSearchCriteria
                {
                    SearchTerm = $"{queryTitle} {absolute}",
                    Categories = categories,
                    SearchType = "search",
                    Source = Source
                });

                if (includeStandardFormat)
                {
                    criteria.Add(TitleCriterion(queryTitle, categories, request.SeasonNumber, request.EpisodeNumber.ToString()));
                }
            }

            if (criteria.Any())
            {
                yield return criteria;
            }
        }

        private IEnumerable<IReadOnlyList<SearchCriteriaBase>> AnimeSeason(AnimeSeasonSearchRequest request, IndexerCapabilities capabilities, int[] categories)
        {
            if ((capabilities != null && !capabilities.SearchAvailable) || request.SeasonNumber <= 0)
            {
                yield break;
            }

            var criteria = new List<SearchCriteriaBase>();
            var includeStandardFormat = _configService.AnimeStandardFormatSearch;
            var rawSearch = capabilities?.SupportsRawSearch == true;
            var seasonQueryTitles = (rawSearch ? request.AllSeasonSceneTitles : request.CleanSeasonSceneTitles)
                .Select(t => t.Replace("+", " "))
                .ToList();

            if (includeStandardFormat)
            {
                AddIfNotNull(criteria, IdCriteria(request, capabilities, categories, request.SeasonNumber, null, null));
            }

            foreach (var queryTitle in QueryTitles(request, capabilities))
            {
                if (seasonQueryTitles.Contains(queryTitle, StringComparer.InvariantCultureIgnoreCase))
                {
                    // A season title alias already identifies the season, no need to add a season number.
                    criteria.Add(new BasicSearchCriteria
                    {
                        SearchTerm = queryTitle,
                        Categories = categories,
                        SearchType = "search",
                        Source = Source
                    });
                }
                else if (includeStandardFormat)
                {
                    criteria.Add(TitleCriterion(queryTitle, categories, request.SeasonNumber, null));
                }
            }

            if (criteria.Any())
            {
                yield return criteria;
            }
        }

        private static IEnumerable<IReadOnlyList<SearchCriteriaBase>> Special(SpecialEpisodeSearchRequest request, IndexerCapabilities capabilities, int[] categories)
        {
            if (capabilities != null && !capabilities.SearchAvailable)
            {
                yield break;
            }

            var criteria = request.EpisodeQueryTitles
                .Select(queryTitle => (SearchCriteriaBase)new BasicSearchCriteria
                {
                    SearchTerm = queryTitle.Replace('+', ' '),
                    Categories = categories,
                    SearchType = "search",
                    Source = Source
                })
                .ToList();

            if (criteria.Any())
            {
                yield return criteria;
            }
        }

        // One request with every ID the indexer supports for the series. Null when it supports none
        // (or has no capabilities yet), as for movies.
        private static TvSearchCriteria IdCriteria(TvSearchRequest request, IndexerCapabilities capabilities, int[] categories, int? season, string episode, string searchTerm)
        {
            if (capabilities == null)
            {
                return null;
            }

            var series = request.Series;
            var tvdbId = capabilities.TvSearchTvdbAvailable && series.TvdbId > 0 ? series.TvdbId : (int?)null;
            var imdbId = capabilities.TvSearchImdbAvailable ? ParseUtil.GetImdbId(series.ImdbId)?.ToString("D7") : null;
            var tvRageId = capabilities.TvSearchTvRageAvailable && series.TvRageId > 0 ? series.TvRageId : (int?)null;
            var tvMazeId = capabilities.TvSearchTvMazeAvailable && series.TvMazeId > 0 ? series.TvMazeId : (int?)null;
            var tmdbId = capabilities.TvSearchTmdbAvailable && series.TmdbId > 0 ? series.TmdbId : (int?)null;

            if (!tvdbId.HasValue && imdbId.IsNullOrWhiteSpace() && !tvRageId.HasValue && !tvMazeId.HasValue && !tmdbId.HasValue)
            {
                return null;
            }

            return new TvSearchCriteria
            {
                TvdbId = tvdbId,
                ImdbId = imdbId,
                RId = tvRageId,
                TvMazeId = tvMazeId,
                TmdbId = tmdbId,
                Season = season,
                Episode = episode,
                SearchTerm = searchTerm,
                Categories = categories,
                SearchType = "tvsearch",
                Source = Source
            };
        }

        private static List<SearchCriteriaBase> TitleCriteria(TvSearchRequest request, IndexerCapabilities capabilities, int[] categories, int? season, string episode)
        {
            if (capabilities != null && !SupportsTitleSearch(capabilities))
            {
                return new List<SearchCriteriaBase>();
            }

            return QueryTitles(request, capabilities)
                .Select(title => TitleCriterion(title, categories, season, episode))
                .ToList();
        }

        private static SearchCriteriaBase TitleCriterion(string title, int[] categories, int? season, string episode)
        {
            return new TvSearchCriteria
            {
                SearchTerm = title,
                Season = season,
                Episode = episode,
                Categories = categories,
                SearchType = "tvsearch",
                Source = Source
            };
        }

        // Sonarr sends the cleaned scene titles, and the original ones too when the indexer's text
        // search is raw. The '+' that cleaning leaves between words is a space in the query.
        private static List<string> QueryTitles(TvSearchRequest request, IndexerCapabilities capabilities)
        {
            var titles = capabilities?.SupportsRawSearch == true ? request.AllSceneTitles : request.CleanSceneTitles;

            return titles
                .Select(t => t.Replace("+", " "))
                .Where(t => t.IsNotNullOrWhiteSpace())
                .Distinct(StringComparer.InvariantCultureIgnoreCase)
                .ToList();
        }

        private static bool SupportsTitleSearch(IndexerCapabilities capabilities)
        {
            return capabilities.TvSearchParams.Contains(TvSearchParam.Q);
        }

        private static bool SupportsIdSearch(IndexerCapabilities capabilities)
        {
            return capabilities.TvSearchTvdbAvailable ||
                   capabilities.TvSearchImdbAvailable ||
                   capabilities.TvSearchTvRageAvailable ||
                   capabilities.TvSearchTvMazeAvailable ||
                   capabilities.TvSearchTmdbAvailable;
        }

        private static void AddIfNotNull(List<SearchCriteriaBase> criteria, SearchCriteriaBase item)
        {
            if (item != null)
            {
                criteria.Add(item);
            }
        }
    }
}
