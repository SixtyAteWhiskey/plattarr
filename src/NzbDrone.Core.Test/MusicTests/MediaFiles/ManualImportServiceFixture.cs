using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.CustomFormats;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.MediaFiles.TrackImport;
using NzbDrone.Core.Music.MediaFiles.TrackImport.Manual;
using NzbDrone.Core.Music.Parser;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.RootFolders;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.MediaFiles
{
    [TestFixture]
    public class ManualImportServiceFixture : CoreTest<ManualImportService>
    {
        private string _root;
        private string _artistPath;
        private string _downloadFolder;
        private string _file;
        private Artist _artist;
        private Album _album;
        private AlbumRelease _release;
        private Track _track;
        private ImportDecisionMakerConfig _config;
        private IdentificationOverrides _overrides;

        [SetUp]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "plattarr-manual-" + System.Guid.NewGuid().ToString("N"));
            _artistPath = Path.Combine(_root, "library", "The Black Keys");
            _downloadFolder = Path.Combine(_root, "downloads", "The Black Keys - Brothers [FLAC]");
            Directory.CreateDirectory(_artistPath);
            Directory.CreateDirectory(_downloadFolder);
            _file = Path.Combine(_downloadFolder, "01 Everlasting Light.flac");
            File.WriteAllText(_file, "audio");

            _artist = new Artist { Id = 12, Path = _artistPath, Metadata = new ArtistMetadata { Name = "The Black Keys" } };
            _album = new Album { Id = 101, ArtistMetadataId = 1, Title = "Brothers", AnyReleaseOk = true };
            _release = new AlbumRelease { Id = 201, AlbumId = 101, Monitored = true, TrackCount = 1 };
            _track = new Track { Id = 301, AlbumReleaseId = 201, Title = "Everlasting Light" };
            _config = null;
            _overrides = null;

            Mocker.GetMock<IDiskProvider>().Setup(d => d.GetFileInfo(It.IsAny<string>())).Returns<string>(p => new FileInfo(p));
            Mocker.GetMock<IDiskProvider>().Setup(d => d.GetFileSize(It.IsAny<string>())).Returns(5);
            Mocker.GetMock<IDiskProvider>().Setup(d => d.FileExists(_file)).Returns(true);
            Mocker.GetMock<IDiskProvider>().Setup(d => d.FolderExists(_downloadFolder)).Returns(true);

            Mocker.GetMock<IMusicCustomFormatCalculationService>().Setup(c => c.ParseCustomFormat(It.IsAny<LocalTrack>())).Returns(new List<CustomFormat>());
            Mocker.GetMock<IAlbumService>().Setup(s => s.GetAlbum(101)).Returns(_album);
            Mocker.GetMock<IArtistService>().Setup(s => s.GetArtist(12)).Returns(_artist);
            Mocker.GetMock<IReleaseService>().Setup(s => s.GetRelease(201)).Returns(_release);
            Mocker.GetMock<ITrackService>().Setup(s => s.GetTracks(It.IsAny<IEnumerable<int>>())).Returns(new List<Track> { _track });
            Mocker.GetMock<IAudioTagService>().Setup(s => s.ReadTags(It.IsAny<string>())).Returns(new ParsedTrackInfo());
            Mocker.GetMock<IRootFolderService>().Setup(s => s.GetBestRootFolder(It.IsAny<string>())).Returns(new RootFolder { Path = _root });

            Mocker.GetMock<IImportApprovedTracks>()
                  .Setup(s => s.Import(It.IsAny<List<ImportDecision<LocalTrack>>>(), It.IsAny<bool>(), It.IsAny<DownloadClientItem>(), It.IsAny<ImportMode>()))
                  .Returns<List<ImportDecision<LocalTrack>>, bool, DownloadClientItem, ImportMode>((decisions, replace, item, mode) =>
                      decisions.Select(d => d.Approved ? new ImportResult(d) : new ImportResult(d, "rejected")).ToList());
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        private ImportDecision<LocalTrack> NewDecision(string path, bool withArtist = true, params ImportRejection[] rejections)
        {
            var local = new LocalTrack
            {
                Path = path,
                Size = 5,
                Quality = new AudioQualityModel(AudioQuality.FLAC),
                ReleaseGroup = "GRP",
                FileTrackInfo = new ParsedTrackInfo { Title = "Everlasting Light" }
            };

            if (withArtist)
            {
                local.Artist = _artist;
                local.Album = _album;
                local.Release = _release;
                local.Tracks = new List<Track> { _track };
            }

            return new ImportDecision<LocalTrack>(local, rejections);
        }

        private void GivenDecisions(params ImportDecision<LocalTrack>[] decisions)
        {
            Mocker.GetMock<IMakeImportDecision>()
                  .Setup(m => m.GetImportDecisions(It.IsAny<List<FileInfo>>(), It.IsAny<IdentificationOverrides>(), It.IsAny<ImportDecisionMakerInfo>(), It.IsAny<ImportDecisionMakerConfig>()))
                  .Callback<List<FileInfo>, IdentificationOverrides, ImportDecisionMakerInfo, ImportDecisionMakerConfig>((files, overrides, info, config) =>
                  {
                      _overrides = overrides;
                      _config = config;
                  })
                  .Returns(decisions.ToList());
        }

        [Test]
        public void should_list_nothing_for_a_download_that_is_not_tracked()
        {
            Mocker.GetMock<ITrackedDownloadService>().Setup(s => s.Find("gone")).Returns((TrackedDownload)null);

            Subject.GetMediaFiles(null, "gone", null, FilterFilesType.Matched, true).Should().BeEmpty();
        }

        [Test]
        public void should_list_nothing_for_a_path_that_does_not_exist()
        {
            Subject.GetMediaFiles(Path.Combine(_root, "missing"), null, null, FilterFilesType.Matched, true).Should().BeEmpty();
        }

        [Test]
        public void should_list_a_single_file_with_the_decision_that_was_made_for_it()
        {
            GivenDecisions(NewDecision(_file));

            var item = Subject.GetMediaFiles(_file, null, null, FilterFilesType.Matched, true).Should().ContainSingle().Subject;

            item.Path.Should().Be(_file);
            item.Name.Should().Be("01 Everlasting Light");
            item.Artist.Should().BeSameAs(_artist);
            item.Album.Should().BeSameAs(_album);
            item.Tracks.Should().ContainSingle();
            item.Quality.Quality.Should().Be(AudioQuality.FLAC);
            item.Size.Should().Be(5);
            _config.IncludeExisting.Should().BeFalse();
            _config.NewDownload.Should().BeTrue();
        }

        [Test]
        public void should_reject_a_single_file_that_gets_no_decision()
        {
            GivenDecisions();

            var item = Subject.GetMediaFiles(_file, null, null, FilterFilesType.Matched, false).Should().ContainSingle().Subject;

            item.Rejections.Should().ContainSingle(r => r.Reason == ImportRejectionReason.UnableToParse);
            item.Id.Should().NotBe(0);
        }

        [Test]
        public void should_include_existing_files_only_when_existing_files_are_not_replaced()
        {
            GivenDecisions(NewDecision(_file));

            Subject.GetMediaFiles(_file, null, null, FilterFilesType.Matched, false);

            _config.IncludeExisting.Should().BeTrue();
        }

        [Test]
        public void should_list_the_files_of_a_folder_using_the_artist_found_from_the_folder_name()
        {
            Mocker.GetMock<IParsingService>().Setup(s => s.GetArtist("The Black Keys - Brothers [FLAC]")).Returns(_artist);
            Mocker.GetMock<IDiskScanService>().Setup(s => s.GetAudioFiles(_downloadFolder, true)).Returns(new[] { new FileInfo(_file) });
            GivenDecisions(NewDecision(_file));

            var items = Subject.GetMediaFiles(_downloadFolder, null, null, FilterFilesType.Known, true);

            items.Should().ContainSingle().Which.Artist.Should().BeSameAs(_artist);
            _overrides.Artist.Should().BeSameAs(_artist);
            _config.Filter.Should().Be(FilterFilesType.Known);
        }

        [Test]
        public void should_not_parse_a_folder_of_over_a_hundred_files_when_the_artist_is_unknown()
        {
            var files = Enumerable.Range(0, 101).Select(i =>
            {
                var path = Path.Combine(_downloadFolder, $"{i}.flac");
                File.WriteAllText(path, "audio");

                return new FileInfo(path);
            }).ToArray();
            Mocker.GetMock<IDiskScanService>().Setup(s => s.GetAudioFiles(_downloadFolder, true)).Returns(files);

            var items = Subject.GetMediaFiles(_downloadFolder, null, null, FilterFilesType.Matched, true);

            items.Should().HaveCount(101);
            items.Should().OnlyContain(i => i.Quality.Quality == AudioQuality.Unknown && i.Artist == null);
            Mocker.GetMock<IMakeImportDecision>().Verify(m => m.GetImportDecisions(It.IsAny<List<FileInfo>>(), It.IsAny<IdentificationOverrides>(), It.IsAny<ImportDecisionMakerInfo>(), It.IsAny<ImportDecisionMakerConfig>()), Times.Never());
        }

        [Test]
        public void should_take_the_artist_of_a_tracked_download_when_the_folder_name_does_not_match()
        {
            var tracked = new TrackedDownload
            {
                DownloadItem = new DownloadClientItem { DownloadId = "dl-1", OutputPath = new OsPath(_downloadFolder) },
                ImportItem = new DownloadClientItem { DownloadId = "dl-1", OutputPath = new OsPath(_downloadFolder) },
                RemoteRelease = new RemoteAlbum { Artist = _artist }
            };

            Mocker.GetMock<ITrackedDownloadService>().Setup(s => s.Find("dl-1")).Returns(tracked);
            Mocker.GetMock<IDiskScanService>().Setup(s => s.GetAudioFiles(_downloadFolder, true)).Returns(new[] { new FileInfo(_file) });
            GivenDecisions(NewDecision(_file));

            var items = Subject.GetMediaFiles(null, "dl-1", null, FilterFilesType.Matched, true);

            items.Should().ContainSingle().Which.DownloadId.Should().Be("dl-1");
            _overrides.Artist.Should().BeSameAs(_artist);
        }

        [Test]
        public void should_apply_the_decision_when_updating_items_and_replace_an_unknown_quality()
        {
            GivenDecisions(NewDecision(_file));

            var item = new ManualImportItem
            {
                Id = 7,
                Path = _file,
                Artist = _artist,
                Album = _album,
                Quality = new AudioQualityModel(AudioQuality.Unknown),
                ReplaceExistingFiles = true
            };

            var result = Subject.UpdateItems(new List<ManualImportItem> { item }).Should().ContainSingle().Subject;

            result.Id.Should().Be(7);
            result.Quality.Quality.Should().Be(AudioQuality.FLAC);
            result.ReleaseGroup.Should().Be("GRP");
            result.Tracks.Should().ContainSingle();
            result.Size.Should().Be(5);
            _overrides.Artist.Should().BeSameAs(_artist);
            _overrides.Album.Should().BeSameAs(_album);
            _config.SingleRelease.Should().BeTrue();
            _config.IncludeExisting.Should().BeFalse();
        }

        [Test]
        public void should_keep_a_quality_the_user_chose_when_updating_items()
        {
            GivenDecisions(NewDecision(_file));

            var item = new ManualImportItem { Id = 7, Path = _file, Artist = _artist, Album = _album, Quality = new AudioQualityModel(AudioQuality.MP3_320), ReleaseGroup = "MINE" };

            var result = Subject.UpdateItems(new List<ManualImportItem> { item }).Single();

            result.Quality.Quality.Should().Be(AudioQuality.MP3_320);
            result.ReleaseGroup.Should().Be("MINE");
        }

        private MusicManualImportCommand NewCommand(string downloadId = null, bool disableReleaseSwitching = false)
        {
            return new MusicManualImportCommand
            {
                ImportMode = ImportMode.Move,
                ReplaceExistingFiles = true,
                Files = new List<ManualImportFile>
                {
                    new ManualImportFile
                    {
                        Path = _file,
                        ArtistId = 12,
                        AlbumId = 101,
                        AlbumReleaseId = 201,
                        TrackIds = new List<int> { 301 },
                        Quality = new AudioQualityModel(AudioQuality.FLAC),
                        ReleaseGroup = "GRP",
                        DownloadId = downloadId,
                        DisableReleaseSwitching = disableReleaseSwitching
                    }
                }
            };
        }

        [Test]
        public void should_import_the_files_chosen_by_the_user()
        {
            Subject.Execute(NewCommand());

            Mocker.GetMock<IImportApprovedTracks>().Verify(
                s => s.Import(
                    It.Is<List<ImportDecision<LocalTrack>>>(d => d.Count == 1 && d[0].Approved && d[0].Item.Artist == _artist && d[0].Item.Tracks.Single() == _track && d[0].Item.Quality.Quality == AudioQuality.FLAC && d[0].Item.ReleaseGroup == "GRP"),
                    true,
                    null,
                    ImportMode.Move),
                Times.Once());
        }

        [Test]
        public void should_reject_a_file_whose_artist_folder_is_not_in_a_root_folder()
        {
            Mocker.GetMock<IRootFolderService>().Setup(s => s.GetBestRootFolder(_artistPath)).Returns((RootFolder)null);

            Subject.Execute(NewCommand());

            Mocker.GetMock<IImportApprovedTracks>().Verify(
                s => s.Import(
                    It.Is<List<ImportDecision<LocalTrack>>>(d => d.Single().Rejections.Single().Reason == ImportRejectionReason.ArtistPathNotInRootFolder),
                    It.IsAny<bool>(),
                    It.IsAny<DownloadClientItem>(),
                    It.IsAny<ImportMode>()),
                Times.Once());
        }

        [Test]
        public void should_turn_off_release_switching_for_an_album_when_asked()
        {
            Subject.Execute(NewCommand(disableReleaseSwitching: true));

            _album.AnyReleaseOk.Should().BeFalse();
            Mocker.GetMock<IAlbumService>().Verify(s => s.UpdateAlbum(_album), Times.Once());
        }

        [Test]
        public void should_leave_release_switching_alone_otherwise()
        {
            Subject.Execute(NewCommand());

            _album.AnyReleaseOk.Should().BeTrue();
            Mocker.GetMock<IAlbumService>().Verify(s => s.UpdateAlbum(It.IsAny<Album>()), Times.Never());
        }

        private TrackedDownload GivenTrackedDownload()
        {
            var tracked = new TrackedDownload
            {
                DownloadItem = new DownloadClientItem { DownloadId = "dl-1", OutputPath = new OsPath(_downloadFolder), CanMoveFiles = true },
                ImportItem = new DownloadClientItem { DownloadId = "dl-1", OutputPath = new OsPath(_downloadFolder), CanMoveFiles = true },
                RemoteRelease = new RemoteAlbum { Artist = _artist, Albums = new List<Album> { new Album { AlbumReleases = new NzbDrone.Core.Datastore.LazyLoaded<List<AlbumRelease>>(new List<AlbumRelease> { _release }) } } },
                State = TrackedDownloadState.ImportPending
            };

            Mocker.GetMock<ITrackedDownloadService>().Setup(s => s.Find("dl-1")).Returns(tracked);

            return tracked;
        }

        [Test]
        public void should_complete_a_tracked_download_once_all_its_tracks_are_imported()
        {
            var tracked = GivenTrackedDownload();

            Subject.Execute(NewCommand("dl-1"));

            tracked.State.Should().Be(TrackedDownloadState.Imported);
            Mocker.GetMock<IEventAggregator>().Verify(e => e.PublishEvent(It.Is<DownloadCompletedEvent>(c => c.TrackedDownload == tracked && c.ParentId == 12)), Times.Once());
        }

        [Test]
        public void should_not_complete_a_tracked_download_when_the_file_was_not_imported()
        {
            var tracked = GivenTrackedDownload();
            Mocker.GetMock<IRootFolderService>().Setup(s => s.GetBestRootFolder(_artistPath)).Returns((RootFolder)null);

            Subject.Execute(NewCommand("dl-1"));

            tracked.State.Should().Be(TrackedDownloadState.ImportPending);
            Mocker.GetMock<IEventAggregator>().Verify(e => e.PublishEvent(It.IsAny<DownloadCompletedEvent>()), Times.Never());
        }

        [Test]
        public void should_import_as_plain_files_when_the_download_has_left_the_queue()
        {
            Mocker.GetMock<ITrackedDownloadService>().Setup(s => s.Find("dl-1")).Returns((TrackedDownload)null);

            Subject.Execute(NewCommand("dl-1"));

            Mocker.GetMock<IImportApprovedTracks>().Verify(s => s.Import(It.IsAny<List<ImportDecision<LocalTrack>>>(), true, null, ImportMode.Move), Times.Once());
            Mocker.GetMock<IEventAggregator>().Verify(e => e.PublishEvent(It.IsAny<DownloadCompletedEvent>()), Times.Never());
        }
    }
}
