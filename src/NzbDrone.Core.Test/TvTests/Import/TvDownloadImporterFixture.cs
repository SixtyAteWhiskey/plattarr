using System.Collections.Generic;
using System.Linq;
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
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.History;
using NzbDrone.Core.Tv.Import;
using NzbDrone.Core.Tv.MediaFiles;
using NzbDrone.Core.Tv.MediaFiles.EpisodeImport;
using NzbDrone.Core.Tv.Notifications;
using NzbDrone.Core.Tv.Parser;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.TvTests.Import
{
    // The TV half of contract 5: what the core asks the importer, and what it must answer.
    [TestFixture]
    public class TvDownloadImporterFixture : CoreTest<TvDownloadImporter>
    {
        private TrackedDownload _trackedDownload;
        private Series _series;
        private Episode _episode1;
        private Episode _episode2;

        [SetUp]
        public void Setup()
        {
            _series = new Series { Id = 5, Title = "Drone" };
            _episode1 = new Episode { Id = 1, SeasonNumber = 1, EpisodeNumber = 1 };
            _episode2 = new Episode { Id = 2, SeasonNumber = 1, EpisodeNumber = 2 };

            _trackedDownload = new TrackedDownload
            {
                Kind = MediaKind.Tv,
                State = TrackedDownloadState.Downloading,
                DownloadItem = new DownloadClientItem
                {
                    DownloadId = "dl1",
                    Title = "Drone.S01E01.HDTV",
                    Category = "tv",
                    OutputPath = new OsPath(@"C:\DropFolder\Drone".AsOsAgnostic())
                },
                RemoteRelease = new RemoteEpisode { Series = _series, Episodes = new List<Episode> { _episode1, _episode2 } }
            };

            // The core sets the import item before it calls the importer.
            _trackedDownload.ImportItem = _trackedDownload.DownloadItem;

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByDownloadId(It.IsAny<string>()))
                  .Returns(new List<EpisodeHistory>());
        }

        private EpisodeHistory GrabbedHistory(string matchType = null, string releaseSource = null)
        {
            var history = new EpisodeHistory { DownloadId = "dl1", ParentId = 5, ItemId = 1, EventType = EpisodeHistoryEventType.Grabbed, Date = System.DateTime.UtcNow };

            history.Data["indexer"] = "MyIndexer";
            history.Data["size"] = "1000";

            if (matchType != null)
            {
                history.Data[EpisodeHistory.SERIES_MATCH_TYPE] = matchType;
            }

            if (releaseSource != null)
            {
                history.Data[EpisodeHistory.RELEASE_SOURCE] = releaseSource;
            }

            return history;
        }

        private void GivenGrabbedHistory(EpisodeHistory history)
        {
            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByDownloadId("dl1"))
                  .Returns(new List<EpisodeHistory> { history });
        }

        [Test]
        public void should_be_the_tv_importer_and_block_the_download_after_a_rejected_result()
        {
            Subject.Kind.Should().Be(MediaKind.Tv);
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
        public void check_should_block_a_download_whose_series_cannot_be_found()
        {
            Mocker.GetMock<IParsingService>().Setup(s => s.GetSeries(It.IsAny<string>())).Returns((Series)null);

            Subject.Check(_trackedDownload).Should().Be(DownloadImportCheck.Blocked);

            _trackedDownload.Status.Should().Be(TrackedDownloadStatus.Warning);
            _trackedDownload.StatusMessages.Single().Messages.Should().ContainSingle().Which.Should().Contain("Series title mismatch");
        }

        [Test]
        public void check_should_be_ready_when_the_title_matches_a_series()
        {
            Mocker.GetMock<IParsingService>().Setup(s => s.GetSeries("Drone.S01E01.HDTV")).Returns(_series);

            Subject.Check(_trackedDownload).Should().Be(DownloadImportCheck.Ready);

            _trackedDownload.Status.Should().Be(TrackedDownloadStatus.Ok);
        }

        [Test]
        public void check_should_use_the_series_from_grab_history_when_the_title_does_not_match()
        {
            GivenGrabbedHistory(GrabbedHistory(SeriesMatchType.Title.ToString()));
            Mocker.GetMock<IParsingService>().Setup(s => s.GetSeries(It.IsAny<string>())).Returns((Series)null);
            Mocker.GetMock<ISeriesService>().Setup(s => s.GetSeries(5)).Returns(_series);

            Subject.Check(_trackedDownload).Should().Be(DownloadImportCheck.Ready);
        }

        [Test]
        public void check_should_block_a_history_match_made_by_id_that_was_not_an_interactive_search()
        {
            GivenGrabbedHistory(GrabbedHistory(SeriesMatchType.Id.ToString(), ReleaseSourceType.Rss.ToString()));
            Mocker.GetMock<IParsingService>().Setup(s => s.GetSeries(It.IsAny<string>())).Returns((Series)null);
            Mocker.GetMock<ISeriesService>().Setup(s => s.GetSeries(5)).Returns(_series);

            Subject.Check(_trackedDownload).Should().Be(DownloadImportCheck.Blocked);

            _trackedDownload.Status.Should().Be(TrackedDownloadStatus.Warning);
        }

        [Test]
        public void check_should_accept_a_history_match_made_by_id_in_an_interactive_search()
        {
            GivenGrabbedHistory(GrabbedHistory(SeriesMatchType.Id.ToString(), ReleaseSourceType.InteractiveSearch.ToString()));
            Mocker.GetMock<IParsingService>().Setup(s => s.GetSeries(It.IsAny<string>())).Returns((Series)null);
            Mocker.GetMock<ISeriesService>().Setup(s => s.GetSeries(5)).Returns(_series);

            Subject.Check(_trackedDownload).Should().Be(DownloadImportCheck.Ready);
        }

        [Test]
        public void import_should_process_the_output_path_for_the_matched_series_and_return_the_results_unchanged()
        {
            var results = new List<ImportResult>
            {
                new ImportResult(new ImportDecision(new LocalEpisode { Path = @"C:\Test\Drone.S01E01.mkv", Episodes = { _episode1 } }))
            };

            Mocker.GetMock<IDownloadedEpisodesImportService>()
                  .Setup(s => s.ProcessPath(@"C:\out", ImportMode.Auto, _series, _trackedDownload.ImportItem))
                  .Returns(results);

            var returned = Subject.Import(_trackedDownload, @"C:\out");

            returned.Should().HaveCount(1);
            returned[0].Should().BeSameAs(results[0]);
            returned[0].Result.Should().Be(ImportResultType.Imported);
        }

        [Test]
        public void is_imported_should_be_true_when_the_imported_files_cover_every_episode()
        {
            var results = new List<IImportResult>
            {
                new ImportResult(new ImportDecision(new LocalEpisode { Path = @"C:\Test\Drone.S01E01-E02.mkv", Episodes = { _episode1, _episode2 } }))
            };

            Subject.IsImported(_trackedDownload, results).Should().BeTrue();
        }

        [Test]
        public void is_imported_should_be_false_when_an_episode_is_missing_and_history_has_no_import()
        {
            var results = new List<IImportResult>
            {
                new ImportResult(new ImportDecision(new LocalEpisode { Path = @"C:\Test\Drone.S01E01.mkv", Episodes = { _episode1 } }))
            };

            Mocker.GetMock<ITrackedDownloadAlreadyImported>()
                  .Setup(s => s.IsImported(It.IsAny<TrackedDownload>(), It.IsAny<List<EpisodeHistory>>()))
                  .Returns(false);

            Subject.IsImported(_trackedDownload, results).Should().BeFalse();
        }

        [Test]
        public void is_imported_should_be_true_from_history_when_nothing_was_just_imported()
        {
            Mocker.GetMock<ITrackedDownloadAlreadyImported>()
                  .Setup(s => s.IsImported(It.IsAny<TrackedDownload>(), It.IsAny<List<EpisodeHistory>>()))
                  .Returns(true);

            Subject.IsImported(_trackedDownload, new List<IImportResult>()).Should().BeTrue();
        }

        [Test]
        public void is_imported_should_count_a_download_with_no_expected_episodes_as_needing_one_import()
        {
            _trackedDownload.GetRemoteEpisode().Episodes.Clear();

            Mocker.GetMock<ITrackedDownloadAlreadyImported>()
                  .Setup(s => s.IsImported(It.IsAny<TrackedDownload>(), It.IsAny<List<EpisodeHistory>>()))
                  .Returns(false);

            Subject.IsImported(_trackedDownload, new List<IImportResult>()).Should().BeFalse();
        }

        [Test]
        public void import_blocked_should_publish_a_manual_interaction_event_with_the_series_and_grabbed_release()
        {
            GivenGrabbedHistory(GrabbedHistory());

            Subject.ImportBlocked(_trackedDownload);

            Mocker.GetMock<IEventAggregator>()
                  .Verify(e => e.PublishEvent(It.Is<ManualInteractionRequiredEvent>(m =>
                      m.Kind == MediaKind.Tv &&
                      m.TrackedDownload == _trackedDownload &&
                      ((TvNotificationData)m.Data).Series == _series &&
                      ((TvNotificationData)m.Data).Release.Indexer == "MyIndexer")), Times.Once());
        }

        [Test]
        public void import_blocked_should_publish_without_release_info_when_nothing_was_grabbed()
        {
            Subject.ImportBlocked(_trackedDownload);

            Mocker.GetMock<IEventAggregator>()
                  .Verify(e => e.PublishEvent(It.Is<ManualInteractionRequiredEvent>(m => ((TvNotificationData)m.Data).Release == null)), Times.Once());
        }

        [Test]
        public void import_blocked_should_publish_for_a_download_that_matched_no_series()
        {
            _trackedDownload.RemoteRelease = null;

            Subject.ImportBlocked(_trackedDownload);

            Mocker.GetMock<IEventAggregator>()
                  .Verify(e => e.PublishEvent(It.Is<ManualInteractionRequiredEvent>(m => ((TvNotificationData)m.Data).Series == null)), Times.Once());
        }
    }
}
