using System.Collections.Generic;
using System.IO;
using System.Linq;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.MediaFiles.Commands;
using NzbDrone.Core.Music.MediaFiles.Events;
using NzbDrone.Core.Music.MediaFiles.TrackImport;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.RootFolders;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MusicTests.MediaFiles
{
    // The scan's folder checks, artist folder creation and ArtistScannedEvent, with a folder that has
    // no audio files. Reading the files is ScanFixture's.
    [TestFixture]
    public class DiskScanServiceFixture : CoreTest<DiskScanService>
    {
        private const string RootPath = "/data/music";
        private const string ArtistPath = RootPath + "/Artist";

        private Artist _artist;

        [SetUp]
        public void Setup()
        {
            _artist = Builder<Artist>.CreateNew().With(a => a.Id = 4).With(a => a.Path = ArtistPath).Build();

            Mocker.GetMock<IRootFolderService>().Setup(s => s.GetBestRootFolder(It.IsAny<string>())).Returns(new RootFolder { Path = RootPath });
            Mocker.GetMock<IRootFolderService>().Setup(s => s.All()).Returns(new List<RootFolder> { new RootFolder { Path = RootPath } });
            Mocker.GetMock<IArtistService>().Setup(s => s.GetArtists(It.IsAny<IEnumerable<int>>())).Returns(new List<Artist> { _artist });
            Mocker.GetMock<IDiskProvider>().Setup(d => d.GetFileInfos(It.IsAny<string>(), It.IsAny<bool>())).Returns(new List<FileInfo>());

            Mocker.GetMock<IMakeImportDecision>()
                .Setup(v => v.GetImportDecisions(It.IsAny<List<FileInfo>>(), It.IsAny<IdentificationOverrides>(), It.IsAny<ImportDecisionMakerInfo>(), It.IsAny<ImportDecisionMakerConfig>()))
                .Returns(new List<ImportDecision<LocalTrack>>());

            Mocker.GetMock<IMediaFileService>().Setup(v => v.GetFilesByArtist(It.IsAny<int>())).Returns(new List<TrackFile>());
            Mocker.GetMock<IMediaFileService>().Setup(v => v.GetFilesWithBasePath(It.IsAny<string>())).Returns(new List<TrackFile>());
            Mocker.GetMock<IMediaFileService>()
                .Setup(v => v.FilterUnchangedFiles(It.IsAny<List<FileInfo>>(), It.IsAny<FilterFilesType>()))
                .Returns((List<FileInfo> files, FilterFilesType filter) => files);
        }

        private void GivenFolders(bool rootExists, bool rootEmpty, bool artistExists)
        {
            Mocker.GetMock<IDiskProvider>().Setup(d => d.FolderExists(RootPath)).Returns(rootExists);
            Mocker.GetMock<IDiskProvider>().Setup(d => d.FolderEmpty(RootPath)).Returns(rootEmpty);
            Mocker.GetMock<IDiskProvider>().Setup(d => d.FolderExists(ArtistPath)).Returns(artistExists);
        }

        private void VerifyScanned(Times times)
        {
            Mocker.GetMock<IEventAggregator>().Verify(e => e.PublishEvent(It.IsAny<ArtistScannedEvent>()), times);
        }

        private void VerifySkipped(ArtistScanSkippedReason reason)
        {
            Mocker.GetMock<IEventAggregator>().Verify(e => e.PublishEvent(It.Is<ArtistScanSkippedEvent>(x => x.Reason == reason)), Times.Once());
            VerifyScanned(Times.Never());
        }

        [Test]
        public void should_publish_artist_scanned_for_each_artist_when_the_folder_exists()
        {
            GivenFolders(true, false, true);

            Subject.Scan(new List<string> { ArtistPath }, artistIds: new List<int> { 4 });

            VerifyScanned(Times.Once());
        }

        [Test]
        public void should_scan_every_root_folder_when_no_folders_are_given()
        {
            GivenFolders(true, false, true);

            Subject.Scan(artistIds: new List<int> { 4 });

            Mocker.GetMock<IRootFolderService>().Verify(s => s.All(), Times.Once());
            VerifyScanned(Times.Once());
        }

        [Test]
        public void should_not_scan_a_folder_outside_the_root_folders()
        {
            GivenFolders(true, false, true);
            Mocker.GetMock<IRootFolderService>().Setup(s => s.GetBestRootFolder(It.IsAny<string>())).Returns((RootFolder)null);

            Subject.Scan(new List<string> { ArtistPath }, artistIds: new List<int> { 4 });

            VerifyScanned(Times.Never());
            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_skip_the_artist_when_the_root_folder_is_missing()
        {
            GivenFolders(false, false, false);

            Subject.Scan(new List<string> { ArtistPath }, artistIds: new List<int> { 4 });

            VerifySkipped(ArtistScanSkippedReason.RootFolderDoesNotExist);
            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_skip_the_artist_when_the_root_folder_is_empty()
        {
            GivenFolders(true, true, false);

            Subject.Scan(new List<string> { ArtistPath }, artistIds: new List<int> { 4 });

            VerifySkipped(ArtistScanSkippedReason.RootFolderIsEmpty);
            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_still_publish_artist_scanned_when_the_artist_folder_is_missing_but_the_root_has_content()
        {
            GivenFolders(true, false, false);

            Subject.Scan(new List<string> { ArtistPath }, artistIds: new List<int> { 4 });

            VerifyScanned(Times.Once());
        }

        [Test]
        public void should_create_a_missing_artist_folder_when_configured()
        {
            GivenFolders(true, false, false);
            Mocker.GetMock<IConfigService>().SetupGet(c => c.CreateEmptyArtistFolders).Returns(true);

            Subject.Scan(new List<string> { RootPath }, artistIds: new List<int> { 4 });

            Mocker.GetMock<IDiskProvider>().Verify(d => d.CreateFolder(ArtistPath), Times.Once());
        }

        [Test]
        public void should_not_create_a_missing_artist_folder_by_default()
        {
            GivenFolders(true, false, false);

            Subject.Scan(new List<string> { RootPath }, artistIds: new List<int> { 4 });

            Mocker.GetMock<IDiskProvider>().Verify(d => d.CreateFolder(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_not_create_a_missing_artist_folder_when_empty_folders_are_deleted()
        {
            GivenFolders(true, false, false);
            Mocker.GetMock<IConfigService>().SetupGet(c => c.CreateEmptyArtistFolders).Returns(true);
            Mocker.GetMock<IConfigService>().SetupGet(c => c.DeleteEmptyFolders).Returns(true);

            Subject.Scan(new List<string> { RootPath }, artistIds: new List<int> { 4 });

            Mocker.GetMock<IDiskProvider>().Verify(d => d.CreateFolder(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_scan_the_folders_of_a_rescan_command()
        {
            GivenFolders(true, false, true);

            Subject.Execute(new RescanFoldersCommand(new List<string> { ArtistPath }, FilterFilesType.Known, false, new List<int> { 4 }));

            Mocker.GetMock<IArtistService>().Verify(s => s.GetArtists(It.Is<IEnumerable<int>>(ids => ids.Contains(4))), Times.AtLeastOnce());
            VerifyScanned(Times.Once());
        }
    }
}
