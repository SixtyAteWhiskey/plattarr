using System;
using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Download.FailedDownloadServiceTests
{
    // The core runs the failed download state machine; the module of the download's media type says
    // whether it grabbed the download and describes the failure.
    [TestFixture]
    public class SourceRoutingFixture : CoreTest<FailedDownloadService>
    {
        private Mock<IFailedDownloadSource> _movies;
        private Mock<IFailedDownloadSource> _tv;
        private TrackedDownload _trackedDownload;

        [SetUp]
        public void Setup()
        {
            _movies = GivenSource(MediaKind.Movies, grabbed: false);
            _tv = GivenSource(MediaKind.Tv, grabbed: true);

            Mocker.SetConstant<IEnumerable<IFailedDownloadSource>>(new[] { _movies.Object, _tv.Object });

            var item = Builder<DownloadClientItem>.CreateNew()
                                                  .With(h => h.Status = DownloadItemStatus.Failed)
                                                  .With(h => h.OutputPath = new OsPath(@"C:\DropFolder\MyDownload".AsOsAgnostic()))
                                                  .With(h => h.Title = "Show.S01E01.HDTV")
                                                  .Build();

            _trackedDownload = Builder<TrackedDownload>.CreateNew()
                                                       .With(c => c.State = TrackedDownloadState.Downloading)
                                                       .With(c => c.DownloadItem = item)
                                                       .With(c => c.Kind = MediaKind.Tv)
                                                       .Build();
        }

        private static Mock<IFailedDownloadSource> GivenSource(MediaKind kind, bool grabbed)
        {
            var mock = new Mock<IFailedDownloadSource>();
            mock.SetupGet(s => s.Kind).Returns(kind);
            mock.Setup(s => s.WasGrabbed(It.IsAny<string>())).Returns(grabbed);
            mock.Setup(s => s.Describe(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TrackedDownload>(), It.IsAny<bool>()))
                .Returns<string, string, TrackedDownload, bool>((id, message, td, skip) => grabbed
                    ? new DownloadFailedEvent { Kind = kind, ParentId = 5, DownloadId = id, Message = message, TrackedDownload = td, SkipRedownload = skip }
                    : null);
            mock.Setup(s => s.Describe(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns<int, string, bool>((id, message, skip) => new DownloadFailedEvent { Kind = kind, ParentId = id, Message = message, SkipRedownload = skip });

            return mock;
        }

        [Test]
        public void check_should_ask_the_source_of_the_downloads_media_type()
        {
            Subject.Check(_trackedDownload);

            _tv.Verify(s => s.WasGrabbed(_trackedDownload.DownloadItem.DownloadId), Times.Once());
            _movies.Verify(s => s.WasGrabbed(It.IsAny<string>()), Times.Never());
            _trackedDownload.State.Should().Be(TrackedDownloadState.FailedPending);
        }

        [Test]
        public void check_should_not_fail_a_download_another_media_type_grabbed()
        {
            _trackedDownload.Kind = MediaKind.Movies;

            Subject.Check(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.Downloading);
            _trackedDownload.StatusMessages.Should().NotBeEmpty();
        }

        [Test]
        public void check_should_warn_instead_of_failing_a_download_with_no_known_media_type()
        {
            _trackedDownload.Kind = null;

            Subject.Check(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.Downloading);
            _trackedDownload.StatusMessages.Should().NotBeEmpty();
        }

        [Test]
        public void check_should_warn_instead_of_failing_a_download_whose_media_type_has_no_source()
        {
            Mocker.SetConstant<IEnumerable<IFailedDownloadSource>>(new[] { _movies.Object });

            Subject.Check(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.Downloading);
            _trackedDownload.StatusMessages.Should().NotBeEmpty();
        }

        [Test]
        public void process_failed_should_publish_the_event_the_source_described()
        {
            _trackedDownload.State = TrackedDownloadState.FailedPending;
            _trackedDownload.DownloadItem.Message = "Disk full";

            Subject.ProcessFailed(_trackedDownload);

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.Is<DownloadFailedEvent>(e => e.Kind == MediaKind.Tv && e.Message == "Disk full" && e.TrackedDownload == _trackedDownload && !e.SkipRedownload)), Times.Once());

            _trackedDownload.State.Should().Be(TrackedDownloadState.Failed);
        }

        [Test]
        public void process_failed_should_leave_a_download_alone_when_the_source_has_no_grab()
        {
            _trackedDownload.State = TrackedDownloadState.FailedPending;
            _trackedDownload.Kind = MediaKind.Movies;

            Subject.ProcessFailed(_trackedDownload);

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<DownloadFailedEvent>()), Times.Never());

            _trackedDownload.State.Should().Be(TrackedDownloadState.FailedPending);
        }

        [Test]
        public void mark_tracked_download_as_failed_should_pass_the_skip_flag_to_the_source()
        {
            Subject.MarkAsFailed(_trackedDownload, skipRedownload: true);

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.Is<DownloadFailedEvent>(e => e.Kind == MediaKind.Tv && e.SkipRedownload && e.Message == "Manually marked as failed")), Times.Once());
        }

        [Test]
        public void mark_tracked_download_as_failed_should_publish_nothing_without_a_grab()
        {
            _trackedDownload.Kind = MediaKind.Movies;

            Subject.MarkAsFailed(_trackedDownload);

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<DownloadFailedEvent>()), Times.Never());
        }

        [Test]
        public void mark_history_as_failed_should_use_the_source_of_the_given_media_type()
        {
            Subject.MarkAsFailed(MediaKind.Tv, 33, true);

            _tv.Verify(s => s.Describe(33, "Manually marked as failed", true), Times.Once());
            _movies.Verify(s => s.Describe(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never());

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.Is<DownloadFailedEvent>(e => e.Kind == MediaKind.Tv && e.ParentId == 33)), Times.Once());
        }

        [Test]
        public void mark_history_as_failed_should_throw_for_a_media_type_with_no_source()
        {
            Mocker.SetConstant<IEnumerable<IFailedDownloadSource>>(new[] { _movies.Object });

            Action mark = () => Subject.MarkAsFailed(MediaKind.Tv, 33);

            mark.Should().Throw<InvalidOperationException>();
        }
    }
}
