using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.DecisionEngineTests.ReleaseSpecifications;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Download.CompletedDownloadServiceTests
{
    // Contract 5: the core picks the importer by TrackedDownload.Kind and works from RemoteRelease and
    // IImportResult alone. A TV importer stands in for a second module.
    [TestFixture]
    public class ImporterRoutingFixture : CoreTest<CompletedDownloadService>
    {
        private TrackedDownload _trackedDownload;
        private Mock<IDownloadImporter> _movieImporter;
        private Mock<IDownloadImporter> _tvImporter;

        [SetUp]
        public void Setup()
        {
            var completed = Builder<DownloadClientItem>.CreateNew()
                                                       .With(h => h.Status = DownloadItemStatus.Completed)
                                                       .With(h => h.OutputPath = new OsPath(@"C:\DropFolder\Show.S01E01".AsOsAgnostic()))
                                                       .With(h => h.Title = "Show.S01E01")
                                                       .Build();

            _trackedDownload = Builder<TrackedDownload>.CreateNew()
                                                       .With(c => c.State = TrackedDownloadState.Downloading)
                                                       .With(c => c.DownloadItem = completed)
                                                       .With(c => c.Kind = MediaKind.Tv)
                                                       .With(c => c.RemoteRelease = new TvRemoteRelease
                                                       {
                                                           Series = new TvParent { Id = 12 },
                                                           Release = new ReleaseInfo { IndexerId = 3 }
                                                       })
                                                       .Build();

            _movieImporter = new Mock<IDownloadImporter>();
            _movieImporter.SetupGet(i => i.Kind).Returns(MediaKind.Movies);

            _tvImporter = new Mock<IDownloadImporter>();
            _tvImporter.SetupGet(i => i.Kind).Returns(MediaKind.Tv);
            _tvImporter.Setup(i => i.Check(It.IsAny<TrackedDownload>())).Returns(DownloadImportCheck.Ready);
            _tvImporter.Setup(i => i.Import(It.IsAny<TrackedDownload>(), It.IsAny<string>()))
                       .Returns(new List<IImportResult>());

            Mocker.GetMock<IProvideImportItemService>()
                  .Setup(s => s.ProvideImportItem(It.IsAny<DownloadClientItem>(), It.IsAny<DownloadClientItem>()))
                  .Returns<DownloadClientItem, DownloadClientItem>((i, p) => i);

            Mocker.SetConstant<IEnumerable<IDownloadImporter>>(new[] { _movieImporter.Object, _tvImporter.Object });
        }

        private void GivenImportResults(params IImportResult[] results)
        {
            _tvImporter.Setup(i => i.Import(It.IsAny<TrackedDownload>(), It.IsAny<string>()))
                       .Returns(results.ToList());
        }

        private static IImportResult Result(ImportResultType type, string path, FailDownloads? failsDownloadAs = null)
        {
            var result = new Mock<IImportResult>();
            result.SetupGet(r => r.Result).Returns(type);
            result.SetupGet(r => r.Path).Returns(path);
            result.SetupGet(r => r.Errors).Returns(type == ImportResultType.Imported ? new List<string>() : new List<string> { "Rejected" });
            result.SetupGet(r => r.FailsDownloadAs).Returns(failsDownloadAs);

            return result.Object;
        }

        [Test]
        public void check_should_ask_the_importer_for_the_downloads_media_type()
        {
            Subject.Check(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.ImportPending);
            _tvImporter.Verify(i => i.Check(_trackedDownload), Times.Once());
            _movieImporter.Verify(i => i.Check(It.IsAny<TrackedDownload>()), Times.Never());
        }

        [Test]
        public void check_should_leave_the_download_alone_when_the_importer_skips_it()
        {
            _tvImporter.Setup(i => i.Check(It.IsAny<TrackedDownload>())).Returns(DownloadImportCheck.Skip);

            Subject.Check(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.Downloading);
            _tvImporter.Verify(i => i.ImportBlocked(It.IsAny<TrackedDownload>()), Times.Never());
        }

        [Test]
        public void check_should_notify_the_importer_once_when_blocked()
        {
            _tvImporter.Setup(i => i.Check(It.IsAny<TrackedDownload>())).Returns(DownloadImportCheck.Blocked);

            Subject.Check(_trackedDownload);
            Subject.Check(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.ImportBlocked);
            _tvImporter.Verify(i => i.ImportBlocked(_trackedDownload), Times.Once());
            _movieImporter.Verify(i => i.ImportBlocked(It.IsAny<TrackedDownload>()), Times.Never());
        }

        [Test]
        public void should_block_a_download_no_importer_claims()
        {
            _trackedDownload.Kind = MediaKind.Music;

            Subject.Check(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.ImportBlocked);
            _trackedDownload.Status.Should().Be(TrackedDownloadStatus.Warning);
            _movieImporter.Verify(i => i.Check(It.IsAny<TrackedDownload>()), Times.Never());
            _tvImporter.Verify(i => i.Check(It.IsAny<TrackedDownload>()), Times.Never());
        }

        [Test]
        public void import_should_mark_imported_and_report_the_parent()
        {
            GivenImportResults(Result(ImportResultType.Imported, @"C:\DropFolder\Show.S01E01\show.mkv".AsOsAgnostic()));
            _tvImporter.Setup(i => i.IsImported(It.IsAny<TrackedDownload>(), It.IsAny<IReadOnlyList<IImportResult>>())).Returns(true);

            Subject.Import(_trackedDownload);

            _tvImporter.Verify(i => i.Import(_trackedDownload, _trackedDownload.DownloadItem.OutputPath.FullPath), Times.Once());
            _movieImporter.Verify(i => i.Import(It.IsAny<TrackedDownload>(), It.IsAny<string>()), Times.Never());
            _trackedDownload.State.Should().Be(TrackedDownloadState.Imported);

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.Is<DownloadCompletedEvent>(e => e.MovieId == 12)), Times.Once());
        }

        [Test]
        public void import_should_block_with_media_type_wording_when_items_were_rejected()
        {
            GivenImportResults(Result(ImportResultType.Imported, @"C:\DropFolder\Show.S01E01\a.mkv".AsOsAgnostic()),
                               Result(ImportResultType.Rejected, @"C:\DropFolder\Show.S01E01\b.mkv".AsOsAgnostic()));

            Subject.Import(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.ImportBlocked);
            _trackedDownload.StatusMessages.First().Title.Should().Be("One or more episodes expected in this release were not imported or missing");
            _trackedDownload.StatusMessages.Select(m => m.Title).Should().Contain("b.mkv");
            _tvImporter.Verify(i => i.ImportBlocked(_trackedDownload), Times.Once());
        }

        [Test]
        public void import_should_not_ask_the_importer_without_a_parent()
        {
            _trackedDownload.RemoteRelease = new TvRemoteRelease { Series = null };

            Subject.Import(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.ImportBlocked);
            _tvImporter.Verify(i => i.Import(It.IsAny<TrackedDownload>(), It.IsAny<string>()), Times.Never());
        }

        [TestCase(FailDownloads.Executables)]
        [TestCase(FailDownloads.PotentiallyDangerous)]
        public void import_should_fail_the_download_when_the_indexer_fails_that_rejection(FailDownloads failDownloads)
        {
            Mocker.SetConstant<IRejectedImportService>(Mocker.Resolve<RejectedImportService>());
            Mocker.GetMock<ICachedIndexerSettingsProvider>()
                  .Setup(s => s.GetSettings(3))
                  .Returns(new CachedIndexerSettings { FailDownloads = new HashSet<FailDownloads> { failDownloads } });

            GivenImportResults(Result(ImportResultType.Rejected, @"C:\DropFolder\Show.S01E01\show.exe".AsOsAgnostic(), failDownloads));

            Subject.Import(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.FailedPending);
        }

        [Test]
        public void import_should_warn_when_the_indexer_does_not_fail_that_rejection()
        {
            Mocker.SetConstant<IRejectedImportService>(Mocker.Resolve<RejectedImportService>());
            Mocker.GetMock<ICachedIndexerSettingsProvider>()
                  .Setup(s => s.GetSettings(3))
                  .Returns(new CachedIndexerSettings { FailDownloads = new HashSet<FailDownloads> { FailDownloads.PotentiallyDangerous } });

            GivenImportResults(Result(ImportResultType.Rejected, @"C:\DropFolder\Show.S01E01\show.exe".AsOsAgnostic(), FailDownloads.Executables));

            Subject.Import(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.ImportPending);
            _trackedDownload.Status.Should().Be(TrackedDownloadStatus.Warning);
        }
    }
}
