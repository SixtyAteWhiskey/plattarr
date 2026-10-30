using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.HealthCheck.Checks;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.HealthCheck.Checks
{
    [TestFixture]
    public class DownloadClientCheckFixture : CoreTest<DownloadClientCheck>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<ILocalizationService>()
                  .Setup(s => s.GetLocalizedString(It.IsAny<string>()))
                  .Returns("Some Warning Message");
        }

        [Test]
        public void should_return_warning_when_download_client_has_not_been_configured()
        {
            Mocker.GetMock<IProvideDownloadClient>()
                  .Setup(s => s.GetDownloadClients(It.IsAny<bool>()))
                  .Returns(Array.Empty<IDownloadClient>());

            Subject.Check().ShouldBeWarning();
        }

        [Test]
        public void should_return_error_when_download_client_throws()
        {
            const string guidance = "Plattarr could not read items from a configured download client. Check the client and local logs.";
            var downloadClient = Mocker.GetMock<IDownloadClient>();
            downloadClient.Setup(s => s.Definition).Returns(new DownloadClientDefinition { Name = "Private client name" });

            downloadClient.Setup(s => s.GetItems())
                          .Throws(new Exception("Connection failed at https://private.example/?apikey=secret"));

            Mocker.GetMock<IProvideDownloadClient>()
                  .Setup(s => s.GetDownloadClients(It.IsAny<bool>()))
                  .Returns(new IDownloadClient[] { downloadClient.Object });

            Mocker.GetMock<ILocalizationService>()
                  .Setup(s => s.GetLocalizedString("DownloadClientCheckUnableToReadItemsMessage"))
                  .Returns(guidance);

            var result = Subject.Check();

            result.ShouldBeError();
            result.Message.Should().Be(guidance)
                  .And.NotContain("Private client name")
                  .And.NotContain("private.example")
                  .And.NotContain("apikey=secret");
            Mocker.GetMock<ILocalizationService>()
                  .Verify(s => s.GetLocalizedString("DownloadClientCheckUnableToReadItemsMessage"), Times.Once());
            Mocker.GetMock<ILocalizationService>()
                  .Verify(s => s.GetLocalizedString("DownloadClientCheckUnableToCommunicateMessage", It.IsAny<Dictionary<string, object>>()), Times.Never());
        }

        [Test]
        public void should_return_ok_when_download_client_returns()
        {
            var downloadClient = Mocker.GetMock<IDownloadClient>();

            downloadClient.Setup(s => s.GetItems())
                          .Returns(new List<DownloadClientItem>());

            Mocker.GetMock<IProvideDownloadClient>()
                  .Setup(s => s.GetDownloadClients(It.IsAny<bool>()))
                  .Returns(new IDownloadClient[] { downloadClient.Object });

            Subject.Check().ShouldBeOk();
        }
    }
}
