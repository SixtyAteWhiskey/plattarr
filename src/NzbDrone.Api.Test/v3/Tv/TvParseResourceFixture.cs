using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Tv.Parse;
using TvParser = NzbDrone.Core.Tv.Parser.Parser;

namespace NzbDrone.Api.Test.v3.Tv
{
    [TestFixture]
    public class TvParseResourceFixture : TestBase
    {
        [Test]
        public void should_map_a_parsed_episode_title()
        {
            var info = TvParser.ParseTitle("Game.of.Thrones.S01E01.1080p.WEB-DL.x264-BLENDER");

            var resource = info.ToResource();

            resource.SeriesTitle.Should().Be("Game of Thrones");
            resource.SeasonNumber.Should().Be(1);
            resource.SeasonNumbers.Should().Equal(1);
            resource.EpisodeNumbers.Should().Equal(1);
            resource.ReleaseGroup.Should().Be("BLENDER");
            resource.ReleaseType.Should().Be(ReleaseType.SingleEpisode);
            resource.FullSeason.Should().BeFalse();
        }

        [Test]
        public void should_map_a_season_pack()
        {
            var info = TvParser.ParseTitle("Game.of.Thrones.S02.1080p.WEB-DL.x264-BLENDER");

            var resource = info.ToResource();

            resource.SeasonNumber.Should().Be(2);
            resource.FullSeason.Should().BeTrue();
            resource.EpisodeNumbers.Should().BeEmpty();
            resource.ReleaseType.Should().Be(ReleaseType.SeasonPack);
        }

        [Test]
        public void should_use_minus_one_when_no_season_was_parsed()
        {
            var resource = new ParsedEpisodeInfo().ToResource();

            resource.SeasonNumber.Should().Be(-1);
        }

        [Test]
        public void should_map_nothing_to_null()
        {
            ((ParsedEpisodeInfo)null).ToResource().Should().BeNull();
        }
    }
}
