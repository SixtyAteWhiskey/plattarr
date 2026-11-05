using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Queue;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Hygiene;
using Radarr.Http.REST;

namespace NzbDrone.Api.Test.v3.Hygiene
{
    [TestFixture]
    public class HygieneContentControllerFixture : TestBase<HygieneContentController>
    {
        [Test]
        public void preview_is_empty_when_inspection_is_disabled()
        {
            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.HygieneContentInspectionEnabled).Returns(false);

            Subject.GetPreview().Value.Should().BeEmpty();
            Mocker.GetMock<IQueueService>().Verify(x => x.GetQueue(), Times.Never);
        }

        [Test]
        public void preview_reports_unsupported_clients_without_removing_jobs()
        {
            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.HygieneContentInspectionEnabled).Returns(true);
            Mocker.GetMock<IQueueService>()
                .Setup(x => x.GetQueue()).Returns(new List<NzbDrone.Core.Queue.Queue>
                {
                    new() { Id = 9, Title = "Test", DownloadId = "HASH", DownloadClientId = 4 }
                });

            Subject.GetPreview().Value.Should().ContainSingle().Which.Status.Should().Be("unsupported-client");
            Mocker.GetMock<IQueueService>().Verify(x => x.Remove(It.IsAny<int>()), Times.Never);
        }

        [Test]
        public void invalid_enabled_configuration_is_rejected()
        {
            Action update = () => Subject.UpdateConfig(1, new HygieneContentConfigResource
            {
                InspectionEnabled = true,
                ListType = "blocklist",
                Patterns = ""
            });

            update.Should().Throw<BadRequestException>();
        }

        [Test]
        public void sync_requires_a_saved_exclusion_list()
        {
            Action sync = () => Subject.SyncExcludedFileNames();

            sync.Should().Throw<BadRequestException>();
            Mocker.GetMock<IProvideDownloadClient>()
                .Verify(x => x.GetDownloadClients(It.IsAny<bool>()), Times.Never);
        }
    }
}
