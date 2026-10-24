using System.IO;
using System.IO.Compression;

namespace NzbDrone.Common.Extensions
{
    public static class GzipExtensions
    {
        public static byte[] Compress(this byte[] data)
        {
            using (var compressedStream = new MemoryStream())
            {
                using (var zipStream = new GZipStream(compressedStream, CompressionMode.Compress))
                {
                    zipStream.Write(data, 0, data.Length);
                }

                return compressedStream.ToArray();
            }
        }
    }
}
