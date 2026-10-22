using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Qualities.Upstream;
using NzbDrone.Core.Tv.Parser;

namespace NzbDrone.Core.Test.TvTests.ParserTests
{
    [TestFixture]
    public class TvQualityFinderFixture
    {
        // Sonarr's own QualityFinderFixture cases, over the TV source vocabulary.
        [TestCase(TvQualitySource.Television, 480)]
        [TestCase(TvQualitySource.Unknown, 480)]
        public void should_return_SDTV(TvQualitySource source, int resolution)
        {
            TvQualityFinder.FindBySourceAndResolution(source, resolution).Should().Be(Quality.SDTV);
        }

        [TestCase(TvQualitySource.Television, 720)]
        [TestCase(TvQualitySource.Unknown, 720)]
        public void should_return_HDTV_720p(TvQualitySource source, int resolution)
        {
            TvQualityFinder.FindBySourceAndResolution(source, resolution).Should().Be(Quality.HDTV720p);
        }

        [TestCase(TvQualitySource.Television, 1080)]
        [TestCase(TvQualitySource.Unknown, 1080)]
        public void should_return_HDTV_1080p(TvQualitySource source, int resolution)
        {
            TvQualityFinder.FindBySourceAndResolution(source, resolution).Should().Be(Quality.HDTV1080p);
        }

        [TestCase(TvQualitySource.Bluray, 720)]
        [TestCase(TvQualitySource.Dvd, 720)]
        public void should_return_Bluray720p(TvQualitySource source, int resolution)
        {
            TvQualityFinder.FindBySourceAndResolution(source, resolution).Should().Be(Quality.Bluray720p);
        }

        // Exact matches in the remux and bluray rows.
        [TestCase(TvQualitySource.BlurayRaw, 1080)]
        public void should_return_remux_1080p(TvQualitySource source, int resolution)
        {
            TvQualityFinder.FindBySourceAndResolution(source, resolution).Should().Be(Quality.Remux1080p);
        }

        [TestCase(TvQualitySource.BlurayRaw, 2160)]
        public void should_return_remux_2160p(TvQualitySource source, int resolution)
        {
            TvQualityFinder.FindBySourceAndResolution(source, resolution).Should().Be(Quality.Remux2160p);
        }

        // Sonarr has no 720p remux: the search finds nothing at or above the raw source.
        [Test]
        public void should_return_unknown_for_remux_720p()
        {
            TvQualityFinder.FindBySourceAndResolution(TvQualitySource.BlurayRaw, 720).Should().Be(Quality.Unknown);
        }

        // 576p with a television or web source must not roll up to Bluray-576p.
        [TestCase(TvQualitySource.Television, 576, "SDTV")]
        [TestCase(TvQualitySource.Web, 576, "WEBDL-480p")]
        [TestCase(TvQualitySource.WebRip, 576, "WEBRip-480p")]
        [TestCase(TvQualitySource.Bluray, 576, "Bluray-576p")]
        public void should_not_roll_576p_into_bluray(TvQualitySource source, int resolution, string expected)
        {
            TvQualityFinder.FindBySourceAndResolution(source, resolution).Name.Should().Be(expected);
        }

        // The table is Sonarr's quality list expressed in catalog qualities: the same set as
        // the importer's Sonarr ID map, so the parser cannot return a quality Sonarr never had.
        [Test]
        public void table_should_hold_exactly_the_qualities_sonarr_defines()
        {
            var table = TvQualityFinder.Qualities.Select(q => q.Id).OrderBy(i => i).ToList();
            var sonarr = SonarrQualityIds.UpstreamToCatalog.Values.OrderBy(i => i).ToList();

            table.Should().Equal(sonarr);
        }

        [Test]
        public void every_table_quality_should_have_a_source_other_than_unknown_except_unknown()
        {
            foreach (var quality in TvQualityFinder.Qualities.Where(q => q != Quality.Unknown))
            {
                TvQualityFinder.SourceOf(quality).Should().NotBe(TvQualitySource.Unknown, quality.Name);
            }
        }

        [Test]
        public void a_catalog_quality_sonarr_does_not_define_should_have_no_source()
        {
            TvQualityFinder.SourceOf(Quality.CAM).Should().Be(TvQualitySource.Unknown);
        }
    }
}
