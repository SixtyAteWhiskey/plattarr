using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Common.Test.ExtensionTests
{
    [TestFixture]
    public class GzipExtensionsFixture
    {
        private static byte[] Decompress(byte[] data)
        {
            using (var input = new MemoryStream(data))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gzip.CopyTo(output);
                return output.ToArray();
            }
        }

        [Test]
        public void should_round_trip_through_gzip()
        {
            var data = Encoding.UTF8.GetBytes(new string('a', 5000) + "tail");

            var compressed = data.Compress();

            compressed.Length.Should().BeLessThan(data.Length);
            Decompress(compressed).Should().Equal(data);
        }

        [Test]
        public void should_round_trip_an_empty_array()
        {
            Decompress(Array.Empty<byte>().Compress()).Should().BeEmpty();
        }
    }
}
