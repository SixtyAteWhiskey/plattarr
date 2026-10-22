using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download.DownloadHistoryTests
{
    // Failed, ignored and completed downloads are recorded for the media type the event names.
    [TestFixture]
    public class DownloadHistoryServiceEventsFixture : CoreTest<DownloadHistoryService>
    {
        private DownloadHistory _inserted;

        [SetUp]
        public void Setup()
        {
            _inserted = null;

            Mocker.GetMock<IDownloadHistoryRepository>()
                .Setup(r => r.Insert(It.IsAny<DownloadHistory>()))
                .Callback<DownloadHistory>(h => _inserted = h)
                .Returns<DownloadHistory>(h => h);
        }

        private static DownloadClientItem Item()
        {
            return new DownloadClientItem
            {
                Title = "Release.Title",
                DownloadId = "HASH",
                DownloadClientInfo = new DownloadClientItemClientInfo { Id = 3, Name = "qb-main", Type = "QBittorrent", Protocol = DownloadProtocol.Torrent }
            };
        }

        private static TrackedDownload Tracked(MediaKind? kind)
        {
            return new TrackedDownload
            {
                Kind = kind,
                DownloadClient = 3,
                Protocol = DownloadProtocol.Torrent,
                DownloadItem = Item()
            };
        }

        [TestCase(MediaKind.Movies)]
        [TestCase(MediaKind.Tv)]
        [TestCase(MediaKind.Music)]
        public void should_record_a_failed_download_for_the_media_type_of_the_event(MediaKind kind)
        {
            Subject.Handle(new DownloadFailedEvent
            {
                Kind = kind,
                ParentId = 21,
                DownloadId = "HASH",
                SourceTitle = "Release.Title",
                Quality = new QualityModel(),
                Languages = new List<Language>(),
                TrackedDownload = Tracked(kind)
            });

            _inserted.MediaKind.Should().Be(kind);
            _inserted.ParentId.Should().Be(21);
            _inserted.EventType.Should().Be(DownloadHistoryEventType.DownloadFailed);
            _inserted.DownloadId.Should().Be("HASH");
            _inserted.DownloadClientId.Should().Be(3);
            _inserted.Data["DownloadClientName"].Should().Be("qb-main");
        }

        [Test]
        public void should_not_record_a_failure_of_an_unknown_download()
        {
            Subject.Handle(new DownloadFailedEvent { Kind = MediaKind.Tv, ParentId = 21, TrackedDownload = null });

            _inserted.Should().BeNull();
        }

        [TestCase(MediaKind.Movies)]
        [TestCase(MediaKind.Tv)]
        public void should_record_an_ignored_download_for_the_media_type_of_the_event(MediaKind kind)
        {
            var item = Item();

            Subject.Handle(new DownloadIgnoredEvent
            {
                Kind = kind,
                ParentId = 22,
                DownloadId = "HASH",
                SourceTitle = "Release.Title",
                DownloadClientInfo = item.DownloadClientInfo
            });

            _inserted.MediaKind.Should().Be(kind);
            _inserted.ParentId.Should().Be(22);
            _inserted.EventType.Should().Be(DownloadHistoryEventType.DownloadIgnored);
            _inserted.DownloadClientId.Should().Be(3);
        }

        [TestCase(MediaKind.Movies)]
        [TestCase(MediaKind.Tv)]
        public void should_record_a_completed_download_for_the_media_type_of_the_tracked_download(MediaKind kind)
        {
            Subject.Handle(new DownloadCompletedEvent(Tracked(kind), 23));

            _inserted.MediaKind.Should().Be(kind);
            _inserted.ParentId.Should().Be(23);
            _inserted.EventType.Should().Be(DownloadHistoryEventType.DownloadImported);
            _inserted.DownloadId.Should().Be("HASH");
            _inserted.DownloadClientId.Should().Be(3);
        }

        [Test]
        public void should_record_no_media_type_for_a_tracked_download_without_one()
        {
            // The repository refuses it; the service does not guess a kind.
            Subject.Handle(new DownloadCompletedEvent(Tracked(null), 23));

            _inserted.MediaKind.Should().Be(default(MediaKind));
        }
    }
}
