using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.Downloads;
using NzbDrone.Core.Music.History;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.Download
{
    [TestFixture]
    public class MusicFailedDownloadSourceFixture : CoreTest<MusicFailedDownloadSource>
    {
        private static AlbumHistory Grab(int id, string downloadId, DateTime date, int albumId, int artistId = 5, AlbumHistoryEventType eventType = AlbumHistoryEventType.Grabbed)
        {
            var history = new AlbumHistory
            {
                Id = id,
                MediaKind = MediaKind.Music,
                EventType = eventType,
                ParentId = artistId,
                ItemId = albumId,
                DownloadId = downloadId,
                SourceTitle = "Artist - Album [FLAC]",
                Quality = new AudioQualityModel(AudioQuality.FLAC, new Revision(2)),
                Date = date
            };

            history.Data[AlbumHistory.DOWNLOAD_CLIENT] = "QBittorrent";
            history.Data[AlbumHistory.RELEASE_SOURCE] = ReleaseSourceType.InteractiveSearch.ToString();
            history.Data["indexer"] = "Knaben";

            return history;
        }

        private void GivenGrabs(params AlbumHistory[] grabs)
        {
            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.Find("HASH", AlbumHistoryEventType.Grabbed))
                  .Returns(new List<AlbumHistory>(grabs));
        }

        [Test]
        public void should_be_the_music_source()
        {
            Subject.Kind.Should().Be(MediaKind.Music);
        }

        [Test]
        public void should_report_a_grab_only_when_history_has_one()
        {
            GivenGrabs(Grab(1, "HASH", DateTime.UtcNow, 11));
            Subject.WasGrabbed("HASH").Should().BeTrue();

            GivenGrabs();
            Subject.WasGrabbed("HASH").Should().BeFalse();
        }

        [Test]
        public void should_describe_nothing_when_history_has_no_grab()
        {
            GivenGrabs();

            Subject.Describe("HASH", "failed", new TrackedDownload(), false).Should().BeNull();
        }

        [Test]
        public void should_name_every_album_the_release_covered()
        {
            var now = DateTime.UtcNow;
            GivenGrabs(Grab(1, "HASH", now, 11), Grab(2, "HASH", now, 12), Grab(3, "HASH", now, 11));

            var trackedDownload = new TrackedDownload();

            var failure = Subject.Describe("HASH", "Disk full", trackedDownload, true);

            failure.Kind.Should().Be(MediaKind.Music);
            failure.ParentId.Should().Be(5);
            failure.ItemIds.Should().BeEquivalentTo(new[] { 11, 12 });
            failure.SourceTitle.Should().Be("Artist - Album [FLAC]");
            failure.DownloadClient.Should().Be("QBittorrent");
            failure.DownloadId.Should().Be("HASH");
            failure.Message.Should().Be("Disk full");
            failure.ReleaseSource.Should().Be(ReleaseSourceType.InteractiveSearch);
            failure.TrackedDownload.Should().BeSameAs(trackedDownload);
            failure.SkipRedownload.Should().BeTrue();
            failure.Data["indexer"].Should().Be("Knaben");
        }

        [Test]
        public void should_carry_the_audio_quality_as_its_display_copy_that_the_music_handlers_can_turn_back()
        {
            GivenGrabs(Grab(1, "HASH", DateTime.UtcNow, 11));

            var failure = Subject.Describe("HASH", "failed", null, false);

            failure.Quality.Quality.Name.Should().Be(AudioQuality.FLAC.Name);
            failure.Quality.Revision.Version.Should().Be(2);
            AudioQualityModel.FromDisplayModel(failure.Quality).Should().Be(new AudioQualityModel(AudioQuality.FLAC, new Revision(2)));
        }

        [Test]
        public void should_read_an_unknown_release_source_when_history_has_none()
        {
            var grab = Grab(1, "HASH", DateTime.UtcNow, 11);
            grab.Data.Remove(AlbumHistory.RELEASE_SOURCE);
            GivenGrabs(grab);

            Subject.Describe("HASH", "failed", null, false).ReleaseSource.Should().Be(ReleaseSourceType.Unknown);
        }

        [Test]
        public void grabbed_history_row_without_a_download_id_should_describe_its_album_and_honor_skip_redownload()
        {
            Mocker.GetMock<IHistoryService>().Setup(s => s.Get(7)).Returns(Grab(7, null, DateTime.UtcNow, 11));

            var failure = Subject.Describe(7, "Manually marked as failed", true);

            failure.ItemIds.Should().Equal(11);
            failure.SkipRedownload.Should().BeTrue();
        }

        [Test]
        public void history_row_without_a_download_id_that_was_not_grabbed_should_throw()
        {
            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.Get(7))
                  .Returns(Grab(7, null, DateTime.UtcNow, 11, eventType: AlbumHistoryEventType.DownloadImported));

            Action act = () => Subject.Describe(7, "failed", false);

            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void history_row_with_a_download_id_should_gather_the_grabs_and_not_honor_skip_redownload()
        {
            var now = DateTime.UtcNow;
            var row = Grab(7, "HASH", now, 11);

            Mocker.GetMock<IHistoryService>().Setup(s => s.Get(7)).Returns(row);
            GivenGrabs(row, Grab(8, "HASH", now, 12));

            var failure = Subject.Describe(7, "Manually marked as failed", true);

            failure.SkipRedownload.Should().BeFalse();
            failure.TrackedDownload.Should().BeNull();
            failure.ItemIds.Should().BeEquivalentTo(new[] { 11, 12 });
        }

        [Test]
        public void history_row_with_a_download_id_and_no_grabbed_history_should_throw()
        {
            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.Get(7))
                  .Returns(Grab(7, "HASH", DateTime.UtcNow, 11, eventType: AlbumHistoryEventType.DownloadImported));
            GivenGrabs();

            Action act = () => Subject.Describe(7, "failed", false);

            act.Should().Throw<InvalidOperationException>();
        }
    }
}
