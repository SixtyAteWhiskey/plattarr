using System;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Hygiene;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.QueueTests
{
    [TestFixture]
    public class HygieneBlacklistSyncServiceFixture : TestBase<HygieneBlacklistSyncService>
    {
        [Test]
        public void scheduled_command_does_nothing_until_enabled()
        {
            Subject.Execute(new HygieneBlacklistSyncCommand());

            Mocker.GetMock<IHygieneListSource>()
                .Verify(x => x.Load(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            Mocker.GetMock<IProvideDownloadClient>()
                .Verify(x => x.GetDownloadClients(It.IsAny<bool>()), Times.Never);
        }

        [Test]
        public void failed_source_is_not_pushed_to_any_client()
        {
            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.HygieneExcludedFileNamesSource).Returns("/missing/list.txt");
            Mocker.GetMock<IHygieneListSource>()
                .Setup(x => x.Load(It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new InvalidOperationException());

            Action sync = () => Subject.Sync();

            sync.Should().Throw<InvalidOperationException>();
            Mocker.GetMock<IProvideDownloadClient>()
                .Verify(x => x.GetDownloadClients(It.IsAny<bool>()), Times.Never);
        }

        [Test]
        public void scheduled_source_failure_is_held_without_throwing()
        {
            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.HygieneExcludedFileNamesSyncEnabled).Returns(true);
            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.HygieneExcludedFileNamesSource).Returns("/missing/list.txt");
            Mocker.GetMock<IHygieneListSource>()
                .Setup(x => x.Load(It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new InvalidOperationException());

            Action run = () => Subject.Execute(new HygieneBlacklistSyncCommand());

            run.Should().NotThrow();
            ExceptionVerification.ExpectedWarns(1);
            Mocker.GetMock<IProvideDownloadClient>()
                .Verify(x => x.GetDownloadClients(It.IsAny<bool>()), Times.Never);
        }
    }
}
