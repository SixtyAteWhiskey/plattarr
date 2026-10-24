using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Common.Test.ExtensionTests
{
    [TestFixture]
    public class NumberExtensionFixture
    {
        [TestCase(0, "0 B")]
        [TestCase(1000, "1,000.0 B")]
        [TestCase(1024, "1.0 KB")]
        [TestCase(1000000, "976.6 KB")]
        [TestCase(377487360, "360.0 MB")]
        [TestCase(1255864686, "1.2 GB")]
        [TestCase(-1024, "-1.0 KB")]
        [TestCase(-1000000, "-976.6 KB")]
        [TestCase(-377487360, "-360.0 MB")]
        [TestCase(-1255864686, "-1.2 GB")]
        [TestCase(long.MinValue, "-8.0 EB")]
        [TestCase(long.MaxValue, "8.0 EB")]
        public void should_calculate_string_correctly(long bytes, string expected)
        {
            bytes.SizeSuffix().Should().Be(expected);
        }

        [TestCase(0, 0)]
        [TestCase(1, 128)]
        [TestCase(320, 40960)]
        public void should_convert_whole_kilobits_to_bytes(int kilobits, long expected)
        {
            kilobits.Kilobits().Should().Be(expected);
        }

        [TestCase(0.5, 64)]
        [TestCase(1.5, 192)]
        [TestCase(256.0, 32768)]
        public void should_convert_fractional_kilobits_to_bytes(double kilobits, long expected)
        {
            kilobits.Kilobits().Should().Be(expected);
        }
    }
}
