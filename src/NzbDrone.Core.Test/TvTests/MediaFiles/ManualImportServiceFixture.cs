using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.CustomFormats;
using NzbDrone.Core.Tv.MediaFiles;
using NzbDrone.Core.Tv.MediaFiles.EpisodeImport;
using NzbDrone.Core.Tv.MediaFiles.EpisodeImport.Aggregation;
using NzbDrone.Core.Tv.MediaFiles.EpisodeImport.Manual;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.TvTests.MediaFiles
{
    [TestFixture]
    public class ManualImportServiceFixture : CoreTest<ManualImportService>
    {
        private readonly string _seriesPath = "c:\\tv\\Show".AsOsAgnostic();
        private readonly string _downloadFolder = "c:\\downloads\\Show.S01E01.1080p.WEB-DL-GRP".AsOsAgnostic();

        private Series _series;
        private Episode _episode1;
        private Episode _episode2;
        private Episode _episode3;
        private string _downloadFile;

        [SetUp]
        public void Setup()
        {
            _series = new Series { Id = 1, Title = "Show", Path = _seriesPath, OriginalLanguage = Language.English };
            _episode1 = new Episode { Id = 10, SeriesId = 1, SeasonNumber = 1, EpisodeNumber = 1 };
            _episode2 = new Episode { Id = 11, SeriesId = 1, SeasonNumber = 1, EpisodeNumber = 2 };
            _episode3 = new Episode { Id = 12, SeriesId = 1, SeasonNumber = 1, EpisodeNumber = 3 };
            _downloadFile = System.IO.Path.Combine(_downloadFolder, "Show.S01E01.1080p.WEB-DL-GRP.mkv");

            Mocker.GetMock<ISeriesService>().Setup(s => s.GetSeries(1)).Returns(_series);

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodes(It.IsAny<IEnumerable<int>>()))
                  .Returns<IEnumerable<int>>(ids => new[] { _episode1, _episode2, _episode3 }.Where(e => ids.Contains(e.Id)).ToList());

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodeBySeries(1))
                  .Returns(new List<Episode> { _episode1, _episode2, _episode3 });

            // A stand-in for the aggregators that guesses wrong, so the tests can tell whether the
            // values the user chose are applied after it.
            Mocker.GetMock<IAggregationService>()
                  .Setup(s => s.Augment(It.IsAny<LocalEpisode>(), It.IsAny<DownloadClientItem>()))
                  .Returns<LocalEpisode, DownloadClientItem>((local, item) =>
                  {
                      local.Quality = new QualityModel(Quality.SDTV);
                      local.ReleaseGroup = "AUGMENTED";
                      local.Languages = new List<Language> { Language.Unknown };
                      local.Series = null;
                      local.Episodes = new List<Episode>();

                      return local;
                  });

            Mocker.GetMock<IImportApprovedEpisodes>()
                  .Setup(s => s.Import(It.IsAny<List<ImportDecision>>(), It.IsAny<bool>(), It.IsAny<DownloadClientItem>(), It.IsAny<ImportMode>()))
                  .Returns<List<ImportDecision>, bool, DownloadClientItem, ImportMode>((decisions, newDownload, item, mode) =>
                      decisions.Select(d => new ImportResult(d, new EpisodeFile())).ToList());

            Mocker.GetMock<ILocalEpisodeCustomFormatCalculationService>()
                  .Setup(s => s.ParseEpisodeCustomFormats(It.IsAny<LocalEpisode>()))
                  .Returns(new List<CustomFormat>());

            Mocker.GetMock<ITvCustomFormatCalculationService>()
                  .Setup(s => s.ParseCustomFormat(It.IsAny<EpisodeFile>(), It.IsAny<Series>()))
                  .Returns(new List<CustomFormat>());
        }

        private TvManualImportCommand GivenCommand(ImportMode mode, params ManualImportFile[] files)
        {
            return new TvManualImportCommand { Files = files.ToList(), ImportMode = mode };
        }

        private ManualImportFile GivenFile(string path, string downloadId = null, params int[] episodeIds)
        {
            return new ManualImportFile
            {
                Path = path,
                SeriesId = 1,
                EpisodeIds = episodeIds.ToList(),
                Quality = new QualityModel(Quality.Bluray1080p),
                ReleaseGroup = "CHOSEN",
                Languages = new List<Language> { Language.English },
                IndexerFlags = (int)IndexerFlags.G_Freeleech,
                ReleaseType = ReleaseType.MultiEpisode,
                DownloadId = downloadId
            };
        }

        private TrackedDownload GivenTrackedDownload(string downloadId, Series series, params Episode[] grabbedEpisodes)
        {
            var tracked = new TrackedDownload
            {
                DownloadItem = new DownloadClientItem { DownloadId = downloadId, CanMoveFiles = true, OutputPath = new OsPath(_downloadFolder) },
                ImportItem = new DownloadClientItem { DownloadId = downloadId, CanMoveFiles = true, OutputPath = new OsPath(_downloadFolder) },
                RemoteRelease = new RemoteEpisode { Series = series, Episodes = grabbedEpisodes.ToList(), ParsedEpisodeInfo = new ParsedEpisodeInfo() },
                State = TrackedDownloadState.ImportBlocked
            };

            Mocker.GetMock<ITrackedDownloadService>().Setup(s => s.Find(downloadId)).Returns(tracked);

            return tracked;
        }

        private List<ImportDecision> CapturedDecisions(bool? newDownload = null, ImportMode? mode = null)
        {
            var captured = new List<ImportDecision>();

            Mocker.GetMock<IImportApprovedEpisodes>().Invocations
                  .Where(i => i.Method.Name == nameof(IImportApprovedEpisodes.Import))
                  .Where(i => newDownload == null || (bool)i.Arguments[1] == newDownload)
                  .Where(i => mode == null || (ImportMode)i.Arguments[3] == mode)
                  .ToList()
                  .ForEach(i => captured.AddRange((List<ImportDecision>)i.Arguments[0]));

            return captured;
        }

        [Test]
        public void should_import_with_the_values_the_user_chose_after_the_aggregators_have_run()
        {
            Subject.Execute(GivenCommand(ImportMode.Move, GivenFile(_downloadFile, null, 10, 11)));

            var local = CapturedDecisions().Should().ContainSingle().Which.LocalEpisode;

            local.Series.Should().BeSameAs(_series);
            local.Episodes.Select(e => e.Id).Should().Equal(10, 11);
            local.Quality.Quality.Should().Be(Quality.Bluray1080p);
            local.ReleaseGroup.Should().Be("CHOSEN");
            local.Languages.Should().Equal(Language.English);
            local.IndexerFlags.Should().Be(IndexerFlags.G_Freeleech);
            local.ReleaseType.Should().Be(ReleaseType.MultiEpisode);
        }

        [Test]
        public void should_keep_the_aggregated_languages_when_the_user_sent_none_that_are_valid()
        {
            var file = GivenFile(_downloadFile, null, 10);
            file.Languages = new List<Language>();

            Subject.Execute(GivenCommand(ImportMode.Auto, file));

            CapturedDecisions().Single().LocalEpisode.Languages.Should().Equal(Language.Unknown);
        }

        [Test]
        public void should_import_a_file_outside_the_series_folder_as_a_new_download_in_the_requested_mode()
        {
            Subject.Execute(GivenCommand(ImportMode.Copy, GivenFile(_downloadFile, null, 10)));

            Mocker.GetMock<IImportApprovedEpisodes>().Verify(
                s => s.Import(It.IsAny<List<ImportDecision>>(), true, null, ImportMode.Copy), Times.Once());
            CapturedDecisions().Single().LocalEpisode.ExistingFile.Should().BeFalse();
        }

        [Test]
        public void should_import_a_file_inside_the_series_folder_as_an_existing_file()
        {
            var inside = System.IO.Path.Combine(_seriesPath, "Season 1", "show.s01e01.mkv");

            Subject.Execute(GivenCommand(ImportMode.Auto, GivenFile(inside, null, 10)));

            Mocker.GetMock<IImportApprovedEpisodes>().Verify(
                s => s.Import(It.IsAny<List<ImportDecision>>(), false, null, ImportMode.Auto), Times.Once());
            CapturedDecisions().Single().LocalEpisode.ExistingFile.Should().BeTrue();
        }

        [Test]
        public void should_not_complete_any_download_for_an_untracked_import()
        {
            Subject.Execute(GivenCommand(ImportMode.Auto, GivenFile(_downloadFile, null, 10)));

            Mocker.GetMock<IEventAggregator>().Verify(e => e.PublishEvent(It.IsAny<DownloadCompletedEvent>()), Times.Never());
        }

        [Test]
        public void should_complete_a_tracked_download_when_every_grabbed_episode_was_imported()
        {
            var tracked = GivenTrackedDownload("abc", _series, _episode1, _episode2);

            Subject.Execute(GivenCommand(ImportMode.Move, GivenFile(_downloadFile, "abc", 10, 11)));

            tracked.State.Should().Be(TrackedDownloadState.Imported);
            Mocker.GetMock<IEventAggregator>().Verify(
                e => e.PublishEvent(It.Is<DownloadCompletedEvent>(c => c.TrackedDownload == tracked && c.ParentId == 1)), Times.Once());
        }

        [Test]
        public void should_import_a_tracked_download_as_a_new_download_with_its_client_item()
        {
            var tracked = GivenTrackedDownload("abc", _series, _episode1);

            Subject.Execute(GivenCommand(ImportMode.Move, GivenFile(_downloadFile, "abc", 10)));

            Mocker.GetMock<IImportApprovedEpisodes>().Verify(
                s => s.Import(It.IsAny<List<ImportDecision>>(), true, tracked.DownloadItem, ImportMode.Move), Times.Once());
        }

        [Test]
        public void should_not_complete_a_tracked_download_when_only_some_of_its_episodes_were_imported()
        {
            var tracked = GivenTrackedDownload("abc", _series, _episode1, _episode2, _episode3);

            Subject.Execute(GivenCommand(ImportMode.Move, GivenFile(_downloadFile, "abc", 10, 11)));

            tracked.State.Should().Be(TrackedDownloadState.ImportBlocked);
            Mocker.GetMock<IEventAggregator>().Verify(e => e.PublishEvent(It.IsAny<DownloadCompletedEvent>()), Times.Never());
        }

        [Test]
        public void should_not_complete_a_tracked_download_when_nothing_was_imported()
        {
            var tracked = GivenTrackedDownload("abc", _series, _episode1);

            Mocker.GetMock<IImportApprovedEpisodes>()
                  .Setup(s => s.Import(It.IsAny<List<ImportDecision>>(), It.IsAny<bool>(), It.IsAny<DownloadClientItem>(), It.IsAny<ImportMode>()))
                  .Returns<List<ImportDecision>, bool, DownloadClientItem, ImportMode>((decisions, newDownload, item, mode) =>
                      decisions.Select(d => new ImportResult(d, "File exists")).ToList());

            Subject.Execute(GivenCommand(ImportMode.Move, GivenFile(_downloadFile, "abc", 10)));

            tracked.State.Should().Be(TrackedDownloadState.ImportBlocked);
            Mocker.GetMock<IEventAggregator>().Verify(e => e.PublishEvent(It.IsAny<DownloadCompletedEvent>()), Times.Never());
        }

        [Test]
        public void should_complete_each_download_for_the_series_it_was_imported_into()
        {
            var other = new Series { Id = 2, Title = "Other", Path = "c:\\tv\\Other".AsOsAgnostic(), OriginalLanguage = Language.English };
            var otherEpisode = new Episode { Id = 20, SeriesId = 2, SeasonNumber = 1, EpisodeNumber = 1 };
            var first = GivenTrackedDownload("abc", _series, _episode1);
            var second = GivenTrackedDownload("def", other, otherEpisode);

            Mocker.GetMock<ISeriesService>().Setup(s => s.GetSeries(2)).Returns(other);
            Mocker.GetMock<IEpisodeService>().Setup(s => s.GetEpisodes(It.Is<IEnumerable<int>>(ids => ids.Contains(20)))).Returns(new List<Episode> { otherEpisode });

            var secondFile = GivenFile(System.IO.Path.Combine(_downloadFolder, "other.s01e01.mkv"), "def", 20);
            secondFile.SeriesId = 2;

            Subject.Execute(GivenCommand(ImportMode.Move, GivenFile(_downloadFile, "abc", 10), secondFile));

            Mocker.GetMock<IEventAggregator>().Verify(
                e => e.PublishEvent(It.Is<DownloadCompletedEvent>(c => c.TrackedDownload == first && c.ParentId == 1)), Times.Once());
            Mocker.GetMock<IEventAggregator>().Verify(
                e => e.PublishEvent(It.Is<DownloadCompletedEvent>(c => c.TrackedDownload == second && c.ParentId == 2)), Times.Once());
        }

        [Test]
        public void should_delete_the_download_folder_when_it_is_done_and_the_client_lets_us_move_files()
        {
            GivenTrackedDownload("abc", _series, _episode1);

            Mocker.GetMock<IDiskProvider>().Setup(d => d.FolderExists(_downloadFolder)).Returns(true);
            Mocker.GetMock<IDownloadedEpisodesImportService>().Setup(s => s.ShouldDeleteFolder(It.IsAny<System.IO.DirectoryInfo>(), _series)).Returns(true);

            Subject.Execute(GivenCommand(ImportMode.Move, GivenFile(_downloadFile, "abc", 10)));

            Mocker.GetMock<IDiskProvider>().Verify(d => d.DeleteFolder(_downloadFolder, true), Times.Once());
        }

        [Test]
        public void should_keep_the_download_folder_when_the_client_does_not_let_us_move_files()
        {
            var tracked = GivenTrackedDownload("abc", _series, _episode1);
            tracked.DownloadItem.CanMoveFiles = false;

            Mocker.GetMock<IDiskProvider>().Setup(d => d.FolderExists(_downloadFolder)).Returns(true);
            Mocker.GetMock<IDownloadedEpisodesImportService>().Setup(s => s.ShouldDeleteFolder(It.IsAny<System.IO.DirectoryInfo>(), _series)).Returns(true);

            Subject.Execute(GivenCommand(ImportMode.Move, GivenFile(_downloadFile, "abc", 10)));

            Mocker.GetMock<IDiskProvider>().Verify(d => d.DeleteFolder(It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void should_keep_the_download_folder_when_files_worth_keeping_are_left_in_it()
        {
            GivenTrackedDownload("abc", _series, _episode1);

            Mocker.GetMock<IDiskProvider>().Setup(d => d.FolderExists(_downloadFolder)).Returns(true);
            Mocker.GetMock<IDownloadedEpisodesImportService>().Setup(s => s.ShouldDeleteFolder(It.IsAny<System.IO.DirectoryInfo>(), _series)).Returns(false);

            Subject.Execute(GivenCommand(ImportMode.Move, GivenFile(_downloadFile, "abc", 10)));

            Mocker.GetMock<IDiskProvider>().Verify(d => d.DeleteFolder(It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void should_reject_a_season_whose_episodes_were_not_selected_when_reprocessing()
        {
            var item = Subject.ReprocessItem(_downloadFile, null, 1, 1, new List<int>(), null, new QualityModel(Quality.Unknown), new List<Language> { Language.Unknown }, 0, ReleaseType.Unknown);

            item.Rejections.Should().ContainSingle(r => r.Reason == ImportRejectionReason.NoEpisodes && r.Message == "Episodes not selected");
            item.Series.Should().BeSameAs(_series);
            Mocker.GetMock<IMakeImportDecision>().Verify(m => m.GetDecision(It.IsAny<LocalEpisode>(), It.IsAny<DownloadClientItem>()), Times.Never());
        }

        [Test]
        public void should_decide_on_the_chosen_episodes_and_fill_unknowns_from_the_file_name_when_reprocessing()
        {
            LocalEpisode decided = null;

            Mocker.GetMock<IMakeImportDecision>()
                  .Setup(m => m.GetDecision(It.IsAny<LocalEpisode>(), It.IsAny<DownloadClientItem>()))
                  .Callback<LocalEpisode, DownloadClientItem>((local, item) => decided = local)
                  .Returns<LocalEpisode, DownloadClientItem>((local, item) => new ImportDecision(local, new ImportRejection(ImportRejectionReason.NotQualityUpgrade, "Not an upgrade")));

            var result = Subject.ReprocessItem(_downloadFile, null, 1, 1, new List<int> { 10 }, null, new QualityModel(Quality.Unknown), new List<Language> { Language.Unknown }, 0, ReleaseType.SingleEpisode);

            decided.Episodes.Select(e => e.Id).Should().Equal(10);
            decided.Quality.Quality.Should().Be(Quality.WEBDL1080p);
            decided.ReleaseGroup.Should().Be("GRP");
            result.Episodes.Select(e => e.Id).Should().Equal(10);
            result.SeasonNumber.Should().Be(1);
            result.Rejections.Should().ContainSingle(r => r.Message == "Not an upgrade");
        }

        [Test]
        public void should_reprocess_a_file_whose_name_cannot_be_parsed_with_empty_parse_info_instead_of_failing()
        {
            var unparseable = "c:\\downloads\\video.mkv".AsOsAgnostic();
            LocalEpisode augmented = null;

            Mocker.GetMock<IAggregationService>()
                  .Setup(s => s.Augment(It.IsAny<LocalEpisode>(), It.IsAny<DownloadClientItem>()))
                  .Callback<LocalEpisode, DownloadClientItem>((local, item) => augmented = local)
                  .Returns<LocalEpisode, DownloadClientItem>((local, item) => local);

            Mocker.GetMock<IMakeImportDecision>()
                  .Setup(m => m.GetDecision(It.IsAny<LocalEpisode>(), It.IsAny<DownloadClientItem>()))
                  .Returns<LocalEpisode, DownloadClientItem>((local, item) => new ImportDecision(local));

            var result = Subject.ReprocessItem(unparseable, null, 1, 1, new List<int> { 10 }, "CHOSEN", new QualityModel(Quality.Bluray1080p), new List<Language> { Language.English }, 0, ReleaseType.SingleEpisode);

            augmented.FileEpisodeInfo.Should().NotBeNull();
            augmented.FileEpisodeInfo.EpisodeNumbers.Should().BeEmpty();
            result.Episodes.Select(e => e.Id).Should().Equal(10);
            result.Rejections.Should().BeEmpty();
        }

        [Test]
        public void should_use_the_parse_made_at_grab_time_for_a_tracked_download_the_client_renamed()
        {
            var tracked = GivenTrackedDownload("hash", _series, _episode1);
            tracked.DownloadItem.Title = "Sintel";
            var grabParse = new ParsedEpisodeInfo { SeriesTitle = "Show", EpisodeNumbers = new[] { 1 } };
            tracked.RemoteRelease = new RemoteEpisode { Series = _series, Episodes = new List<Episode> { _episode1 }, ParsedEpisodeInfo = grabParse };
            LocalEpisode augmented = null;

            Mocker.GetMock<IAggregationService>()
                  .Setup(s => s.Augment(It.IsAny<LocalEpisode>(), It.IsAny<DownloadClientItem>()))
                  .Callback<LocalEpisode, DownloadClientItem>((local, item) => augmented = local)
                  .Returns<LocalEpisode, DownloadClientItem>((local, item) => local);

            Mocker.GetMock<IMakeImportDecision>()
                  .Setup(m => m.GetDecision(It.IsAny<LocalEpisode>(), It.IsAny<DownloadClientItem>()))
                  .Returns<LocalEpisode, DownloadClientItem>((local, item) => new ImportDecision(local));

            Subject.ReprocessItem(_downloadFile, "hash", 1, 1, new List<int> { 10 }, null, new QualityModel(Quality.Unknown), new List<Language> { Language.Unknown }, 0, ReleaseType.SingleEpisode);

            augmented.DownloadClientEpisodeInfo.Should().BeSameAs(grabParse);
        }

        [Test]
        public void should_return_no_items_for_a_download_the_client_no_longer_reports()
        {
            Mocker.GetMock<ITrackedDownloadService>().Setup(s => s.Find("gone")).Returns((TrackedDownload)null);

            Subject.GetMediaFiles(null, "gone", null, true).Should().BeEmpty();
        }

        [Test]
        public void should_return_no_items_when_the_path_does_not_exist()
        {
            Subject.GetMediaFiles("c:\\nowhere".AsOsAgnostic(), null, null, true).Should().BeEmpty();
        }

        [Test]
        public void should_reject_a_file_whose_series_cannot_be_found()
        {
            Mocker.GetMock<IDiskProvider>().Setup(d => d.FileExists(_downloadFile)).Returns(true);

            var items = Subject.GetMediaFiles(_downloadFile, null, null, true);

            var item = items.Should().ContainSingle().Which;
            item.Series.Should().BeNull();
            item.Rejections.Should().ContainSingle(r => r.Reason == ImportRejectionReason.UnknownSeries);
        }

        [Test]
        public void should_list_the_files_of_a_series_with_the_episodes_each_file_holds()
        {
            var file = new EpisodeFile { Id = 5, SeriesId = 1, SeasonNumber = 1, RelativePath = "Season 1\\show.s01e01.mkv".AsOsAgnostic(), ReleaseGroup = "GRP", Quality = new QualityModel(Quality.HDTV720p), Languages = new List<Language> { Language.English } };
            _episode1.EpisodeFileId = 5;
            _episode2.EpisodeFileId = 5;

            Mocker.GetMock<IMediaFileService>().Setup(s => s.GetFilesBySeries(1)).Returns(new List<EpisodeFile> { file });

            var item = Subject.GetMediaFiles(1, null).Should().ContainSingle().Which;

            item.EpisodeFileId.Should().Be(5);
            item.Episodes.Select(e => e.Id).Should().Equal(10, 11);
            item.SeasonNumber.Should().Be(1);
            item.Series.Should().BeSameAs(_series);
            item.Path.Should().Be(System.IO.Path.Combine(_seriesPath, file.RelativePath));
            item.Quality.Quality.Should().Be(Quality.HDTV720p);
            item.Rejections.Should().BeEmpty();
        }

        [Test]
        public void should_list_only_the_files_of_the_season_asked_for()
        {
            var file = new EpisodeFile { Id = 6, SeriesId = 1, SeasonNumber = 2, RelativePath = "Season 2\\show.s02e01.mkv".AsOsAgnostic(), Quality = new QualityModel(), Languages = new List<Language>() };

            Mocker.GetMock<IMediaFileService>().Setup(s => s.GetFilesBySeason(1, 2)).Returns(new List<EpisodeFile> { file });

            Subject.GetMediaFiles(1, 2).Should().ContainSingle().Which.EpisodeFileId.Should().Be(6);

            Mocker.GetMock<IMediaFileService>().Verify(s => s.GetFilesBySeries(It.IsAny<int>()), Times.Never());
        }
    }
}
