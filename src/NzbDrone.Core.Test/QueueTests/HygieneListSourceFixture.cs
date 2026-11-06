using System;
using System.IO;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Hygiene;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.QueueTests
{
    [TestFixture]
    public class HygieneListSourceFixture : TestBase<HygieneListSource>
    {
        [Test]
        public void inline_list_needs_no_file_or_http_request()
        {
            Subject.Load("*.exe", "").Should().Be("*.exe");
            Mocker.GetMock<IHttpClient>().Verify(x => x.Get(It.IsAny<HttpRequest>()), Times.Never);
        }

        [Test]
        public void absolute_file_overrides_inline_list()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "*.mkv");
                Subject.Load("*.exe", path).Should().Be("*.mkv");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void empty_source_fails_instead_of_clearing_a_live_list()
        {
            var path = Path.GetTempFileName();
            try
            {
                Action load = () => Subject.Load("*.exe", path);
                load.Should().Throw<InvalidDataException>();
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void oversized_file_is_rejected()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, new string('x', HygieneListSource.MaximumBytes + 1));
                Action load = () => Subject.Load("*.exe", path);
                load.Should().Throw<InvalidDataException>();
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void remote_request_is_public_and_bounded()
        {
            HttpRequest request = null;
            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.Get(It.IsAny<HttpRequest>()))
                .Returns((HttpRequest value) =>
                {
                    request = value;
                    return new HttpResponse(value, new HttpHeader(), "*.mkv");
                });

            Subject.Load("*.exe", "https://example.com/list.txt").Should().Be("*.mkv");
            request.PublicDestinationOnly.Should().BeTrue();
            request.MaximumResponseBytes.Should().Be(HygieneListSource.MaximumBytes);
        }

        [TestCase("relative.txt")]
        [TestCase("ftp://example.com/list.txt")]
        [TestCase("http://")]
        public void invalid_source_is_rejected(string source)
        {
            Action validate = () => HygieneListSource.Validate(source);
            validate.Should().Throw<ArgumentException>();
        }
    }
}
