using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.DataAugmentation.Scene;
using NzbDrone.Core.Tv.Search;

namespace NzbDrone.Core.Test.TvTests.SearchTests
{
    // Each expectation is the request Sonarr's NewznabRequestGenerator (b3d4bb8) sent for the same
    // criteria, written as the indexer layer's criteria.
    [TestFixture]
    public class TvSearchTranslatorFixture : CoreTest<TvSearchTranslator>
    {
        private Series _series;
        private IndexerDefinition _indexer;

        [SetUp]
        public void SetUp()
        {
            _series = new Series
            {
                Title = "Marvel's Agents of S.H.I.E.L.D.",
                TvdbId = 263365,
                TvRageId = 34770,
                TvMazeId = 1,
                TmdbId = 1403,
                ImdbId = "tt2364582",
                SeriesType = SeriesTypes.Standard
            };

            _indexer = new IndexerDefinition { Id = 1, Capabilities = FullCapabilities() };
        }

        private static IndexerCapabilities FullCapabilities()
        {
            return new IndexerCapabilities
            {
                SearchParams = new List<SearchParam> { SearchParam.Q },
                TvSearchParams = new List<TvSearchParam>
                {
                    TvSearchParam.Q, TvSearchParam.Season, TvSearchParam.Ep, TvSearchParam.ImdbId, TvSearchParam.TvdbId,
                    TvSearchParam.RId, TvSearchParam.TvMazeId, TvSearchParam.TmdbId
                }
            };
        }

        private T Request<T>(Action<T> configure = null)
            where T : TvSearchRequest, new()
        {
            var request = new T
            {
                Series = _series,
                SceneTitles = new List<string> { "Marvel's Agents of S.H.I.E.L.D.", "Agents of SHIELD" },
                Episodes = new List<Episode>()
            };

            configure?.Invoke(request);

            return request;
        }

        private List<IReadOnlyList<SearchCriteriaBase>> Translate(TvSearchRequest request, params int[] categories)
        {
            return Subject.Translate(request, _indexer, categories.Length > 0 ? categories : new[] { 5000 }).ToList();
        }

        private void GivenAnimeStandardFormatSearch()
        {
            Mocker.GetMock<IConfigService>().SetupGet(s => s.AnimeStandardFormatSearch).Returns(true);
        }

        [Test]
        public void single_episode_should_search_ids_first_then_clean_titles()
        {
            var tiers = Translate(Request<SingleEpisodeSearchRequest>(r =>
            {
                r.SeasonNumber = 2;
                r.EpisodeNumber = 5;
            }));

            tiers.Should().HaveCount(2);

            var id = tiers[0].Should().ContainSingle().Which.Should().BeOfType<TvSearchCriteria>().Subject;
            id.TvdbId.Should().Be(263365);
            id.ImdbId.Should().Be("2364582");
            id.RId.Should().Be(34770);
            id.TvMazeId.Should().Be(1);
            id.TmdbId.Should().Be(1403);
            id.Season.Should().Be(2);
            id.Episode.Should().Be("5");
            id.SearchTerm.Should().BeNull();
            id.Categories.Should().Equal(5000);

            tiers[1].Should().OnlyContain(c => c is TvSearchCriteria);
            tiers[1].Select(c => c.SearchTerm).Should().Equal("Marvels Agents of SHIELD", "Agents of SHIELD");
            tiers[1].Cast<TvSearchCriteria>().Should().OnlyContain(c => c.Season == 2 && c.Episode == "5" && c.TvdbId == null);
        }

        [Test]
        public void should_only_send_ids_the_indexer_supports()
        {
            _indexer.Capabilities.TvSearchParams = new List<TvSearchParam> { TvSearchParam.Q, TvSearchParam.Season, TvSearchParam.Ep, TvSearchParam.TvdbId };

            var id = (TvSearchCriteria)Translate(Request<SingleEpisodeSearchRequest>())[0].Single();

            id.TvdbId.Should().Be(263365);
            id.ImdbId.Should().BeNull();
            id.RId.Should().BeNull();
            id.TvMazeId.Should().BeNull();
            id.TmdbId.Should().BeNull();
        }

