using System;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.HealthCheck;
using NzbDrone.Core.HealthCheck.Checks;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.HealthCheck.Checks
{
    [TestFixture]
    public class DownloadClientConnectionCheckFixture : CoreTest<DownloadClientConnectionCheck>
    {
        private Mock<IDownloadClient> _client;

        [SetUp]
        public void Setup()
        {
            _client = new Mock<IDownloadClient>();
            _client.As<IDownloadClientConnectionStatus>();
            Mocker.GetMock<IProvideDownloadClient>()
                  .Setup(s => s.GetDownloadClients(It.IsAny<bool>()))
                  .Returns(new[] { _client.Object });
            Mocker.GetMock<ILocalizationService>()
                  .Setup(s => s.GetLocalizedString(It.IsAny<string>()))
                  .Returns((string key) => key);
        }

        [TestCase("connected")]
        [TestCase("firewalled")]
        [TestCase(null)]
        public void healthy_or_unknown_connection_does_not_warn(string status)
        {
            _client.As<IDownloadClientConnectionStatus>()
                   .Setup(s => s.GetConnectionStatus())
                   .Returns(status);

            Subject.Check().Type.Should().Be(HealthCheckResult.Ok);
        }

        [Test]
        public void disconnected_client_warns_about_its_network()
        {
            _client.As<IDownloadClientConnectionStatus>()
                   .Setup(s => s.GetConnectionStatus())
                   .Returns("disconnected");

            var result = Subject.Check();

            result.Type.Should().Be(HealthCheckResult.Warning);
            result.Reason.Should().Be(HealthCheckReason.DownloadClientConnectionDisconnected);
            result.Message.Should().Be("DownloadClientConnectionDisconnectedMessage");
        }

        [Test]
        public void status_error_warns_without_exposing_exception_details()
        {
            _client.As<IDownloadClientConnectionStatus>()
                   .Setup(s => s.GetConnectionStatus())
                   .Throws(new Exception("https://private.example/?key=secret"));

            var result = Subject.Check();

            result.Type.Should().Be(HealthCheckResult.Warning);
            result.Reason.Should().Be(HealthCheckReason.DownloadClientConnectionStatusUnavailable);
            result.Message.Should().Be("DownloadClientConnectionStatusUnavailableMessage");
            result.Message.Should().NotContain("private.example");
        }

        [Test]
        public void unsupported_client_does_not_warn()
        {
            Mocker.GetMock<IProvideDownloadClient>()
                  .Setup(s => s.GetDownloadClients(It.IsAny<bool>()))
                  .Returns(new[] { new Mock<IDownloadClient>().Object });

            Subject.Check().Type.Should().Be(HealthCheckResult.Ok);
        }
    }
}
