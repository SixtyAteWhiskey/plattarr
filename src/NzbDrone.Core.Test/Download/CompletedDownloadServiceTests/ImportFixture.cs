using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.History;
using NzbDrone.Core.Movies.Import;
using NzbDrone.Core.Movies.MediaFiles;
using NzbDrone.Core.Movies.MediaFiles.MovieImport;
using NzbDrone.Core.Movies.Parser;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Download
{
    [TestFixture]
    public class ImportFixture : CoreTest<CompletedDownloadService>
    {
        private TrackedDownload _trackedDownload;

        [SetUp]
        public void Setup()
        {
            var completed = Builder<DownloadClientItem>.CreateNew()
                                                    .With(h => h.Status = DownloadItemStatus.Completed)
                                                    .With(h => h.OutputPath = new OsPath(@"C:\DropFolder\MyDownload".AsOsAgnostic()))
                                                    .With(h => h.Title = "Drone.1998")
                                                    .Build();

            var remoteMovie = BuildRemoteMovie();

            _trackedDownload = Builder<TrackedDownload>.CreateNew()
                    .With(c => c.State = TrackedDownloadState.Downloading)
                    .With(c => c.DownloadItem = completed)
                    .With(c => c.RemoteRelease = remoteMovie)
                    .With(c => c.Kind = MediaKind.Movies)
                    .Build();

            Mocker.GetMock<IDownloadClient>()
              .SetupGet(c => c.Definition)
              .Returns(new DownloadClientDefinition { Id = 1, Name = "testClient" });

            Mocker.GetMock<IProvideDownloadClient>()
                  .Setup(c => c.Get(It.IsAny<int>()))
                  .Returns(Mocker.GetMock<IDownloadClient>().Object);

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.MostRecentForDownloadId(_trackedDownload.DownloadItem.DownloadId))
                  .Returns(new MovieHistory());

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetMovie("Drone.1998"))
                  .Returns(remoteMovie.Movie);

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByDownloadId(It.IsAny<string>()))
                  .Returns(new List<MovieHistory>());

            Mocker.GetMock<IProvideImportItemService>()
                  .Setup(s => s.ProvideImportItem(It.IsAny<DownloadClientItem>(), It.IsAny<DownloadClientItem>()))
                  .Returns<DownloadClientItem, DownloadClientItem>((i, p) => i);

            Mocker.SetConstant<IEnumerable<IDownloadImporter>>(new[] { Mocker.Resolve<MovieDownloadImporter>() });
        }

        private RemoteMovie BuildRemoteMovie()
        {
            return new RemoteMovie
            {
                Movie = new Movie()
            };
        }

        private void GivenABadlyNamedDownload()
        {
            _trackedDownload.DownloadItem.DownloadId = "1234";
            _trackedDownload.DownloadItem.Title = "Droned Pilot"; // Set a badly named download
            Mocker.GetMock<IHistoryService>()
               .Setup(s => s.MostRecentForDownloadId(It.Is<string>(i => i == "1234")))
               .Returns(new MovieHistory() { SourceTitle = "Droned 1998" });

            Mocker.GetMock<IParsingService>()
               .Setup(s => s.GetMovie(It.IsAny<string>()))
               .Returns((Movie)null);

            Mocker.GetMock<IParsingService>()
                .Setup(s => s.GetMovie("Droned 1998"))
                .Returns(BuildRemoteMovie().Movie);
        }

        private void GivenSeriesMatch()
        {
            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetMovie(It.IsAny<string>()))
                  .Returns(_trackedDownload.GetRemoteMovie().Movie);
        }

        [Test]
        public void invalid_local_path_blocks_import_without_exposing_the_path()
        {
            var privatePath = OsInfo.IsWindows ? "/private/download" : @"C:\private\download";
            _trackedDownload.DownloadItem.OutputPath = new OsPath(privatePath);

            Subject.Import(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.Downloading);
            _trackedDownload.Status.Should().Be(TrackedDownloadStatus.Warning);
            _trackedDownload.StatusMessages.Should().ContainSingle().Which.Messages.Should().ContainSingle()
                .Which.Should().NotContain(privatePath);
            Mocker.GetMock<IDownloadedMovieImportService>()
                .Verify(v => v.ProcessPath(It.IsAny<string>(), It.IsAny<ImportMode>(), It.IsAny<Movie>(), It.IsAny<DownloadClientItem>()), Times.Never());
        }

        [Test]
        public void empty_import_result_explains_location_and_file_checks_without_exposing_the_path()
        {
            Mocker.GetMock<IDownloadedMovieImportService>()
                .Setup(v => v.ProcessPath(It.IsAny<string>(), It.IsAny<ImportMode>(), It.IsAny<Movie>(), It.IsAny<DownloadClientItem>()))
                .Returns(new List<ImportResult>());

            Subject.Import(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.ImportPending);
            _trackedDownload.Status.Should().Be(TrackedDownloadStatus.Warning);
            var message = _trackedDownload.StatusMessages.Should().ContainSingle().Subject.Messages.Should().ContainSingle().Subject;
            message.Should().Contain("No files were imported")
                .And.Contain("download location")
                .And.Contain("remote path mappings")
                .And.Contain("import eligibility")
                .And.NotContain(_trackedDownload.DownloadItem.OutputPath.FullPath);
            Mocker.GetMock<IEventAggregator>().Verify(v => v.PublishEvent(It.IsAny<DownloadCompletedEvent>()), Times.Never());
        }

        [Test]
        public void should_not_mark_as_imported_if_all_files_were_rejected()
        {
            Mocker.GetMock<IDownloadedMovieImportService>()
                .Setup(v => v.ProcessPath(It.IsAny<string>(), It.IsAny<ImportMode>(), It.IsAny<Movie>(), It.IsAny<DownloadClientItem>()))
                .Returns(new List<ImportResult>
                {
                    new ImportResult(
                        new ImportDecision(
                            new LocalMovie { Path = @"C:\TestPath\Droned.1998.mkv" },
                            new ImportRejection(ImportRejectionReason.Unknown, "Rejected!")),
                        "Test Failure"),

                    new ImportResult(
                        new ImportDecision(
                            new LocalMovie { Path = @"C:\TestPath\Droned.1999.mkv" },
                            new ImportRejection(ImportRejectionReason.Unknown, "Rejected!")),
                        "Test Failure")
                });

            Subject.Import(_trackedDownload);

            Mocker.GetMock<IEventAggregator>()
                .Verify(v => v.PublishEvent<DownloadCompletedEvent>(It.IsAny<DownloadCompletedEvent>()), Times.Never());

            AssertNotImported();
        }

        [Test]
        public void should_not_mark_as_imported_if_no_movies_were_parsed()
        {
            Mocker.GetMock<IDownloadedMovieImportService>()
                .Setup(v => v.ProcessPath(It.IsAny<string>(), It.IsAny<ImportMode>(), It.IsAny<Movie>(), It.IsAny<DownloadClientItem>()))
                .Returns(new List<ImportResult>
                {
                    new ImportResult(
                        new ImportDecision(
                            new LocalMovie { Path = @"C:\TestPath\Droned.1998.mkv" },
                            new ImportRejection(ImportRejectionReason.Unknown, "Rejected!")),
                        "Test Failure"),

                    new ImportResult(
                        new ImportDecision(
                            new LocalMovie { Path = @"C:\TestPath\Droned.1998.mkv" },
                            new ImportRejection(ImportRejectionReason.Unknown, "Rejected!")),
                        "Test Failure")
                });

            _trackedDownload.GetRemoteMovie().Movie = new Movie();

            Subject.Import(_trackedDownload);

            AssertNotImported();
        }

        [Test]
        public void should_not_mark_as_imported_if_all_files_were_skipped()
        {
            Mocker.GetMock<IDownloadedMovieImportService>()
                  .Setup(v => v.ProcessPath(It.IsAny<string>(), It.IsAny<ImportMode>(), It.IsAny<Movie>(), It.IsAny<DownloadClientItem>()))
                  .Returns(new List<ImportResult>
                           {
                               new ImportResult(new ImportDecision(new LocalMovie { Path = @"C:\TestPath\Droned.1998.mkv" }), "Test Failure"),
                               new ImportResult(new ImportDecision(new LocalMovie { Path = @"C:\TestPath\Droned.1998.mkv" }), "Test Failure")
                           });

            Subject.Import(_trackedDownload);

            AssertNotImported();
        }

        [Test]
        public void should_mark_as_imported_if_all_movies_were_imported_but_extra_files_were_not()
        {
            GivenSeriesMatch();

            _trackedDownload.GetRemoteMovie().Movie = new Movie();

            Mocker.GetMock<IDownloadedMovieImportService>()
                  .Setup(v => v.ProcessPath(It.IsAny<string>(), It.IsAny<ImportMode>(), It.IsAny<Movie>(), It.IsAny<DownloadClientItem>()))
                  .Returns(new List<ImportResult>
               {
                               new ImportResult(new ImportDecision(new LocalMovie { Path = @"C:\TestPath\Droned.S01E01.mkv", Movie = _trackedDownload.GetRemoteMovie().Movie })),
                               new ImportResult(new ImportDecision(new LocalMovie { Path = @"C:\TestPath\Droned.S01E01.mkv" }), "Test Failure")
               });

            Subject.Import(_trackedDownload);

            AssertImported();
        }

        [Test]
        public void should_mark_as_imported_if_the_download_can_be_tracked_using_the_source_movieid()
        {
            GivenABadlyNamedDownload();

            Mocker.GetMock<IDownloadedMovieImportService>()
                  .Setup(v => v.ProcessPath(It.IsAny<string>(), It.IsAny<ImportMode>(), It.IsAny<Movie>(), It.IsAny<DownloadClientItem>()))
                  .Returns(new List<ImportResult>
               {
                               new ImportResult(new ImportDecision(new LocalMovie { Path = @"C:\TestPath\Droned.S01E01.mkv", Movie = _trackedDownload.GetRemoteMovie().Movie }))
               });

            Mocker.GetMock<IMovieService>()
                  .Setup(v => v.GetMovie(It.IsAny<int>()))
                  .Returns(BuildRemoteMovie().Movie);

            Subject.Import(_trackedDownload);

            AssertImported();
        }

        private void AssertNotImported()
        {
            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<DownloadCompletedEvent>()), Times.Never());

            _trackedDownload.State.Should().Be(TrackedDownloadState.ImportBlocked);
        }

        private void AssertImported()
        {
            Mocker.GetMock<IDownloadedMovieImportService>()
                .Verify(v => v.ProcessPath(_trackedDownload.DownloadItem.OutputPath.FullPath, ImportMode.Auto, _trackedDownload.GetRemoteMovie().Movie, _trackedDownload.DownloadItem), Times.Once());

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<DownloadCompletedEvent>()), Times.Once());

            _trackedDownload.State.Should().Be(TrackedDownloadState.Imported);
        }
    }
}
