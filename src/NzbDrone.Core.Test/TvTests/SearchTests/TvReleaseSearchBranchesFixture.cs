using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.DataAugmentation.Scene;
using NzbDrone.Core.Tv.DecisionEngine;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Tv.Search;
using IIndexerReleaseFetcher = NzbDrone.Core.IndexerSearch.IIndexerReleaseFetcher;
using IndexerSearchResult = NzbDrone.Core.IndexerSearch.IndexerSearchResult;

namespace NzbDrone.Core.Test.TvTests.SearchTests
{
    // Branch coverage for ReleaseSearchService that the Sonarr-derived fixture does not reach:
    // daily, special and anime routing, monitored filtering, scene mapping season filters and
    // de-duplication. Each test pins a behavior found by mutation testing (docs/module-interface.md,
    // TV step 4).
    [TestFixture]
    public class TvReleaseSearchBranchesFixture : CoreTest<ReleaseSearchService>
    {
        private Series _series;
        private List<Episode> _episodes;
        private List<TvSearchRequest> _requests;
        private List<DownloadDecision> _searchDecisions;

        [SetUp]
        public void Setup()
        {
            _series = new Series { Id = 1, TvdbId = 100, Title = "Show", CleanTitle = "show", SeriesType = SeriesTypes.Standard, Tags = new HashSet<int>() };
            _episodes = new List<Episode>();
            _requests = new List<TvSearchRequest>();
            _searchDecisions = new List<DownloadDecision>();

            Mocker.GetMock<ISeriesService>().Setup(s => s.GetSeries(_series.Id)).Returns(_series);

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodesBySeason(_series.Id, It.IsAny<int>()))
                  .Returns<int, int>((_, season) => _episodes.Where(e => e.SeasonNumber == season).ToList());

            Mocker.GetMock<ISceneMappingService>().Setup(s => s.FindByTvdbId(It.IsAny<int>())).Returns(new List<SceneMapping>());
            Mocker.GetMock<ISceneMappingService>()
                  .Setup(s => s.GetSceneNames(It.IsAny<int>(), It.IsAny<List<int>>(), It.IsAny<List<int>>()))
                  .Returns(new List<string>());

            Mocker.GetMock<IMakeDownloadDecision>()
                  .Setup(s => s.GetSearchDecision(It.IsAny<List<ReleaseInfo>>(), It.IsAny<TvSearchRequest>()))
                  .Returns(() => _searchDecisions);

            Mocker.GetMock<IIndexerReleaseFetcher>()
                  .Setup(f => f.Search(It.IsAny<TvSearchRequest>(), It.IsAny<IEnumerable<int>>(), MediaKind.Tv, It.IsAny<ISearchTranslator<TvSearchRequest>>()))
                  .Callback<TvSearchRequest, IEnumerable<int>, MediaKind, ISearchTranslator<TvSearchRequest>>((request, _, _, _) => _requests.Add(request))
                  .ReturnsAsync(new IndexerSearchResult(new List<ReleaseInfo>(), 1));
        }

        private Episode GivenEpisode(int id, int season, int number, string airDate = null, bool monitored = true, string title = null, int? absolute = null, int? sceneAbsolute = null)
        {
            var episode = new Episode
            {
                Id = id,
                SeriesId = _series.Id,
                Series = _series,
                SeasonNumber = season,
                EpisodeNumber = number,
                Monitored = monitored,
                Title = title,
                AirDate = airDate,
                AirDateUtc = airDate == null ? null : DateTime.Parse(airDate).ToUniversalTime(),
                AbsoluteEpisodeNumber = absolute,
                SceneAbsoluteEpisodeNumber = sceneAbsolute
            };

            _episodes.Add(episode);

            return episode;
        }

        [Test]
        public async Task daily_episode_without_air_date_should_fail_the_search()
        {
            _series.SeriesType = SeriesTypes.Daily;
            var episode = GivenEpisode(1, 1, 1);

            Func<Task> search = () => Subject.EpisodeSearch(episode, true, false);

            await search.Should().ThrowAsync<SearchFailedException>();
            _requests.Should().BeEmpty();
        }

        [Test]
        public async Task daily_episode_should_search_by_air_date()
        {
            _series.SeriesType = SeriesTypes.Daily;
            var episode = GivenEpisode(1, 1, 1, "2020-03-04");

            await Subject.EpisodeSearch(episode, true, false);

            _requests.Should().ContainSingle().Which.Should().BeOfType<DailyEpisodeSearchRequest>()
                     .Which.AirDate.Should().Be(new DateTime(2020, 3, 4));
        }

