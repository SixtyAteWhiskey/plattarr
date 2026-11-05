using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Hygiene;
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
            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.HygieneContentListType).Returns("blocklist");
            Mocker.GetMock<IHygieneListSource>()
                .Setup(x => x.Load(It.IsAny<string>(), It.IsAny<string>())).Returns("*.exe");
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

        [Test]
        public void enabled_inspection_accepts_a_source_without_inline_patterns()
        {
            Subject.UpdateConfig(1, new HygieneContentConfigResource
            {
                InspectionEnabled = true,
                ListType = "blocklist",
                PatternSource = "/config/content-patterns.txt"
            });

            Mocker.GetMock<IConfigService>()
                .VerifySet(x => x.HygieneContentPatternSource = "/config/content-patterns.txt", Times.Once);
        }

        [Test]
        public void automatic_sync_requires_inline_or_source_list()
        {
            Action update = () => Subject.UpdateConfig(1, new HygieneContentConfigResource
            {
                ListType = "blocklist",
                ExcludedFileNamesSyncEnabled = true
            });

            update.Should().Throw<BadRequestException>();
        }

        [Test]
        public void invalid_source_is_rejected_before_saving()
        {
            Action update = () => Subject.UpdateConfig(1, new HygieneContentConfigResource
            {
                ListType = "blocklist",
                PatternSource = "ftp://example.com/list.txt"
            });

            update.Should().Throw<BadRequestException>();
            Mocker.GetMock<IConfigService>()
                .VerifySet(x => x.HygieneContentPatternSource = It.IsAny<string>(), Times.Never);
        }

        [Test]
        public void unreadable_pattern_source_holds_preview_without_client_reads()
        {
            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.HygieneContentInspectionEnabled).Returns(true);
            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.HygieneContentPatternSource).Returns("/missing/list.txt");
            Mocker.GetMock<IHygieneListSource>()
                .Setup(x => x.Load(It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new System.IO.IOException());
            Mocker.GetMock<IQueueService>()
                .Setup(x => x.GetQueue()).Returns(new List<NzbDrone.Core.Queue.Queue>
                {
                    new() { Id = 9, Title = "Test", DownloadId = "HASH", DownloadClientId = 4 }
                });

            Subject.GetPreview().Value.Should().ContainSingle().Which.Status.Should().Be("patterns-unavailable");
            Mocker.GetMock<IProvideDownloadClient>()
                .Verify(x => x.Get(It.IsAny<int>()), Times.Never);
        }
    }
}