        [Test]
        public void should_skip_ids_the_series_does_not_have()
        {
            _series.TvRageId = 0;
            _series.TvMazeId = 0;
            _series.TmdbId = 0;
            _series.ImdbId = null;

            var id = (TvSearchCriteria)Translate(Request<SingleEpisodeSearchRequest>())[0].Single();

            id.TvdbId.Should().Be(263365);
            id.ImdbId.Should().BeNull();
            id.RId.Should().BeNull();
            id.TvMazeId.Should().BeNull();
            id.TmdbId.Should().BeNull();
        }

        [Test]
        public void should_search_titles_only_when_the_indexer_has_no_id_search()
        {
            _indexer.Capabilities.TvSearchParams = new List<TvSearchParam> { TvSearchParam.Q, TvSearchParam.Season, TvSearchParam.Ep };

            var tiers = Translate(Request<SingleEpisodeSearchRequest>());

            tiers.Should().ContainSingle();
            tiers[0].Select(c => c.SearchTerm).Should().Equal("Marvels Agents of SHIELD", "Agents of SHIELD");
        }

        [Test]
        public void should_not_search_titles_when_the_indexer_has_no_text_search()
        {
            _indexer.Capabilities.TvSearchParams = new List<TvSearchParam> { TvSearchParam.Season, TvSearchParam.Ep, TvSearchParam.TvdbId };

            var tiers = Translate(Request<SingleEpisodeSearchRequest>());

            tiers.Should().ContainSingle();
            tiers[0].Single().Should().BeOfType<TvSearchCriteria>().Which.TvdbId.Should().Be(263365);
        }

        [Test]
        public void single_episode_needs_season_and_ep_parameters()
        {
            _indexer.Capabilities.TvSearchParams = new List<TvSearchParam> { TvSearchParam.Q, TvSearchParam.Season, TvSearchParam.TvdbId };

            Translate(Request<SingleEpisodeSearchRequest>()).Should().BeEmpty();
        }

        [Test]
        public void season_needs_only_the_season_parameter()
        {
            _indexer.Capabilities.TvSearchParams = new List<TvSearchParam> { TvSearchParam.Q, TvSearchParam.Season, TvSearchParam.TvdbId };

            var tiers = Translate(Request<SeasonSearchRequest>(r => r.SeasonNumber = 3));

            tiers.Should().HaveCount(2);
            var id = (TvSearchCriteria)tiers[0].Single();
            id.Season.Should().Be(3);
            id.Episode.Should().BeNull();
            id.EpisodeSearchString.Should().Be("S03");
        }

        [Test]
        public void season_zero_is_sent_as_a_season_number()
        {
            var id = (TvSearchCriteria)Translate(Request<SeasonSearchRequest>(r => r.SeasonNumber = 0))[0].Single();

            id.Season.Should().Be(0);
        }

        [Test]
        public void daily_episode_should_send_year_and_month_day()
        {
            var tiers = Translate(Request<DailyEpisodeSearchRequest>(r => r.AirDate = new DateTime(2024, 3, 7)));

            tiers.Should().HaveCount(2);

            foreach (var criteria in tiers.SelectMany(t => t).Cast<TvSearchCriteria>())
            {
                criteria.Season.Should().Be(2024);
                criteria.Episode.Should().Be("03/07");
                criteria.EpisodeSearchString.Should().Be("2024.03.07");
            }
        }

        [Test]
        public void daily_season_should_send_the_year_and_need_ep_support()
        {
            var tiers = Translate(Request<DailySeasonSearchRequest>(r => r.Year = 2023));

            tiers.SelectMany(t => t).Cast<TvSearchCriteria>().Should().OnlyContain(c => c.Season == 2023 && c.Episode == null);

            _indexer.Capabilities.TvSearchParams = new List<TvSearchParam> { TvSearchParam.Q, TvSearchParam.Season, TvSearchParam.TvdbId };

            Translate(Request<DailySeasonSearchRequest>(r => r.Year = 2023)).Should().BeEmpty();
        }

        [Test]
        public void id_mode_should_search_ids_only()
        {
            var tiers = Translate(Request<SingleEpisodeSearchRequest>(r => r.SearchMode = SearchMode.SearchID));

            tiers.Should().ContainSingle();
            tiers[0].Single().Should().BeOfType<TvSearchCriteria>().Which.TvdbId.Should().Be(263365);
        }

