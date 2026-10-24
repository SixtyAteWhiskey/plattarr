using System;
using System.Collections.Generic;
using System.IO;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.MediaFiles.Commands;
using NzbDrone.Core.Music.MediaFiles.TrackImport;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MusicTests.MediaFiles
{
    [TestFixture]
    public class DownloadedAlbumsCommandServiceFixture : FileSystemTest<DownloadedAlbumsCommandService>
    {
        private string _downloadFolder;
        private string _downloadFile;

        private TrackedDownload _trackedDownload;

        [SetUp]
        public void Setup()
        {
            _downloadFolder = Tmp("c:\\drop_other\\Show.S01E01\\");
            _downloadFile = Tmp("c:\\drop_other\\Show.S01E01.mkv");

            Mocker.GetMock<IDownloadedTracksImportService>()
                .Setup(v => v.ProcessRootFolder(It.IsAny<DirectoryInfo>()))
                .Returns(new List<ImportResult>());

            Mocker.GetMock<IDownloadedTracksImportService>()
                .Setup(v => v.ProcessPath(It.IsAny<string>(), It.IsAny<ImportMode>(), It.IsAny<Artist>(), It.IsAny<DownloadClientItem>()))
                .Returns(new List<ImportResult>());

            var downloadItem = Builder<DownloadClientItem>.CreateNew()
                .With(v => v.DownloadId = "sab1")
                .With(v => v.Status = DownloadItemStatus.Downloading)
                .Build();

            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                .With(v => v.Artist = new Artist())
                .Build();

            _trackedDownload = new TrackedDownload
            {
                DownloadItem = downloadItem,
                RemoteRelease = remoteAlbum,
                State = TrackedDownloadState.Downloading
            };
        }

        private void GivenExistingFolder(string path)
        {
            FileSystem.AddDirectory(path);
        }

        private void GivenExistingFile(string path)
        {
            FileSystem.AddFile(path, new MockFileData(string.Empty));
        }

        private void GivenValidQueueItem()
        {
            Mocker.GetMock<ITrackedDownloadService>()
                  .Setup(s => s.Find("sab1"))
                  .Returns(_trackedDownload);
        }

        [Test]
        public void should_skip_import_if_dronefactory_doesnt_exist()
        {
            Assert.Throws<ArgumentException>(() => Subject.Execute(new DownloadedAlbumsScanCommand()));

            Mocker.GetMock<IDownloadedTracksImportService>().Verify(c => c.ProcessRootFolder(It.IsAny<DirectoryInfo>()), Times.Never());
        }

        [Test]
        public void should_process_folder_if_downloadclientid_is_not_specified()
        {
            GivenExistingFolder(_downloadFolder);

            Subject.Execute(new DownloadedAlbumsScanCommand() { Path = _downloadFolder });

            Mocker.GetMock<IDownloadedTracksImportService>().Verify(c => c.ProcessPath(It.IsAny<string>(), ImportMode.Auto, null, null), Times.Once());
        }

        [Test]
        public void should_process_file_if_downloadclientid_is_not_specified()
        {
            GivenExistingFile(_downloadFile);

            Subject.Execute(new DownloadedAlbumsScanCommand() { Path = _downloadFile });

            Mocker.GetMock<IDownloadedTracksImportService>().Verify(c => c.ProcessPath(It.IsAny<string>(), ImportMode.Auto, null, null), Times.Once());
        }

        [Test]
        public void should_process_folder_with_downloadclientitem_if_available()
        {
            GivenExistingFolder(_downloadFolder);
            GivenValidQueueItem();

            Subject.Execute(new DownloadedAlbumsScanCommand() { Path = _downloadFolder, DownloadClientId = "sab1" });

            Mocker.GetMock<IDownloadedTracksImportService>().Verify(c => c.ProcessPath(_downloadFolder, ImportMode.Auto, _trackedDownload.GetRemoteAlbum().Artist, _trackedDownload.DownloadItem), Times.Once());
        }

        [Test]
        public void should_process_folder_without_downloadclientitem_if_not_available()
        {
            GivenExistingFolder(_downloadFolder);

            Subject.Execute(new DownloadedAlbumsScanCommand() { Path = _downloadFolder, DownloadClientId = "sab1" });

            Mocker.GetMock<IDownloadedTracksImportService>().Verify(c => c.ProcessPath(_downloadFolder, ImportMode.Auto, null, null), Times.Once());

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_warn_if_neither_folder_or_file_exists()
        {
            Subject.Execute(new DownloadedAlbumsScanCommand() { Path = _downloadFolder });

            Mocker.GetMock<IDownloadedTracksImportService>().Verify(c => c.ProcessPath(It.IsAny<string>(), ImportMode.Auto, null, null), Times.Never());

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_override_import_mode()
        {
            GivenExistingFile(_downloadFile);

            Subject.Execute(new DownloadedAlbumsScanCommand() { Path = _downloadFile, ImportMode = ImportMode.Copy });

            Mocker.GetMock<IDownloadedTracksImportService>().Verify(c => c.ProcessPath(It.IsAny<string>(), ImportMode.Copy, null, null), Times.Once());
        }
    }
}
