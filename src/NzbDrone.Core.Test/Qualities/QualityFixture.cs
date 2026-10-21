using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Qualities
{
    [TestFixture]
    public class QualityFixture : CoreTest
    {
        // Catalog IDs are stored in every database; they must never change
        // (docs/catalogs.md). These cases pin every one.
        public static object[] FromIntCases =
                {
                        new object[] { 0, Quality.Unknown },
                        new object[] { 1, Quality.WORKPRINT },
                        new object[] { 2, Quality.CAM },
                        new object[] { 3, Quality.TELESYNC },
                        new object[] { 4, Quality.TELECINE },
                        new object[] { 5, Quality.REGIONAL },
                        new object[] { 6, Quality.DVDSCR },
                        new object[] { 7, Quality.SDTV },
                        new object[] { 8, Quality.DVD },
                        new object[] { 9, Quality.DVDR },
                        new object[] { 10, Quality.WEBDL480p },
                        new object[] { 11, Quality.WEBRip480p },
                        new object[] { 12, Quality.Bluray480p },
                        new object[] { 13, Quality.Bluray576p },
                        new object[] { 14, Quality.HDTV720p },
                        new object[] { 15, Quality.WEBDL720p },
                        new object[] { 16, Quality.WEBRip720p },
                        new object[] { 17, Quality.Bluray720p },
                        new object[] { 18, Quality.HDTV1080p },
                        new object[] { 19, Quality.WEBDL1080p },
                        new object[] { 20, Quality.WEBRip1080p },
                        new object[] { 21, Quality.Bluray1080p },
                        new object[] { 22, Quality.Remux1080p },
                        new object[] { 23, Quality.HDTV2160p },
                        new object[] { 24, Quality.WEBDL2160p },
                        new object[] { 25, Quality.WEBRip2160p },
                        new object[] { 26, Quality.Bluray2160p },
                        new object[] { 27, Quality.Remux2160p },
                        new object[] { 28, Quality.BRDISK },
                        new object[] { 29, Quality.RAWHD },
                };

        public static object[] ToIntCases =
                {
                        new object[] { Quality.Unknown, 0 },
                        new object[] { Quality.WORKPRINT, 1 },
                        new object[] { Quality.CAM, 2 },
                        new object[] { Quality.TELESYNC, 3 },
                        new object[] { Quality.TELECINE, 4 },
                        new object[] { Quality.REGIONAL, 5 },
                        new object[] { Quality.DVDSCR, 6 },
                        new object[] { Quality.SDTV, 7 },
                        new object[] { Quality.DVD, 8 },
                        new object[] { Quality.DVDR, 9 },
                        new object[] { Quality.WEBDL480p, 10 },
                        new object[] { Quality.WEBRip480p, 11 },
                        new object[] { Quality.Bluray480p, 12 },
                        new object[] { Quality.Bluray576p, 13 },
                        new object[] { Quality.HDTV720p, 14 },
                        new object[] { Quality.WEBDL720p, 15 },
                        new object[] { Quality.WEBRip720p, 16 },
                        new object[] { Quality.Bluray720p, 17 },
                        new object[] { Quality.HDTV1080p, 18 },
                        new object[] { Quality.WEBDL1080p, 19 },
                        new object[] { Quality.WEBRip1080p, 20 },
                        new object[] { Quality.Bluray1080p, 21 },
                        new object[] { Quality.Remux1080p, 22 },
                        new object[] { Quality.HDTV2160p, 23 },
                        new object[] { Quality.WEBDL2160p, 24 },
                        new object[] { Quality.WEBRip2160p, 25 },
                        new object[] { Quality.Bluray2160p, 26 },
                        new object[] { Quality.Remux2160p, 27 },
                        new object[] { Quality.BRDISK, 28 },
                        new object[] { Quality.RAWHD, 29 },
                };

        [Test]
        [TestCaseSource("FromIntCases")]
        public void should_be_able_to_convert_int_to_qualityTypes(int source, Quality expected)
        {
            var quality = (Quality)source;
            quality.Should().Be(expected);
        }

        [Test]
        [TestCaseSource("ToIntCases")]
        public void should_be_able_to_convert_qualityTypes_to_int(Quality source, int expected)
        {
            var i = (int)source;
            i.Should().Be(expected);
        }

        public static List<QualityProfileQualityItem> GetDefaultQualities(params Quality[] allowed)
        {
            var qualities = new List<Quality>
            {
                Quality.Unknown,
                Quality.CAM,
                Quality.TELECINE,
                Quality.DVDSCR,
                Quality.REGIONAL,
                Quality.SDTV,
                Quality.DVD,
                Quality.DVDR,
                Quality.HDTV720p,
                Quality.HDTV1080p,
                Quality.HDTV2160p,
                Quality.WEBDL480p,
                Quality.WEBDL720p,
                Quality.WEBDL1080p,
                Quality.WEBDL2160p,
                Quality.Bluray480p,
                Quality.Bluray576p,
                Quality.Bluray720p,
                Quality.Bluray1080p,
                Quality.Bluray2160p,
                Quality.BRDISK,
                Quality.RAWHD
            };

            if (allowed.Length == 0)
            {
                allowed = qualities.ToArray();
            }

            var items = qualities
                .Except(allowed)
                .Concat(allowed)
                .Select(v => new QualityProfileQualityItem
                {
                    Quality = v,
                    Allowed = allowed.Contains(v)
                }).ToList();

            return items;
        }
    }
}
