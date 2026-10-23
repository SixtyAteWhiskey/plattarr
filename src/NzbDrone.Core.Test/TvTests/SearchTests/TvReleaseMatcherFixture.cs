using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Parser;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Tv.Search;

namespace NzbDrone.Core.Test.TvTests.SearchTests
{
    [TestFixture]
    public class TvReleaseMatcherFixture : CoreTest<TvReleaseMatcher>
    {
        private ReleaseInfo _release;
        private TvSearchRequest _request;
        private RemoteEpisode _mapped;

        [SetUp]
        public void SetUp()
        {
            _release = new ReleaseInfo
            {
                Title = "The.Series.S01E01.720p.HDTV.x264-GRP",
                TvdbId = 81189,
                TvRageId = 18164,
                ImdbId = 903747
            };

            _request = new SingleEpisodeSearchRequest { Series = new Series { Title = "The Series" } };
            _mapped = new RemoteEpisode { Series = new Series { Title = "The Series" } };

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TvSearchRequest>()))
                  .Returns(_mapped);
        }

        [Test]
        public void kind_is_tv()
        {
            Subject.Kind.Should().Be(Modules.MediaKind.Tv);
        }

        [Test]
        public void maps_a_parsed_title_with_the_release_ids_and_the_request()
        {
            Subject.Map(_release, _request);

            Mocker.GetMock<IParsingService>().Verify(
                s => s.Map(
                    It.Is<ParsedEpisodeInfo>(p => p.SeriesTitle == "The Series" && p.SeasonNumbers[0] == 1 && p.EpisodeNumbers[0] == 1),
                    81189,
                    18164,
                    "tt0903747",
                    _request),
                Times.Once());
        }

        [Test]
        public void sets_the_release_on_the_mapped_episode()
        {
            var result = Subject.Map(_release, _request);

            result.Should().BeSameAs(_mapped);
            result.Release.Should().BeSameAs(_release);
        }

        [Test]
        public void pads_the_imdb_id_to_seven_digits()
        {
            _release.ImdbId = 123;

            Subject.Map(_release, _request);

            Mocker.GetMock<IParsingService>().Verify(
                s => s.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), "tt0000123", It.IsAny<TvSearchRequest>()),
                Times.Once());
        }

        [Test]
        public void passes_a_null_imdb_id_when_the_release_has_none()
        {
            _release.ImdbId = 0;

            Subject.Map(_release, _request);

            Mocker.GetMock<IParsingService>().Verify(
                s => s.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), null, It.IsAny<TvSearchRequest>()),
                Times.Once());
        }

        [Test]
        public void returns_null_when_the_title_does_not_parse()
        {
            _release.Title = "[] ()";

            Subject.Map(_release, _request).Should().BeNull();

            Mocker.GetMock<IParsingService>().Verify(
                s => s.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TvSearchRequest>()),
                Times.Never());
        }

        [Test]
        public void returns_null_when_the_parsed_series_title_is_blank()
        {
            // A special episode parse with no series title cannot be matched to a series.
            _release.Title = "S01E01.720p.HDTV.x264-GRP";

            Subject.Map(_release, _request).Should().BeNull();
        }

        [Test]
        public void uses_the_special_episode_parse_when_the_title_does_not_parse()
        {
            _release.Title = "[] ()";

            var special = new ParsedEpisodeInfo { SeriesTitle = "The Series", ReleaseTitle = _release.Title, Special = true };

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.ParseSpecialEpisodeTitle(null, _release.Title, 81189, 18164, "tt0903747", _request))
                  .Returns(special);

            Subject.Map(_release, _request);

            Mocker.GetMock<IParsingService>().Verify(
                s => s.Map(special, 81189, 18164, "tt0903747", _request),
                Times.Once());
        }

        [Test]
        public void prefers_the_special_episode_parse_for_episode_zero()
        {
            _release.Title = "The.Series.S01E00.720p.HDTV.x264-GRP";

            var special = new ParsedEpisodeInfo { SeriesTitle = "The Series", ReleaseTitle = _release.Title, Special = true };

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.ParseSpecialEpisodeTitle(It.Is<ParsedEpisodeInfo>(p => p.EpisodeNumbers[0] == 0), _release.Title, 81189, 18164, "tt0903747", _request))
                  .Returns(special);

            Subject.Map(_release, _request);

            Mocker.GetMock<IParsingService>().Verify(
                s => s.Map(special, 81189, 18164, "tt0903747", _request),
                Times.Once());
        }

        [Test]
        public void keeps_the_title_parse_when_the_special_episode_parse_finds_nothing()
        {
            _release.Title = "The.Series.S00E00.720p.HDTV.x264-GRP";

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.ParseSpecialEpisodeTitle(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TvSearchRequest>()))
                  .Returns((ParsedEpisodeInfo)null);

            Subject.Map(_release, _request);

            Mocker.GetMock<IParsingService>().Verify(
                s => s.Map(It.Is<ParsedEpisodeInfo>(p => p.SeriesTitle == "The Series"), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TvSearchRequest>()),
                Times.Once());
        }

        [Test]
        public void does_not_ask_for_a_special_episode_parse_for_a_regular_episode()
        {
            Subject.Map(_release, _request);

            Mocker.GetMock<IParsingService>().Verify(
                s => s.ParseSpecialEpisodeTitle(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TvSearchRequest>()),
                Times.Never());
        }

        [Test]
        public void parses_a_season_title_that_has_no_season_number()
        {
            var title = "The.Series.Complete.Series.720p.BluRay.x264-GRP";

            NzbDrone.Core.Tv.Parser.Parser.ParseTitle(title).Should().BeNull("the fixture needs a title only the season parse reads");
            NzbDrone.Core.Tv.Parser.Parser.ParseSeasonTitle(title).Should().NotBeNull();

            _release.Title = title;

            Subject.Map(_release, _request);

            Mocker.GetMock<IParsingService>().Verify(
                s => s.Map(It.Is<ParsedEpisodeInfo>(p => p.FullSeason), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TvSearchRequest>()),
                Times.Once());
        }

        [Test]
        public void the_contract_map_passes_the_tv_request_through()
        {
            var result = ((Modules.IReleaseMatcher)Subject).Map(_release, _request);

            result.Should().BeSameAs(_mapped);
        }
    }
}
