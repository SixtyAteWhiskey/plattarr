using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Http.Dispatchers;

namespace NzbDrone.Common.Test.Http
{
    [TestFixture]
    public class BoundedHttpResponseReaderFixture
    {
        [Test]
        public async Task should_accept_exact_limit_and_reject_larger_advertised_response()
        {
            using var exact = new ByteArrayContent(new byte[4]);
            (await BoundedHttpResponseReader.ReadAsync(exact, 4, CancellationToken.None)).Should().HaveCount(4);

            using var oversized = new ByteArrayContent(new byte[5]);
            await FluentActions.Invoking(() => BoundedHttpResponseReader.ReadAsync(oversized, 4, CancellationToken.None))
                               .Should().ThrowAsync<InvalidDataException>();
        }

        [Test]
        public async Task should_reject_a_stream_without_an_advertised_length()
        {
            using var content = new StreamContent(new NonSeekableStream(new byte[5]));
            await FluentActions.Invoking(() => BoundedHttpResponseReader.ReadAsync(content, 4, CancellationToken.None))
                               .Should().ThrowAsync<InvalidDataException>();
        }

        private sealed class NonSeekableStream : MemoryStream
        {
            public NonSeekableStream(byte[] data)
                : base(data)
            {
            }

            public override bool CanSeek => false;
        }
    }
}
