using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
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

namespace NzbDrone.Core.Test.Download.CompletedDownloadServiceTests
{
    [TestFixture]
    public class ProcessFixture : CoreTest<CompletedDownloadService>
    {
        private TrackedDownload _trackedDownload;

        [SetUp]
        public void Setup()
        {
            var completed = Builder<DownloadClientItem>.CreateNew()
                                                    .With(h => h.Status = DownloadItemStatus.Completed)
                                                    .With(h => h.OutputPath = new OsPath(@"C:\DropFolder\MyDownload".AsOsAgnostic()))
                                                    .With(h => h.Title = "Drone.S01E01.HDTV")
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

            Mocker.GetMock<IProvideImportItemService>()
                  .Setup(c => c.ProvideImportItem(It.IsAny<DownloadClientItem>(), It.IsAny<DownloadClientItem>()))
                  .Returns((DownloadClientItem item, DownloadClientItem previous) => item);

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByDownloadId(_trackedDownload.DownloadItem.DownloadId))
                  .Returns(new List<MovieHistory>());

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetMovie("Drone.S01E01.HDTV"))
                  .Returns(remoteMovie.Movie);

            Mocker.SetConstant<IEnumerable<IDownloadImporter>>(new[] { Mocker.Resolve<MovieDownloadImporter>() });
        }

        private RemoteMovie BuildRemoteMovie()
        {
            return new RemoteMovie
            {
                Movie = new Movie(),
            };
        }

        private void GivenNoGrabbedHistory()
        {
            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.FindByDownloadId(_trackedDownload.DownloadItem.DownloadId))
                .Returns(new List<MovieHistory>());
        }

        private void GivenMovieMatch()
        {
            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetMovie(It.IsAny<string>()))
                  .Returns(_trackedDownload.GetRemoteMovie().Movie);
        }

        private void GivenABadlyNamedDownload()
        {
            _trackedDownload.DownloadItem.DownloadId = "1234";
            _trackedDownload.DownloadItem.Title = "Droned Pilot"; // Set a badly named download
            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByDownloadId(It.Is<string>(i => i == "1234")))
                  .Returns(new List<MovieHistory>
                  {
                      new MovieHistory() { SourceTitle = "Droned S01E01", EventType = MovieHistoryEventType.Grabbed }
                  });

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetMovie(It.IsAny<string>()))
                  .Returns((Movie)null);

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetMovie("Droned S01E01"))
                  .Returns(BuildRemoteMovie().Movie);
        }

        [TestCase(DownloadItemStatus.Downloading)]
        [TestCase(DownloadItemStatus.Failed)]
        [TestCase(DownloadItemStatus.Queued)]
        [TestCase(DownloadItemStatus.Paused)]
        [TestCase(DownloadItemStatus.Warning)]
        public void should_not_process_if_download_status_isnt_completed(DownloadItemStatus status)
        {
            _trackedDownload.DownloadItem.Status = status;

            Subject.Check(_trackedDownload);

            AssertNotReadyToImport();
        }

        [Test]
        public void should_not_process_if_matching_history_is_not_found_and_no_category_specified()
        {
            _trackedDownload.DownloadItem.Category = null;
            GivenNoGrabbedHistory();

            Subject.Check(_trackedDownload);

            AssertNotReadyToImport();
        }

        [Test]
        public void should_process_if_matching_history_is_not_found_but_category_specified()
        {
            _trackedDownload.DownloadItem.Category = "tv";
            GivenNoGrabbedHistory();
            GivenMovieMatch();

            Subject.Check(_trackedDownload);

            AssertReadyToImport();
        }

        [Test]
        public void should_not_process_if_output_path_is_empty()
        {
            _trackedDownload.DownloadItem.OutputPath = default(OsPath);

            Subject.Check(_trackedDownload);

            AssertNotReadyToImport();
            _trackedDownload.Status.Should().Be(TrackedDownloadStatus.Warning);
            _trackedDownload.StatusMessages.Should().ContainSingle().Which.Messages.Should().ContainSingle()
                .Which.Should().Be("No download location is available for this completed item, so automatic import cannot continue. Check the download client's completed-download location and path mappings.");
        }

        [Test]
        public void should_explain_invalid_local_path_without_exposing_it()
        {
            var privatePath = OsInfo.IsWindows ? "/private/download" : @"C:\private\download";
            _trackedDownload.DownloadItem.OutputPath = new OsPath(privatePath);

            Subject.Check(_trackedDownload);

            AssertNotReadyToImport();
            _trackedDownload.Status.Should().Be(TrackedDownloadStatus.Warning);
            _trackedDownload.StatusMessages.Should().ContainSingle().Which.Messages.Should().ContainSingle()
                .Which.Should().Be("The download location is not a valid path for this system, so automatic import cannot continue. Check the download client's location and configure a Remote Path Mapping if the client uses a different path.")
                .And.NotContain(privatePath);
        }

        [Test]
        public void should_not_process_if_the_download_cannot_be_tracked_using_the_source_title_as_it_was_initiated_externally()
        {
            GivenABadlyNamedDownload();

            Mocker.GetMock<IDownloadedMovieImportService>()
                  .Setup(v => v.ProcessPath(It.IsAny<string>(), It.IsAny<ImportMode>(), It.IsAny<Movie>(), It.IsAny<DownloadClientItem>()))
                  .Returns(new List<ImportResult>
                           {
                               new ImportResult(new ImportDecision(new LocalMovie { Path = @"C:\TestPath\Droned.S01E01.mkv" }))
                           });

            Subject.Check(_trackedDownload);

            AssertNotReadyToImport();
        }

        [Test]
        public void should_not_process_when_there_is_a_title_mismatch()
        {
            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetMovie("Drone.S01E01.HDTV"))
                  .Returns((Movie)null);

            Subject.Check(_trackedDownload);

            AssertNotReadyToImport();
        }

        private void AssertNotReadyToImport()
        {
            _trackedDownload.State.Should().NotBe(TrackedDownloadState.ImportPending);
        }

        private void AssertReadyToImport()
        {
            _trackedDownload.State.Should().Be(TrackedDownloadState.ImportPending);
        }
    }
}
