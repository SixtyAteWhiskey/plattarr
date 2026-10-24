using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Downloads;
using NzbDrone.Core.Music.Events;
using NzbDrone.Core.Music.History;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.MediaFiles.Events;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.Download
{
    [TestFixture]
    public class MusicDownloadHistoryHandlerFixture : CoreTest<MusicDownloadHistoryHandler>
    {
        private DownloadHistory _inserted;
        private DownloadClientItem _item;

        [SetUp]
        public void Setup()
        {
            _inserted = null;

            _item = new DownloadClientItem
            {
                Title = "Artist - Album [FLAC]",
                DownloadId = "HASH",
                DownloadClientInfo = new DownloadClientItemClientInfo { Id = 3, Name = "qb-main", Type = "QBittorrent", Protocol = DownloadProtocol.Torrent }
            };

            Mocker.GetMock<IDownloadHistoryRepository>()
                .Setup(r => r.Insert(It.IsAny<DownloadHistory>()))
                .Callback<DownloadHistory>(h => _inserted = h)
                .Returns<DownloadHistory>(h => h);
        }

        private static TrackImportedEvent Imported(bool newDownload, DownloadClientItem item)
        {
            var artist = new Artist { Id = 9 };

            return new TrackImportedEvent(
                new LocalTrack { Path = "/downloads/a.flac", Artist = artist },
                new TrackFile { Path = "/music/Artist/a.flac", Artist = artist },
                new List<TrackFile>(),
                newDownload,
                item);
        }

        [Test]
        public void should_record_a_file_imported_by_a_new_download_as_music()
        {
            Subject.Handle(Imported(true, _item));

            _inserted.MediaKind.Should().Be(MediaKind.Music);
            _inserted.ParentId.Should().Be(9);
            _inserted.EventType.Should().Be(DownloadHistoryEventType.FileImported);
            _inserted.DownloadId.Should().Be("HASH");
            _inserted.DownloadClientId.Should().Be(3);
            _inserted.Protocol.Should().Be(DownloadProtocol.Torrent);
            _inserted.SourceTitle.Should().Be("/downloads/a.flac");
            _inserted.Data["DownloadClient"].Should().Be("QBittorrent");
            _inserted.Data["DownloadClientName"].Should().Be("qb-main");
            _inserted.Data["SourcePath"].Should().Be("/downloads/a.flac");
            _inserted.Data["DestinationPath"].Should().Be("/music/Artist/a.flac");
        }

        [Test]
        public void should_not_record_a_file_that_was_not_a_new_download()
        {
            Subject.Handle(Imported(false, _item));

            _inserted.Should().BeNull();
        }

        [Test]
        public void should_find_the_download_id_in_history_when_the_import_has_none()
        {
            _item.DownloadId = null;

            Mocker.GetMock<IHistoryService>()
                .Setup(h => h.FindDownloadId(It.IsAny<TrackImportedEvent>()))
                .Returns("FROM-HISTORY");

            Subject.Handle(Imported(true, _item));

            _inserted.DownloadId.Should().Be("FROM-HISTORY");
        }

        [Test]
        public void should_record_a_manual_import_that_has_no_download_client_item()
        {
            Mocker.GetMock<IHistoryService>()
                .Setup(h => h.FindDownloadId(It.IsAny<TrackImportedEvent>()))
                .Returns("FROM-HISTORY");

            Subject.Handle(Imported(true, null));

            _inserted.DownloadId.Should().Be("FROM-HISTORY");
            _inserted.DownloadClientId.Should().Be(0);
            _inserted.Data.Should().NotContainKey("DownloadClient");
            _inserted.Data["DestinationPath"].Should().Be("/music/Artist/a.flac");
        }

        [Test]
        public void should_not_record_a_file_with_no_download_id_anywhere()
        {
            _item.DownloadId = null;

            Mocker.GetMock<IHistoryService>()
                .Setup(h => h.FindDownloadId(It.IsAny<TrackImportedEvent>()))
                .Returns((string)null);

            Subject.Handle(Imported(true, _item));

            _inserted.Should().BeNull();
        }

        [Test]
        public void should_delete_download_history_of_deleted_artists_for_the_music_kind()
        {
            Subject.Handle(new ArtistsDeletedEvent(new List<Artist> { new Artist { Id = 4 }, new Artist { Id = 5 } }, false, false));

            Mocker.GetMock<IDownloadHistoryRepository>()
                .Verify(r => r.DeleteByParentIds(MediaKind.Music, It.Is<List<int>>(l => l.Count == 2 && l.Contains(4) && l.Contains(5))), Times.Once());
        }
    }
}
