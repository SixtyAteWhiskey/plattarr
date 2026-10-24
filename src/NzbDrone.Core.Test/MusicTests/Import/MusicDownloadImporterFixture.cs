using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.History;
using NzbDrone.Core.Music.Import;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.MediaFiles.TrackImport;
using NzbDrone.Core.Music.Notifications;
using NzbDrone.Core.Music.Parser;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MusicTests.Import
{
    // The Music half of contract 5: what the core asks the importer, and what it must answer.
    [TestFixture]
    public class MusicDownloadImporterFixture : CoreTest<MusicDownloadImporter>
    {
        private TrackedDownload _trackedDownload;
        private Artist _artist;
        private Album _album1;
        private Album _album2;

        [SetUp]
        public void Setup()
        {
            _artist = new Artist { Id = 5, Name = "Artist" };
            _album1 = NewAlbum(1, 10);
            _album2 = NewAlbum(2, 8);

            _trackedDownload = new TrackedDownload
            {
                Kind = MediaKind.Music,
                State = TrackedDownloadState.Downloading,
                DownloadItem = new DownloadClientItem
                {
                    DownloadId = "dl1",
                    Title = "Artist - Album [2018 - FLAC]",
                    Category = "music",
                    OutputPath = new OsPath(@"C:\DropFolder\Album".AsOsAgnostic())
                },
                RemoteRelease = new RemoteAlbum { Artist = _artist, Albums = new List<Album> { _album1, _album2 } }
            };

            // The core sets the import item before it calls the importer.
            _trackedDownload.ImportItem = _trackedDownload.DownloadItem;

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByDownloadId(It.IsAny<string>()))
                  .Returns(new List<AlbumHistory>());
        }

        private static Album NewAlbum(int id, int trackCount)
        {
            var release = new AlbumRelease { Monitored = true, TrackCount = trackCount };
            var unmonitored = new AlbumRelease { Monitored = false, TrackCount = 99 };

            return new Album { Id = id, AlbumReleases = new List<AlbumRelease> { release, unmonitored } };
        }

        private static ImportResult Imported(params Track[] tracks)
        {
            return new ImportResult(new ImportDecision<LocalTrack>(new LocalTrack { Path = @"C:\Test\track.flac", Tracks = tracks.ToList() }));
        }

        private static Track[] Tracks(int count)
        {
            return Enumerable.Range(1, count).Select(i => new Track { Id = i }).ToArray();
        }

        [Test]
        public void should_be_the_music_importer_and_block_the_download_after_a_rejected_result()
        {
            Subject.Kind.Should().Be(MediaKind.Music);
            Subject.BlocksImportAfterRejection.Should().BeTrue();
        }

        [Test]
        public void check_should_skip_a_download_that_was_not_grabbed_and_has_no_category()
        {
            _trackedDownload.DownloadItem.Category = null;

            Subject.Check(_trackedDownload).Should().Be(DownloadImportCheck.Skip);

            _trackedDownload.Status.Should().Be(TrackedDownloadStatus.Warning);
        }

        [Test]
        public void check_should_block_a_download_whose_artist_cannot_be_found()
        {
            Mocker.GetMock<IParsingService>().Setup(s => s.GetArtist(It.IsAny<string>())).Returns((Artist)null);

            Subject.Check(_trackedDownload).Should().Be(DownloadImportCheck.Blocked);

            _trackedDownload.Status.Should().Be(TrackedDownloadStatus.Warning);
            _trackedDownload.StatusMessages.Single().Messages.Should().ContainSingle().Which.Should().Contain("Artist name mismatch");
        }

        [Test]
        public void check_should_be_ready_when_the_title_matches_an_artist()
        {
            Mocker.GetMock<IParsingService>().Setup(s => s.GetArtist("Artist - Album [2018 - FLAC]")).Returns(_artist);

            Subject.Check(_trackedDownload).Should().Be(DownloadImportCheck.Ready);

            _trackedDownload.Status.Should().Be(TrackedDownloadStatus.Ok);
        }

        [Test]
        public void check_should_use_the_artist_from_grab_history_when_the_title_does_not_match()
        {
            var grabbed = new AlbumHistory { DownloadId = "dl1", ParentId = 5, ItemId = 1, EventType = AlbumHistoryEventType.Grabbed, Date = DateTime.UtcNow };

            Mocker.GetMock<IHistoryService>().Setup(s => s.MostRecentForDownloadId("dl1")).Returns(grabbed);
            Mocker.GetMock<IParsingService>().Setup(s => s.GetArtist(It.IsAny<string>())).Returns((Artist)null);
            Mocker.GetMock<IArtistService>().Setup(s => s.GetArtist(5)).Returns(_artist);

            Subject.Check(_trackedDownload).Should().Be(DownloadImportCheck.Ready);
        }

        [Test]
        public void check_should_block_when_neither_the_title_nor_the_history_names_an_artist()
        {
            var grabbed = new AlbumHistory { DownloadId = "dl1", ParentId = 5, ItemId = 1, EventType = AlbumHistoryEventType.Grabbed, Date = DateTime.UtcNow };

            Mocker.GetMock<IHistoryService>().Setup(s => s.MostRecentForDownloadId("dl1")).Returns(grabbed);
            Mocker.GetMock<IParsingService>().Setup(s => s.GetArtist(It.IsAny<string>())).Returns((Artist)null);
            Mocker.GetMock<IArtistService>().Setup(s => s.GetArtist(5)).Returns((Artist)null);

            Subject.Check(_trackedDownload).Should().Be(DownloadImportCheck.Blocked);
        }

        [Test]
        public void import_should_process_the_output_path_for_the_matched_artist_and_return_the_results_unchanged()
        {
            var results = new List<ImportResult> { Imported(Tracks(2)) };

            Mocker.GetMock<IDownloadedTracksImportService>()
                  .Setup(s => s.ProcessPath(@"C:\out", ImportMode.Auto, _artist, _trackedDownload.ImportItem))
                  .Returns(results);

            var returned = Subject.Import(_trackedDownload, @"C:\out");

            returned.Should().HaveCount(1);
            returned[0].Should().BeSameAs(results[0]);
            returned[0].Result.Should().Be(ImportResultType.Imported);
        }

        [Test]
        public void is_imported_should_be_true_when_every_result_was_imported()
        {
            var results = new List<IImportResult> { Imported(Tracks(1)), Imported(Tracks(1)) };

            Subject.IsImported(_trackedDownload, results).Should().BeTrue();
        }

        [Test]
        public void is_imported_should_be_true_when_the_imported_tracks_cover_every_monitored_release_track()
        {
            var results = new List<IImportResult>
            {
                Imported(Tracks(18)),
                new ImportResult(new ImportDecision<LocalTrack>(new LocalTrack { Path = @"C:\Test\x.flac" }, new ImportRejection(ImportRejectionReason.Unknown, "Rejected")), "Rejected")
            };

            Subject.IsImported(_trackedDownload, results).Should().BeTrue();
        }

        [Test]
        public void is_imported_should_ignore_unmonitored_releases_when_counting_expected_tracks()
        {
            // 10 + 8 monitored tracks are expected; the unmonitored releases (99 each) are not.
            var results = new List<IImportResult>
            {
                Imported(Tracks(17)),
                new ImportResult(new ImportDecision<LocalTrack>(new LocalTrack { Path = @"C:\Test\x.flac" }, new ImportRejection(ImportRejectionReason.Unknown, "Rejected")), "Rejected")
            };

            Mocker.GetMock<ITrackedDownloadAlreadyImported>()
                  .Setup(s => s.IsImported(It.IsAny<TrackedDownload>(), It.IsAny<List<AlbumHistory>>()))
                  .Returns(false);

            Subject.IsImported(_trackedDownload, results).Should().BeFalse();
        }

        [Test]
        public void is_imported_should_be_false_when_tracks_are_missing_and_history_has_no_import()
        {
            var results = new List<IImportResult>
            {
                Imported(Tracks(1)),
                new ImportResult(new ImportDecision<LocalTrack>(new LocalTrack { Path = @"C:\Test\x.flac" }, new ImportRejection(ImportRejectionReason.Unknown, "Rejected")), "Rejected")
            };

            Mocker.GetMock<ITrackedDownloadAlreadyImported>()
                  .Setup(s => s.IsImported(It.IsAny<TrackedDownload>(), It.IsAny<List<AlbumHistory>>()))
                  .Returns(false);

            Subject.IsImported(_trackedDownload, results).Should().BeFalse();
        }

        [Test]
        public void is_imported_should_be_true_from_history_when_nothing_was_just_imported()
        {
            Mocker.GetMock<ITrackedDownloadAlreadyImported>()
                  .Setup(s => s.IsImported(It.IsAny<TrackedDownload>(), It.IsAny<List<AlbumHistory>>()))
                  .Returns(true);

            Subject.IsImported(_trackedDownload, new List<IImportResult>()).Should().BeTrue();
        }

        [Test]
        public void is_imported_should_be_false_with_no_results_and_no_history()
        {
            Mocker.GetMock<ITrackedDownloadAlreadyImported>()
                  .Setup(s => s.IsImported(It.IsAny<TrackedDownload>(), It.IsAny<List<AlbumHistory>>()))
                  .Returns(false);

            Subject.IsImported(_trackedDownload, new List<IImportResult>()).Should().BeFalse();
        }

        [Test]
        public void import_blocked_should_publish_a_manual_interaction_event_with_the_artist_and_release()
        {
            Subject.ImportBlocked(_trackedDownload);

            Mocker.GetMock<IEventAggregator>()
                  .Verify(
                      e => e.PublishEvent(It.Is<ManualInteractionRequiredEvent>(m =>
                          m.Kind == MediaKind.Music &&
                          m.TrackedDownload == _trackedDownload &&
                          ((MusicNotificationData)m.Data).Artist == _artist &&
                          ((MusicNotificationData)m.Data).RemoteAlbum == _trackedDownload.RemoteRelease)),
                      Times.Once());
        }

        [Test]
        public void import_blocked_should_publish_for_a_download_that_matched_no_artist()
        {
            _trackedDownload.RemoteRelease = null;

            Subject.ImportBlocked(_trackedDownload);

            Mocker.GetMock<IEventAggregator>()
                  .Verify(e => e.PublishEvent(It.Is<ManualInteractionRequiredEvent>(m => ((MusicNotificationData)m.Data).Artist == null)), Times.Once());
        }
    }
}
