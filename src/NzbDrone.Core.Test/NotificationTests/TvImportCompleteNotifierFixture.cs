using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Download;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.History;
using NzbDrone.Core.Tv.MediaFiles;
using NzbDrone.Core.Tv.Notifications;
using NzbDrone.Core.Tv.RootFolders;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.NotificationTests
{
    [TestFixture]
    public class TvImportCompleteNotifierFixture : TestBase
    {
        private const string DownloadId = "HASH123";

        private Mock<INotificationDispatcher> _dispatcher;
        private Mock<IHistoryService> _history;
        private Mock<IEpisodeService> _episodeService;
        private Mock<IMediaFileService> _mediaFileService;
        private TvImportCompleteNotifier _notifier;
        private Series _series;
        private List<Episode> _episodes;
        private List<EpisodeFile> _files;
        private DownloadCompletedEvent _completed;
        private ImportCompleteMessage _sent;

        [SetUp]
        public void Setup()
        {
            _dispatcher = new Mock<INotificationDispatcher>();
            _dispatcher.Setup(d => d.AnyEnabled(NotificationEvent.ImportComplete)).Returns(true);
            _dispatcher.Setup(d => d.ImportComplete(It.IsAny<ImportCompleteMessage>())).Callback<ImportCompleteMessage>(m => _sent = m);

            _series = TvGoldenData.Series();
            _episodes = TvGoldenData.Episodes(_series);
            _episodes[0].EpisodeFileId = 10;
            _episodes[1].EpisodeFileId = 11;
            _files = new List<EpisodeFile>
            {
                TvGoldenData.EpisodeFile(_series, _episodes, 10, "Season 01/Game of Thrones - S01E01 WEBDL-1080p.mkv"),
                TvGoldenData.EpisodeFile(_series, _episodes, 11, "Season 01/Game of Thrones - S01E02 WEBDL-1080p.mkv")
            };

            var seriesService = new Mock<ISeriesService>();
            seriesService.Setup(s => s.GetSeries(_series.Id)).Returns(_series);

            _history = new Mock<IHistoryService>();
            _history.Setup(h => h.Find(DownloadId, EpisodeHistoryEventType.DownloadFolderImported))
                    .Returns(_episodes.Select(e => new EpisodeHistory { ParentId = _series.Id, ItemId = e.Id }).ToList());
            _history.Setup(h => h.Find(DownloadId, EpisodeHistoryEventType.Grabbed))
                    .Returns(new List<EpisodeHistory>
                    {
                        new EpisodeHistory
                        {
                            Date = new System.DateTime(2026, 10, 1, 0, 0, 0, System.DateTimeKind.Utc),
                            SourceTitle = "Game.of.Thrones.S01E01E02.1080p.WEB-DL-GRP",
                            ParentId = _series.Id,
                            ItemId = 101,
                            Data = new Dictionary<string, string> { { "indexer", "Knaben" }, { "size", "4294967296" } }
                        }
                    });

            _episodeService = new Mock<IEpisodeService>();
            _episodeService.Setup(s => s.GetEpisodes(It.IsAny<IEnumerable<int>>()))
                           .Returns<IEnumerable<int>>(ids => _episodes.Where(e => ids.Contains(e.Id)).ToList());

            _mediaFileService = new Mock<IMediaFileService>();
            _mediaFileService.Setup(s => s.GetFiles(It.IsAny<IEnumerable<int>>()))
                             .Returns<IEnumerable<int>>(ids => _files.Where(f => ids.Contains(f.Id)).ToList());

            var renderer = new TvActivityRenderer(seriesService.Object, _episodeService.Object, new Mock<IRootFolderService>().Object);
            _notifier = new TvImportCompleteNotifier(_dispatcher.Object, renderer, _history.Object, seriesService.Object, _episodeService.Object, _mediaFileService.Object, TestLogger);

            var tracked = TvGoldenData.Tracked(TvGoldenData.Remote(_series, _episodes));
            tracked.Kind = MediaKind.Tv;
            tracked.DownloadItem.OutputPath = new OsPath("/downloads/Game.of.Thrones.S01E01E02.1080p.WEB-DL-GRP");
            _completed = new DownloadCompletedEvent(tracked, _series.Id);
        }

        [Test]
        public void should_send_one_message_with_every_imported_episode_and_file()
        {
            _notifier.Handle(_completed);

            _dispatcher.Verify(d => d.ImportComplete(It.IsAny<ImportCompleteMessage>()), Times.Once());

            _sent.Message.Should().Be("Game of Thrones - 1x01x02 - Winter Is Coming + The Kingsroad [WEBDL-1080p Proper]");
            _sent.DownloadId.Should().Be(DownloadId);
            _sent.SourcePath.Should().Be("/downloads/Game.of.Thrones.S01E01E02.1080p.WEB-DL-GRP");
            _sent.DestinationPath.Should().Be("/tv/Game of Thrones/Season 01");
            _sent.ReleaseGroup.Should().Be("GRP");
            _sent.QualityName.Should().Be("WEBDL-1080p");
            _sent.Files.Select(f => f.Id).Should().Equal(10, 11);
            _sent.Release.Title.Should().Be("Game.of.Thrones.S01E01E02.1080p.WEB-DL-GRP");
            _sent.Release.Indexer.Should().Be("Knaben");
            _sent.DownloadClientInfo.Should().NotBeNull();

            var data = _sent.Data.Should().BeOfType<TvNotificationData>().Subject;
            data.Series.Should().BeSameAs(_series);
            data.Episodes.Select(e => e.EpisodeNumber).Should().Equal(1, 2);
            data.EpisodeFiles.Select(f => f.Id).Should().Equal(10, 11);
            data.Release.Title.Should().Be("Game.of.Thrones.S01E01E02.1080p.WEB-DL-GRP");
        }

        [Test]
        public void should_describe_the_series_so_tag_rules_can_match()
        {
            _notifier.Handle(_completed);

            _sent.Subject.Tags.Should().BeEquivalentTo(_series.Tags);
        }

        [Test]
        public void should_use_the_season_message_for_a_season_pack()
        {
            _completed.TrackedDownload.GetRemoteEpisode().ParsedEpisodeInfo.FullSeason = true;

            _notifier.Handle(_completed);

            _sent.Message.Should().Be("Game of Thrones - Season 1 [WEBDL-1080p Proper]");
        }

        [Test]
        public void should_order_episodes_and_files_whatever_order_history_lists_them()
        {
            _history.Setup(h => h.Find(DownloadId, EpisodeHistoryEventType.DownloadFolderImported))
                    .Returns(new List<EpisodeHistory>
                    {
                        new EpisodeHistory { ParentId = _series.Id, ItemId = 102 },
                        new EpisodeHistory { ParentId = _series.Id, ItemId = 101 },
                        new EpisodeHistory { ParentId = _series.Id, ItemId = 101 }
                    });

            _notifier.Handle(_completed);

            ((TvNotificationData)_sent.Data).Episodes.Select(e => e.EpisodeNumber).Should().Equal(1, 2);
            _sent.Files.Select(f => f.Id).Should().Equal(10, 11);
        }

        [Test]
        public void should_ignore_history_rows_of_another_series()
        {
            _history.Setup(h => h.Find(DownloadId, EpisodeHistoryEventType.DownloadFolderImported))
                    .Returns(new List<EpisodeHistory>
                    {
                        new EpisodeHistory { ParentId = _series.Id, ItemId = 101 },
                        new EpisodeHistory { ParentId = _series.Id + 1, ItemId = 102 }
                    });

            _notifier.Handle(_completed);

            ((TvNotificationData)_sent.Data).Episodes.Select(e => e.Id).Should().Equal(101);
            _sent.Files.Select(f => f.Id).Should().Equal(10);
        }

        [Test]
        public void should_send_one_file_once_when_a_multi_episode_file_covers_two_episodes()
        {
            _episodes[1].EpisodeFileId = 10;

            _notifier.Handle(_completed);

            _sent.Files.Select(f => f.Id).Should().Equal(10);
            ((TvNotificationData)_sent.Data).Episodes.Should().HaveCount(2);
        }

        [Test]
        public void should_fall_back_to_the_file_quality_when_the_download_has_no_parsed_info()
        {
            _completed.TrackedDownload.RemoteRelease = null;

            _notifier.Handle(_completed);

            _sent.QualityName.Should().Be("WEBDL-1080p");
            _sent.ReleaseGroup.Should().BeNull();
        }

        [Test]
        public void should_do_nothing_for_a_download_of_another_media_type()
        {
            _completed.TrackedDownload.Kind = MediaKind.Movies;

            _notifier.Handle(_completed);

            _dispatcher.Verify(d => d.ImportComplete(It.IsAny<ImportCompleteMessage>()), Times.Never());
            _history.Verify(h => h.Find(It.IsAny<string>(), It.IsAny<EpisodeHistoryEventType>()), Times.Never());
        }

        [Test]
        public void should_do_nothing_when_no_provider_is_enabled_for_the_event()
        {
            _dispatcher.Setup(d => d.AnyEnabled(NotificationEvent.ImportComplete)).Returns(false);

            _notifier.Handle(_completed);

            _dispatcher.Verify(d => d.ImportComplete(It.IsAny<ImportCompleteMessage>()), Times.Never());
            _history.Verify(h => h.Find(It.IsAny<string>(), It.IsAny<EpisodeHistoryEventType>()), Times.Never());
        }

        [Test]
        public void should_do_nothing_without_a_download_id()
        {
            _completed.TrackedDownload.DownloadItem.DownloadId = null;

            _notifier.Handle(_completed);

            _dispatcher.Verify(d => d.ImportComplete(It.IsAny<ImportCompleteMessage>()), Times.Never());
        }

        [Test]
        public void should_do_nothing_when_history_holds_no_import_for_the_download()
        {
            _history.Setup(h => h.Find(DownloadId, EpisodeHistoryEventType.DownloadFolderImported)).Returns(new List<EpisodeHistory>());

            _notifier.Handle(_completed);

            _dispatcher.Verify(d => d.ImportComplete(It.IsAny<ImportCompleteMessage>()), Times.Never());
        }

        [Test]
        public void should_do_nothing_when_the_imported_episodes_have_no_file_left()
        {
            _episodes.ForEach(e => e.EpisodeFileId = 0);

            _notifier.Handle(_completed);

            _dispatcher.Verify(d => d.ImportComplete(It.IsAny<ImportCompleteMessage>()), Times.Never());
        }
    }
}
