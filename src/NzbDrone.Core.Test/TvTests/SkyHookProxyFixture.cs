using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.DataAugmentation.DailySeries;
using NzbDrone.Core.Tv.MetadataSource.SkyHook;
using NzbDrone.Core.Tv.MetadataSource.SkyHook.Resource;

namespace NzbDrone.Core.Test.TvTests
{
    // Hermetic SkyHook tests: every response is the recorded Game of Thrones payload
    // (Files/SkyHook/tvdb-121361-official.json), no network involved.
    [TestFixture]
    public class SkyHookProxyFixture : CoreTest
    {
        private SkyHookProxy _proxy;
        private HttpRequest _lastRequest;

        [SetUp]
        public void Setup()
        {
            _proxy = SkyHookTestData.CreateProxy(Mocker);
            CaptureRequests();
        }

        private void CaptureRequests(HttpResponse<ShowResource> response = null)
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Get<ShowResource>(It.IsAny<HttpRequest>()))
                  .Callback<HttpRequest>(r => _lastRequest = r)
                  .Returns(response ?? SkyHookTestData.Response(SkyHookTestData.GameOfThronesJson));
        }

        private void GivenSearchResponse(string json)
        {
            var response = new HttpResponse<List<ShowResource>>(new HttpResponse(new HttpRequest("https://skyhook.test/"), new HttpHeader(), json, HttpStatusCode.OK));

            Mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Get<List<ShowResource>>(It.IsAny<HttpRequest>()))
                  .Callback<HttpRequest>(r => _lastRequest = r)
                  .Returns(response);
        }

        private static string SearchJson(string showJson)
        {
            return $"[{showJson}]";
        }

        [Test]
        public void should_request_the_tvdb_route_with_three_letter_language_and_season_type()
        {
            _proxy.GetSeriesInfo(SkyHookTestData.GameOfThronesTvdbId, Language.French, SeasonType.Official);

            _lastRequest.Url.FullUri.Should().Contain("/tvdb/shows/fra/121361/official");
            _lastRequest.AllowAutoRedirect.Should().BeTrue();
            _lastRequest.SuppressHttpError.Should().BeTrue();
        }

        [Test]
        public void should_fall_back_to_english_for_an_unknown_language()
        {
            _proxy.GetSeriesInfo(SkyHookTestData.GameOfThronesTvdbId, Language.Unknown, SeasonType.Official);

            _lastRequest.Url.FullUri.Should().Contain("/tvdb/shows/eng/121361/official");
        }

        [Test]
        public void should_map_series_fields()
        {
            var series = _proxy.GetSeriesInfo(SkyHookTestData.GameOfThronesTvdbId, Language.English, SeasonType.Official).Item1;

            series.TvdbId.Should().Be(121361);
            series.TmdbId.Should().Be(1399);
            series.TvRageId.Should().Be(24493);
            series.TvMazeId.Should().Be(82);
            series.ImdbId.Should().Be("tt0944947");
            series.Title.Should().Be("Game of Thrones");
            series.CleanTitle.Should().Be("gamethrones");
            series.TitleSlug.Should().Be("game-of-thrones");
            series.OriginalLanguage.Should().Be(Language.English);
            series.OriginalCountry.Should().Be("usa");
            series.Network.Should().Be("HBO");
            series.Runtime.Should().Be(57);
            series.AirTime.Should().Be("21:00");
            series.Certification.Should().Be("TV-MA");
            series.Status.Should().Be(SeriesStatusType.Ended);
            series.Year.Should().Be(2011);
            series.FirstAired.Should().Be(new DateTime(2011, 4, 17, 0, 0, 0, DateTimeKind.Utc));
            series.LastAired.Should().Be(new DateTime(2019, 5, 19, 0, 0, 0, DateTimeKind.Utc));
            series.Genres.Should().BeEquivalentTo(new[] { "Action", "Adventure", "Drama", "Fantasy" });
            series.Ratings.Votes.Should().Be(2676466);
            series.Ratings.Value.Should().Be(9.2m);
            series.Monitored.Should().BeTrue();
            series.SeriesType.Should().Be(SeriesTypes.Standard);
        }

        [Test]
        public void should_map_seasons_images_actors_season_types_and_translations()
        {
            var series = _proxy.GetSeriesInfo(SkyHookTestData.GameOfThronesTvdbId, Language.English, SeasonType.Official).Item1;

            series.Seasons.Select(s => s.SeasonNumber).Should().Equal(Enumerable.Range(0, 9));
            series.Seasons.Single(s => s.SeasonNumber == 0).Monitored.Should().BeFalse();
            series.Seasons.Where(s => s.SeasonNumber > 0).Should().OnlyContain(s => s.Monitored);
            series.Seasons.Single(s => s.SeasonNumber == 1).Title.Should().Be("Winter is Coming");

            series.Images.Select(i => i.CoverType).Should().BeEquivalentTo(new[]
            {
                MediaCoverTypes.Banner, MediaCoverTypes.Poster, MediaCoverTypes.Fanart, MediaCoverTypes.Clearlogo
            });

            series.Actors.Should().HaveCount(44);
            series.Actors.First().Name.Should().Be("Sean Bean");
            series.Actors.First().Character.Should().Be("Ned Stark");
            series.Actors.First().Images.Single().CoverType.Should().Be(MediaCoverTypes.Headshot);

            series.SeasonTypes.Select(t => t.Type).Should().Contain(new[] { "official", "dvd" });
            series.SeasonTypes.First().EpisodeCount.Should().Be(73);

            series.Translations.Should().NotBeEmpty();
            series.Translations.Should().Contain(t => t.Language == Language.English && t.Title == "Game of Thrones");
            series.Translations.Should().Contain(t => t.Title == "Hra o trůny");
        }

        [Test]
        public void should_map_episodes()
        {
            var episodes = _proxy.GetSeriesInfo(SkyHookTestData.GameOfThronesTvdbId, Language.English, SeasonType.Official).Item2;

            episodes.Should().HaveCount(128);
            episodes.Count(e => e.SeasonNumber == 0).Should().Be(55);

            var first = episodes.First();
            first.TvdbId.Should().Be(3226241);
            first.SeasonNumber.Should().Be(0);
            first.EpisodeNumber.Should().Be(1);
            first.Title.Should().Be("Inside Game of Thrones");
            first.AirDate.Should().Be("2010-12-05");
            first.AirDateUtc.Should().Be(new DateTime(2010, 12, 6, 2, 0, 0, DateTimeKind.Utc));
            first.Images.Single().CoverType.Should().Be(MediaCoverTypes.Screenshot);
        }

        [Test]
        public void should_mark_the_series_daily_when_the_daily_list_says_so()
        {
            Mocker.GetMock<IDailySeriesService>()
                  .Setup(s => s.IsDailySeries(SkyHookTestData.GameOfThronesTvdbId))
                  .Returns(true);

            var series = _proxy.GetSeriesInfo(SkyHookTestData.GameOfThronesTvdbId, Language.English, SeasonType.Official).Item1;

            series.SeriesType.Should().Be(SeriesTypes.Daily);
        }

        [Test]
        public void should_throw_series_not_found_on_404()
        {
            CaptureRequests(SkyHookTestData.Response("{}", HttpStatusCode.NotFound));

            Assert.Throws<SeriesNotFoundException>(() => _proxy.GetSeriesInfo(999999999, Language.English, SeasonType.Official))
                  .TvdbSeriesId.Should().Be(999999999);
        }

        [Test]
        public void should_throw_http_exception_on_other_http_errors()
        {
            CaptureRequests(SkyHookTestData.Response("{}", HttpStatusCode.InternalServerError));

            Assert.Throws<HttpException>(() => _proxy.GetSeriesInfo(SkyHookTestData.GameOfThronesTvdbId, Language.English, SeasonType.Official));
        }

        [TestCase("tvdb:")]
        [TestCase("tvdb:abc")]
        [TestCase("tvdb:-5")]
        [TestCase("tvdb:0")]
        [TestCase("tvdbid:12 34")]
        public void should_return_empty_for_an_invalid_tvdb_search_term(string term)
        {
            _proxy.SearchForNewSeries(term, Language.English).Should().BeEmpty();

            Mocker.GetMock<IHttpClient>().Verify(c => c.Get<ShowResource>(It.IsAny<HttpRequest>()), Times.Never());
        }

        [Test]
        public void should_return_the_library_series_for_a_tvdb_search_without_calling_skyhook()
        {
            var existing = new Series { TvdbId = 121361, Title = "Already Here" };

            Mocker.GetMock<ISeriesService>().Setup(s => s.FindByTvdbId(121361)).Returns(existing);

            var result = _proxy.SearchForNewSeries("tvdb:121361", Language.English);

            result.Should().ContainSingle().Which.Should().BeSameAs(existing);
            Mocker.GetMock<IHttpClient>().Verify(c => c.Get<ShowResource>(It.IsAny<HttpRequest>()), Times.Never());
        }

        [Test]
        public void should_look_up_a_tvdb_search_term_that_is_not_in_the_library()
        {
            var result = _proxy.SearchForNewSeries("TVDBID: 121361", Language.English);

            result.Should().ContainSingle().Which.Title.Should().Be("Game of Thrones");
            _lastRequest.Url.FullUri.Should().Contain("/shows/eng/121361/official");
        }

        [Test]
        public void should_return_empty_when_the_tvdb_search_term_is_unknown_to_skyhook()
        {
            CaptureRequests(SkyHookTestData.Response("{}", HttpStatusCode.NotFound));

            _proxy.SearchForNewSeries("tvdb:999999999", Language.English).Should().BeEmpty();
        }

        [Test]
        public void should_send_a_lowercase_trimmed_term_and_language_for_a_title_search()
        {
            GivenSearchResponse(SearchJson(SkyHookTestData.GameOfThronesJson));

            var result = _proxy.SearchForNewSeries("  Game Of Thrones ", Language.German);

            result.Should().ContainSingle().Which.TvdbId.Should().Be(121361);
            _lastRequest.Url.FullUri.Should().Contain("/search");
            _lastRequest.Url.FullUri.Should().Contain("language=deu");
            _lastRequest.Url.FullUri.Should().Contain("term=game%20of%20thrones");
        }

        [Test]
        public void should_prefer_the_library_series_over_the_remote_one_in_title_results()
        {
            var existing = new Series { TvdbId = 121361, Title = "Already Here" };

            GivenSearchResponse(SearchJson(SkyHookTestData.GameOfThronesJson));
            Mocker.GetMock<ISeriesService>().Setup(s => s.FindByTvdbId(121361)).Returns(existing);

            _proxy.SearchForNewSeries("game of thrones", Language.English).Should().ContainSingle().Which.Should().BeSameAs(existing);
        }

        [TestCase("/tmp/game of thrones")]
        [TestCase("/")]
        public void should_reject_search_terms_that_look_like_paths(string term)
        {
            Assert.Throws<InvalidSearchTermException>(() => _proxy.SearchForNewSeries(term, Language.English));
        }

        [Test]
        public void should_wrap_http_failures_in_a_skyhook_exception()
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Get<List<ShowResource>>(It.IsAny<HttpRequest>()))
                  .Throws(new HttpException(new HttpRequest("https://skyhook.test/"), new HttpResponse(new HttpRequest("https://skyhook.test/"), new HttpHeader(), string.Empty, HttpStatusCode.BadGateway)));

            Assert.Throws<SkyHookException>(() => _proxy.SearchForNewSeries("game of thrones", Language.English))
                  .Message.Should().Contain("Unable to communicate with SkyHook");
        }

        [Test]
        public void should_wrap_unparseable_responses_in_a_skyhook_exception()
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Get<List<ShowResource>>(It.IsAny<HttpRequest>()))
                  .Throws(new InvalidOperationException("boom"));

            Assert.Throws<SkyHookException>(() => _proxy.SearchForNewSeries("game of thrones", Language.English))
                  .Message.Should().Contain("Invalid response received from SkyHook");
        }

        [TestCase("tt0944947", "imdb:tt0944947")]
        [TestCase("0944947", "imdb:tt0944947")]
        public void should_search_by_a_normalized_imdb_id(string imdbId, string expectedTerm)
        {
            GivenSearchResponse(SearchJson(SkyHookTestData.GameOfThronesJson));

            _proxy.SearchForNewSeriesByImdbId(imdbId, Language.English).Should().ContainSingle();

            _lastRequest.Url.FullUri.Should().Contain("term=" + Uri.EscapeDataString(expectedTerm));
        }

        [Test]
        public void should_return_empty_for_an_unparseable_imdb_id()
        {
            _proxy.SearchForNewSeriesByImdbId("not-an-id", Language.English).Should().BeEmpty();
        }

        [Test]
        public void should_search_by_tmdb_anilist_and_mal_ids()
        {
            GivenSearchResponse(SearchJson(SkyHookTestData.GameOfThronesJson));

            _proxy.SearchForNewSeriesByTmdbId(1399, Language.English);
            _lastRequest.Url.FullUri.Should().Contain("term=" + Uri.EscapeDataString("tmdb:1399"));

            _proxy.SearchForNewSeriesByAniListId(5, Language.English);
            _lastRequest.Url.FullUri.Should().Contain("term=" + Uri.EscapeDataString("anilist:5"));

            _proxy.SearchForNewSeriesByMyAnimeListId(7, Language.English);
            _lastRequest.Url.FullUri.Should().Contain("term=" + Uri.EscapeDataString("mal:7"));
        }
    }
}
