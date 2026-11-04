using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NzbDrone.Common.Http.Dispatchers
{
    public static class BoundedHttpResponseReader
    {
        public static async Task<byte[]> ReadAsync(HttpContent content, long maximumBytes, CancellationToken cancellationToken)
        {
            if (maximumBytes < 0 || content.Headers.ContentLength > maximumBytes)
            {
                throw new InvalidDataException("HTTP response exceeds the configured size limit");
            }

            await using var source = await content.ReadAsStreamAsync(cancellationToken);
            await using var destination = new MemoryStream();
            var buffer = new byte[81920];

            while (true)
            {
                var count = await source.ReadAsync(buffer, cancellationToken);
                if (count == 0)
                {
                    return destination.ToArray();
                }

                if (destination.Length > maximumBytes - count)
                {
                    throw new InvalidDataException("HTTP response exceeds the configured size limit");
                }

                await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            }
        }
    }
}
