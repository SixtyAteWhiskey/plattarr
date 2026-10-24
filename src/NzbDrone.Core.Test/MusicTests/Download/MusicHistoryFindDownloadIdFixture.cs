using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.History;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.MediaFiles.Events;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.Download
{
    [TestFixture]
    public class MusicHistoryFindDownloadIdFixture : CoreTest<HistoryService>
    {
        private List<AlbumHistory> _history;

        [SetUp]
        public void Setup()
        {
            _history = new List<AlbumHistory>();

            Mocker.GetMock<IAlbumHistoryRepository>()
                .Setup(r => r.FindDownloadHistory(It.IsAny<int>(), It.IsAny<AudioQualityModel>()))
                .Returns(() => _history);
        }

        private static AlbumHistory Row(int albumId, AlbumHistoryEventType type, string downloadId)
        {
            return new AlbumHistory { ItemId = albumId, ParentId = 1, EventType = type, DownloadId = downloadId };
        }

        private static TrackImportedEvent Imported(params int[] albumIds)
        {
            var local = new LocalTrack
            {
                Artist = new Artist { Id = 1 },
                Tracks = albumIds.Select(a => new Track { AlbumRelease = new LazyLoaded<AlbumRelease>(new AlbumRelease { Album = new LazyLoaded<Album>(new Album { Id = a }) }) }).ToList()
            };

            return new TrackImportedEvent(local, new TrackFile { Path = "/music/a.flac", Quality = new AudioQualityModel() }, new List<TrackFile>(), true, null);
        }

        [Test]
        public void should_return_null_when_there_is_no_history()
        {
            Subject.FindDownloadId(Imported(10)).Should().BeNull();
        }

        [Test]
        public void should_return_the_download_id_of_the_single_open_grab()
        {
            _history.Add(Row(10, AlbumHistoryEventType.Grabbed, "abc"));

            Subject.FindDownloadId(Imported(10)).Should().Be("abc");
        }

        [Test]
        public void should_return_null_when_the_grab_has_already_been_processed()
        {
            _history.Add(Row(10, AlbumHistoryEventType.Grabbed, "abc"));
            _history.Add(Row(10, AlbumHistoryEventType.DownloadImported, "abc"));

            Subject.FindDownloadId(Imported(10)).Should().BeNull();
        }

        [Test]
        public void should_return_null_when_the_grab_has_already_failed()
        {
            _history.Add(Row(10, AlbumHistoryEventType.Grabbed, "abc"));
            _history.Add(Row(10, AlbumHistoryEventType.DownloadFailed, "abc"));

            Subject.FindDownloadId(Imported(10)).Should().BeNull();
        }

        [Test]
        public void should_return_null_when_the_album_has_two_open_grabs()
        {
            _history.Add(Row(10, AlbumHistoryEventType.Grabbed, "abc"));
            _history.Add(Row(10, AlbumHistoryEventType.Grabbed, "def"));

            Subject.FindDownloadId(Imported(10)).Should().BeNull();
        }

        [Test]
        public void should_return_the_download_id_when_a_multi_album_file_matches_one_grab()
        {
            _history.Add(Row(10, AlbumHistoryEventType.Grabbed, "abc"));
            _history.Add(Row(11, AlbumHistoryEventType.Grabbed, "abc"));

            Subject.FindDownloadId(Imported(10, 11)).Should().Be("abc");
        }

        [Test]
        public void should_return_null_when_the_albums_were_grabbed_by_different_downloads()
        {
            _history.Add(Row(10, AlbumHistoryEventType.Grabbed, "abc"));
            _history.Add(Row(11, AlbumHistoryEventType.Grabbed, "def"));

            Subject.FindDownloadId(Imported(10, 11)).Should().BeNull();
        }

        [Test]
        public void should_return_null_when_one_of_the_albums_has_no_open_grab()
        {
            _history.Add(Row(10, AlbumHistoryEventType.Grabbed, "abc"));

            Subject.FindDownloadId(Imported(10, 11)).Should().BeNull();
        }

        [Test]
        public void should_ignore_grabs_of_other_albums()
        {
            _history.Add(Row(99, AlbumHistoryEventType.Grabbed, "other"));

            Subject.FindDownloadId(Imported(10)).Should().BeNull();
        }
    }
}
