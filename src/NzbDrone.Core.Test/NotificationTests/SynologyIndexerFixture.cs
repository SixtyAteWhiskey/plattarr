using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.Synology;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.NotificationTests
{
    [TestFixture]
    public class SynologyIndexerFixture : CoreTest<SynologyIndexer>
    {
        private ActivitySubject _subject;
        private DownloadMessage _upgrade;

        [SetUp]
        public void SetUp()
        {
            _subject = new ActivitySubject
            {
                Kind = MediaKind.Movies,
                ParentPath = @"C:\Test\".AsOsAgnostic()
            };

            _upgrade = new DownloadMessage
            {
                Subject = _subject,

                File = new NotificationFile
                {
                    RelativePath = "moviefile1.mkv"
                },

                OldFiles = new List<NotificationFile>
                {
                    new NotificationFile { RelativePath = "oldmoviefile1.mkv" },
                    new NotificationFile { RelativePath = "oldmoviefile2.mkv" }
                }
            };

            Subject.Definition = new NotificationDefinition
            {
                Settings = new SynologyIndexerSettings
                {
                    UpdateLibrary = true
                }
            };
        }

        [Test]
        public void should_not_update_library_if_disabled()
        {
            (Subject.Definition.Settings as SynologyIndexerSettings).UpdateLibrary = false;

            Subject.OnRename(new RenameMessage { Subject = _subject });

            Mocker.GetMock<ISynologyIndexerProxy>()
                  .Verify(v => v.UpdateFolder(_subject.ParentPath), Times.Never());
        }

        [Test]
        public void should_remove_old_movie_on_upgrade()
        {
            Subject.OnDownload(_upgrade);

            Mocker.GetMock<ISynologyIndexerProxy>()
                  .Verify(v => v.DeleteFile(@"C:\Test\oldmoviefile1.mkv".AsOsAgnostic()), Times.Once());

            Mocker.GetMock<ISynologyIndexerProxy>()
                  .Verify(v => v.DeleteFile(@"C:\Test\oldmoviefile2.mkv".AsOsAgnostic()), Times.Once());
        }

        [Test]
        public void should_add_new_movie_on_upgrade()
        {
            Subject.OnDownload(_upgrade);

            Mocker.GetMock<ISynologyIndexerProxy>()
                  .Verify(v => v.AddFile(@"C:\Test\moviefile1.mkv".AsOsAgnostic()), Times.Once());
        }

        [Test]
        public void should_update_entire_movie_folder_on_rename()
        {
            Subject.OnRename(new RenameMessage { Subject = _subject });

            Mocker.GetMock<ISynologyIndexerProxy>()
                  .Verify(v => v.UpdateFolder(@"C:\Test\".AsOsAgnostic()), Times.Once());
        }
    }
}