        [Test]
        public void title_mode_should_search_titles_only()
        {
            var tiers = Translate(Request<SingleEpisodeSearchRequest>(r => r.SearchMode = SearchMode.SearchTitle));

            tiers.Should().ContainSingle();
            tiers[0].Cast<TvSearchCriteria>().Should().OnlyContain(c => c.TvdbId == null && c.SearchTerm != null);
        }

        [Test]
        public void id_and_title_mode_should_search_both_in_one_tier()
        {
            var tiers = Translate(Request<SingleEpisodeSearchRequest>(r => r.SearchMode = SearchMode.SearchID | SearchMode.SearchTitle));

            tiers.Should().ContainSingle();
            tiers[0].Should().HaveCount(3);
            tiers[0].Cast<TvSearchCriteria>().Count(c => c.TvdbId.HasValue).Should().Be(1);
            tiers[0].Cast<TvSearchCriteria>().Count(c => c.SearchTerm != null).Should().Be(2);
        }

        [Test]
        public void raw_text_search_engines_get_the_original_titles_too()
        {
            _indexer.Capabilities.SupportsRawSearch = true;

            var tiers = Translate(Request<SingleEpisodeSearchRequest>(r => r.SearchMode = SearchMode.SearchTitle));

            tiers[0].Select(c => c.SearchTerm).Should().BeEquivalentTo(new[]
            {
                "Marvel's Agents of S.H.I.E.L.D.",
                "Agents of SHIELD",
                "Marvels Agents of SHIELD"
            });
        }

        [Test]
        public void should_use_the_categories_it_is_given()
        {
            Translate(Request<SingleEpisodeSearchRequest>(), 5030, 5040).SelectMany(t => t).Should().OnlyContain(c => c.Categories.SequenceEqual(new[] { 5030, 5040 }));
        }

        [Test]
        public void should_search_titles_when_the_indexer_has_no_capabilities_yet()
        {
            _indexer.Capabilities = null;

            var tiers = Translate(Request<SingleEpisodeSearchRequest>());

            tiers.Should().ContainSingle();
            tiers[0].Should().OnlyContain(c => c.SearchTerm != null);
        }

        [Test]
        public void anime_episode_should_search_the_absolute_number_by_id_and_title()
        {
            var tiers = Translate(Request<AnimeEpisodeSearchRequest>(r =>
            {
                r.SeasonNumber = 1;
                r.EpisodeNumber = 5;
                r.AbsoluteEpisodeNumber = 5;
                r.SceneTitles = new List<string> { "Shingeki no Kyojin" };
            }));

            tiers.Should().ContainSingle();

            var id = tiers[0][0].Should().BeOfType<TvSearchCriteria>().Subject;
            id.TvdbId.Should().Be(263365);
            id.SearchTerm.Should().Be("05");
            id.Season.Should().BeNull();
            id.Episode.Should().BeNull();

            var title = tiers[0][1].Should().BeOfType<BasicSearchCriteria>().Subject;
            title.SearchTerm.Should().Be("Shingeki no Kyojin 05");
            title.SearchType.Should().Be("search");

            tiers[0].Should().HaveCount(2);
        }

        [Test]
        public void anime_episode_should_add_standard_numbering_when_configured()
        {
            GivenAnimeStandardFormatSearch();

            var tiers = Translate(Request<AnimeEpisodeSearchRequest>(r =>
            {
                r.SeasonNumber = 2;
                r.EpisodeNumber = 3;
                r.AbsoluteEpisodeNumber = 27;
                r.SceneTitles = new List<string> { "Shingeki no Kyojin" };
            }));

            tiers.Should().ContainSingle();
            tiers[0].Should().HaveCount(4);

            var idStandard = tiers[0][1].Should().BeOfType<TvSearchCriteria>().Subject;
            idStandard.TvdbId.Should().Be(263365);
            idStandard.Season.Should().Be(2);
            idStandard.Episode.Should().Be("3");
            idStandard.SearchTerm.Should().BeNull();

            tiers[0][2].SearchTerm.Should().Be("Shingeki no Kyojin 27");

            var titleStandard = tiers[0][3].Should().BeOfType<TvSearchCriteria>().Subject;
            titleStandard.SearchTerm.Should().Be("Shingeki no Kyojin");
            titleStandard.Season.Should().Be(2);
            titleStandard.Episode.Should().Be("3");
        }

