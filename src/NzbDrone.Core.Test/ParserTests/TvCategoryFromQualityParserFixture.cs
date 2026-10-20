using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class TvCategoryFromQualityParserFixture : CoreTest
    {
        [TestCase("Show.S01E01.2160p.WEB-DL.DDP5.1.x265-GROUP")]
        [TestCase("Show.S01E01.2160p.HDTV.x265-GROUP")]
        [TestCase("Show.S01E01.2160p.BluRay.x265-GROUP")]
        public void should_parse_uhd(string title)
        {
            TvCategoryFromQualityParser.ParseTvShowQuality(title).Should().Be(NewznabStandardCategory.TVUHD);
        }

        [TestCase("Show.S01E01.1080p.WEB-DL.DDP5.1.H.264-GROUP")]
        [TestCase("Show.S01E01.720p.WEBRip.x264-GROUP")]
        [TestCase("Show.S01E01.720p.HDTV.x264-GROUP")]
        [TestCase("Show.S01E01.1080p.BluRay.x264-GROUP")]
        [TestCase("Show.S01E01.PDTV.hr.ws.XviD-GROUP")]
        [TestCase("Show.S01E01.TrollHD-GROUP")]
        public void should_parse_hd(string title)
        {
            TvCategoryFromQualityParser.ParseTvShowQuality(title).Should().Be(NewznabStandardCategory.TVHD);
        }

        [TestCase("Show.S01E01.480p.WEB-DL.x264-GROUP")]
        [TestCase("Show.S01E01.HDTV.XviD-GROUP")]
        [TestCase("Show.S01E01.1080p.BluRay.XviD-GROUP")]
        [TestCase("Show.S01E01.DVDRip.XviD-GROUP")]
        [TestCase("Show.S01E01.PDTV.XviD-GROUP")]
        [TestCase("Show_S01E01_WS_DSR_XviD-GROUP")]
        public void should_parse_sd(string title)
        {
            TvCategoryFromQualityParser.ParseTvShowQuality(title).Should().Be(NewznabStandardCategory.TVSD);
        }

        [TestCase("Show.S01E01-GROUP")]
        [TestCase("Show.S01E01.1080p-GROUP")]
        public void should_fall_back_to_tv(string title)
        {
            TvCategoryFromQualityParser.ParseTvShowQuality(title).Should().Be(NewznabStandardCategory.TV);
        }
    }
}