        [Test]
        public async Task daily_search_should_add_the_series_title_to_the_scene_titles()
        {
            _series.SeriesType = SeriesTypes.Daily;
            var episode = GivenEpisode(1, 1, 1, "2020-03-04");

            await Subject.EpisodeSearch(episode, true, false);

            _requests.Single().SceneTitles.Should().Contain("Show");
        }

        [Test]
        public async Task daily_search_should_not_duplicate_the_series_title_when_a_scene_name_has_it()
        {
            _series.SeriesType = SeriesTypes.Daily;
            var episode = GivenEpisode(1, 1, 1, "2020-03-04");

            Mocker.GetMock<ISceneMappingService>()
                  .Setup(s => s.GetSceneNames(It.IsAny<int>(), It.IsAny<List<int>>(), It.IsAny<List<int>>()))
                  .Returns(new List<string> { "SHOW" });

            await Subject.EpisodeSearch(episode, true, false);

            _requests.Single().SceneTitles.Should().Equal("SHOW");
        }

        [Test]
        public async Task season_search_should_carry_the_monitored_only_flag_into_the_request()
        {
            _series.SeriesType = SeriesTypes.Daily;
            GivenEpisode(1, 1, 1, "2020-03-04");
            GivenEpisode(2, 1, 2, "2020-03-05");

            await Subject.SeasonSearch(_series.Id, 1, false, true, true, false);

            _requests.Should().ContainSingle().Which.MonitoredEpisodesOnly.Should().BeTrue();
        }

        [Test]
        public async Task daily_season_search_should_skip_episodes_without_an_air_date()
        {
            _series.SeriesType = SeriesTypes.Daily;
            GivenEpisode(1, 1, 1);
            GivenEpisode(2, 1, 2, "2020-03-05");

            await Subject.SeasonSearch(_series.Id, 1, false, false, true, false);

            _requests.Should().ContainSingle().Which.Should().BeOfType<DailyEpisodeSearchRequest>();
        }

        [Test]
        public async Task daily_season_search_should_skip_unmonitored_episodes_unless_interactive()
        {
            _series.SeriesType = SeriesTypes.Daily;
            GivenEpisode(1, 1, 1, "2020-03-04", monitored: false);
            GivenEpisode(2, 1, 2, "2020-03-05");

            await Subject.SeasonSearch(_series.Id, 1, false, true, true, false);

            _requests.Should().ContainSingle().Which.Episodes.Select(e => e.Id).Should().Equal(2);

            _requests.Clear();

            await Subject.SeasonSearch(_series.Id, 1, false, true, true, true);

            _requests.Should().ContainSingle().Which.Should().BeOfType<DailySeasonSearchRequest>()
                     .Which.Episodes.Select(e => e.Id).Should().BeEquivalentTo(new[] { 1, 2 });
        }

        [Test]
        public async Task special_episode_should_be_searched_as_special()
        {
            var episode = GivenEpisode(1, 0, 1, "2020-03-04", title: "Making Of");

            await Subject.EpisodeSearch(episode, true, false);

            _requests.OfType<SpecialEpisodeSearchRequest>().Should().ContainSingle()
                     .Which.EpisodeQueryTitles.Should().ContainSingle(t => t.Contains("Making+Of"));
        }

        [Test]
        public async Task anime_special_without_absolute_numbers_should_be_searched_as_special()
        {
            _series.SeriesType = SeriesTypes.Anime;
            var episode = GivenEpisode(1, 0, 1, "2020-03-04", title: "Recap");

            await Subject.EpisodeSearch(episode, true, false);

            _requests.OfType<SpecialEpisodeSearchRequest>().Should().ContainSingle();
            _requests.OfType<AnimeEpisodeSearchRequest>().Should().BeEmpty();
        }

        [Test]
        public async Task anime_special_with_an_absolute_number_should_be_searched_as_anime()
        {
            _series.SeriesType = SeriesTypes.Anime;
            var episode = GivenEpisode(1, 0, 1, "2020-03-04", absolute: 13);

            await Subject.EpisodeSearch(episode, true, false);

            _requests.OfType<AnimeEpisodeSearchRequest>().Should().ContainSingle();
            _requests.OfType<SpecialEpisodeSearchRequest>().Should().BeEmpty();
        }

        [Test]
        public async Task anime_search_should_prefer_the_scene_absolute_number()
        {
            _series.SeriesType = SeriesTypes.Anime;
            var episode = GivenEpisode(1, 1, 5, "2020-03-04", absolute: 5, sceneAbsolute: 7);

            await Subject.EpisodeSearch(episode, true, false);

            _requests.OfType<AnimeEpisodeSearchRequest>().Single().AbsoluteEpisodeNumber.Should().Be(7);
        }