        [Test]
        public void anime_episode_standard_numbering_needs_a_season_and_episode_number()
        {
            GivenAnimeStandardFormatSearch();

            var tiers = Translate(Request<AnimeEpisodeSearchRequest>(r =>
            {
                r.SeasonNumber = 0;
                r.EpisodeNumber = 3;
                r.AbsoluteEpisodeNumber = 3;
                r.SceneTitles = new List<string> { "Shingeki no Kyojin" };
            }));

            tiers.Should().ContainSingle();
            tiers[0].Should().HaveCount(2);
        }

        [Test]
        public void anime_needs_free_text_search()
        {
            _indexer.Capabilities.SearchParams = new List<SearchParam>();

            Translate(Request<AnimeEpisodeSearchRequest>(r => r.AbsoluteEpisodeNumber = 5)).Should().BeEmpty();
            Translate(Request<AnimeSeasonSearchRequest>(r => r.SeasonNumber = 1)).Should().BeEmpty();
            Translate(Request<SpecialEpisodeSearchRequest>(r => r.EpisodeQueryTitles = new[] { "x" })).Should().BeEmpty();
        }

        [Test]
        public void anime_season_should_search_titles_and_use_season_titles_without_a_number()
        {
            GivenAnimeStandardFormatSearch();

            var tiers = Translate(Request<AnimeSeasonSearchRequest>(r =>
            {
                r.SeasonNumber = 4;
                r.SceneTitles = new List<string> { "Shingeki no Kyojin", "The Final Season" };
                r.SeasonSceneTitles = new List<string> { "The Final Season" };
            }));

            tiers.Should().ContainSingle();

            var id = tiers[0][0].Should().BeOfType<TvSearchCriteria>().Subject;
            id.TvdbId.Should().Be(263365);
            id.Season.Should().Be(4);
            id.Episode.Should().BeNull();

            var plain = tiers[0].Skip(1).ToList();
            plain.Should().HaveCount(2);

            var withSeason = plain[0].Should().BeOfType<TvSearchCriteria>().Subject;
            withSeason.SearchTerm.Should().Be("Shingeki no Kyojin");
            withSeason.Season.Should().Be(4);

            var seasonTitle = plain[1].Should().BeOfType<BasicSearchCriteria>().Subject;
            seasonTitle.SearchTerm.Should().Be("Final Season");
        }

        [Test]
        public void anime_season_without_standard_numbering_only_searches_season_titles()
        {
            var tiers = Translate(Request<AnimeSeasonSearchRequest>(r =>
            {
                r.SeasonNumber = 4;
                r.SceneTitles = new List<string> { "Shingeki no Kyojin", "The Final Season" };
                r.SeasonSceneTitles = new List<string> { "The Final Season" };
            }));

            tiers.Should().ContainSingle();
            tiers[0].Should().ContainSingle().Which.SearchTerm.Should().Be("Final Season");
        }

        [Test]
        public void anime_season_zero_is_not_searched()
        {
            GivenAnimeStandardFormatSearch();

            Translate(Request<AnimeSeasonSearchRequest>(r => r.SeasonNumber = 0)).Should().BeEmpty();
            Translate(Request<AnimeSeasonSearchRequest>(r => r.SeasonNumber = 1)).Should().NotBeEmpty();
        }

        [Test]
        public void special_should_search_each_query_title_as_text()
        {
            var tiers = Translate(Request<SpecialEpisodeSearchRequest>(r => r.EpisodeQueryTitles = new[] { "Agents of SHIELD+Slingshot", "Marvels Agents of SHIELD+Slingshot" }));

            tiers.Should().ContainSingle();
            tiers[0].Should().OnlyContain(c => c is BasicSearchCriteria && c.SearchType == "search");
            tiers[0].Select(c => c.SearchTerm).Should().Equal("Agents of SHIELD Slingshot", "Marvels Agents of SHIELD Slingshot");
        }

        [Test]
        public void should_not_translate_a_request_without_a_shape()
        {
            Translate(new TvSearchRequest { Series = _series, SceneTitles = new List<string> { "x" } }).Should().BeEmpty();
        }
    }
}