        [Test]
        public async Task anime_search_should_use_the_absolute_number_without_a_scene_one()
        {
            _series.SeriesType = SeriesTypes.Anime;
            var episode = GivenEpisode(1, 1, 5, "2020-03-04", absolute: 5);

            await Subject.EpisodeSearch(episode, true, false);

            _requests.OfType<AnimeEpisodeSearchRequest>().Single().AbsoluteEpisodeNumber.Should().Be(5);
        }

        [Test]
        public async Task season_search_with_no_episodes_should_not_search_or_load_the_series()
        {
            var decisions = await Subject.SeasonSearch(_series.Id, 3, false, true, true, false);

            decisions.Should().BeEmpty();
            _requests.Should().BeEmpty();
            Mocker.GetMock<ISeriesService>().Verify(v => v.GetSeries(It.IsAny<int>()), Times.Never());
        }

        [Test]
        public async Task season_search_of_specials_should_search_as_special()
        {
            GivenEpisode(1, 0, 1, "2020-03-04", title: "Making Of");
            GivenEpisode(2, 0, 2, "2020-03-05", title: "Outtakes");

            await Subject.SeasonSearch(_series.Id, 0, false, true, true, false);

            _requests.OfType<SpecialEpisodeSearchRequest>().Should().ContainSingle();
            _requests.OfType<SeasonSearchRequest>().Should().BeEmpty();
        }

        [Test]
        public async Task special_search_should_only_use_monitored_episode_titles_unless_interactive()
        {
            GivenEpisode(1, 0, 1, "2020-03-04", title: "Making Of");
            GivenEpisode(2, 0, 2, "2020-03-05", title: "Outtakes", monitored: false);

            await Subject.SeasonSearch(_series.Id, 0, false, true, true, false);

            var special = _requests.OfType<SpecialEpisodeSearchRequest>().Single();
            special.EpisodeQueryTitles.Should().OnlyContain(t => t.Contains("Making+Of"));

            // One by-number search for the monitored episode only.
            _requests.OfType<SingleEpisodeSearchRequest>().Should().ContainSingle()
                     .Which.Episodes.Select(e => e.Id).Should().Equal(1);

            _requests.Clear();

            await Subject.SeasonSearch(_series.Id, 0, false, true, true, true);

            _requests.OfType<SpecialEpisodeSearchRequest>().Single().EpisodeQueryTitles.Should().HaveCount(2);
            _requests.OfType<SingleEpisodeSearchRequest>().Should().HaveCount(2);
        }

        [Test]
        public async Task special_search_should_query_each_distinct_episode_title_once()
        {
            GivenEpisode(1, 0, 1, "2020-03-04", title: "Making Of");
            GivenEpisode(2, 0, 2, "2020-03-05", title: "MAKING OF");

            await Subject.SeasonSearch(_series.Id, 0, false, true, true, false);

            _requests.OfType<SpecialEpisodeSearchRequest>().Single().EpisodeQueryTitles.Should().ContainSingle();
        }

        [Test]
        public async Task season_scoped_scene_mapping_should_only_apply_to_that_season()
        {
            var episode1 = GivenEpisode(1, 1, 1, "2020-03-04");
            var episode2 = GivenEpisode(2, 2, 1, "2020-03-05");

            Mocker.GetMock<ISceneMappingService>()
                  .Setup(s => s.FindByTvdbId(_series.TvdbId))
                  .Returns(new List<SceneMapping>
                           {
                               new SceneMapping { SearchTerm = "Alt Show", ParseTerm = "altshow", SeasonNumber = 2, SceneSeasonNumber = -1, SceneOrigin = "tvdb", TvdbId = _series.TvdbId }
                           });

            await Subject.EpisodeSearch(episode1, true, false);
            await Subject.EpisodeSearch(episode2, true, false);

            _requests[0].SceneTitles.Should().NotContain("Alt Show");
            _requests.Skip(1).SelectMany(r => r.SceneTitles).Should().Contain("Alt Show");
        }

        [Test]
        public async Task should_keep_the_decision_with_the_fewest_rejections_for_a_duplicate_guid()
        {
            var many = new DownloadDecision(
                new RemoteEpisode { Release = new ReleaseInfo { Guid = "same", IndexerPriority = 25 } },
                new DownloadRejection(DownloadRejectionReason.MinimumAgeDelay, "a"),
                new DownloadRejection(DownloadRejectionReason.MinimumAgeDelay, "b"));

            var few = new DownloadDecision(new RemoteEpisode { Release = new ReleaseInfo { Guid = "same", IndexerPriority = 25 } });

            _searchDecisions.Add(many);
            _searchDecisions.Add(few);

            var episode = GivenEpisode(1, 1, 1, "2020-03-04");

            var decisions = await Subject.EpisodeSearch(episode, true, false);

            decisions.Should().ContainSingle().Which.Should().BeSameAs(few);
        }
    }
}
