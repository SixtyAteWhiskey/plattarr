using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Events;
using NzbDrone.Core.Music.History;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.MediaFiles.Events;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests
{
    // The Music history handlers: what a grab, an import, a failure, an ignore, a delete, a rename and a
    // retag write (one row per album or per track), and that they leave the events of other media types alone.
    [TestFixture]
    public class MusicHistoryEventsFixture : CoreTest<HistoryService>
    {
        private List<AlbumHistory> _inserted;

        [SetUp]
        public void Setup()
        {
            _inserted = new List<AlbumHistory>();

            Mocker.GetMock<IAlbumHistoryRepository>()
                  .Setup(r => r.Insert(It.IsAny<AlbumHistory>()))
                  .Callback<AlbumHistory>(h => _inserted.Add(h))
                  .Returns<AlbumHistory>(h => h);

            Mocker.GetMock<IAlbumHistoryRepository>()
                  .Setup(r => r.InsertMany(It.IsAny<IList<AlbumHistory>>()))
                  .Callback<IList<AlbumHistory>>(h => _inserted.AddRange(h));
        }

        private static RemoteAlbum NewRemoteAlbum(long? size = 1234, params int[] albumIds)
        {
            return new RemoteAlbum
            {
                Artist = new Artist { Id = 5 },
                Albums = albumIds.Select(id => new Album { Id = id, Title = "Album " + id }).ToList(),
                ParsedAlbumInfo = new ParsedAlbumInfo
                {
                    Quality = new AudioQualityModel(AudioQuality.FLAC),
                    ReleaseGroup = "GRP"
                },
                Release = new TorrentInfo
                {
                    Title = "Artist-Album-2010-FLAC-GRP",
                    Indexer = "idx",
                    Size = size,
                    InfoHash = "ABCDEF",
                    DownloadProtocol = DownloadProtocol.Torrent
                }
            };
        }

        private static TrackFile NewTrackFile(int albumId = 21, params int[] trackIds)
        {
            return new TrackFile
            {
                Id = 90,
                AlbumId = albumId,
                Path = "/music/a/01.flac",
                Size = 555,
                ReleaseGroup = "GRP",
                Quality = new AudioQualityModel(AudioQuality.FLAC),
                IndexerFlags = IndexerFlags.G_Freeleech,
                Artist = new LazyLoaded<Artist>(new Artist { Id = 5 }),
                Tracks = new LazyLoaded<List<Track>>(trackIds.Select(id => new Track { Id = id }).ToList())
            };
        }

        [Test]
        public void should_write_a_grab_row_for_every_album()
        {
            Subject.Handle(new ReleaseGrabbedEvent(NewRemoteAlbum(1234, 11, 12))
            {
                DownloadId = "HASH",
                DownloadClient = "QBittorrent",
                DownloadClientName = "qbit"
            });

            _inserted.Should().HaveCount(2);
            _inserted.Select(h => h.ItemId).Should().Equal(11, 12);
            _inserted.Should().OnlyContain(h => h.MediaKind == MediaKind.Music &&
                                                 h.EventType == AlbumHistoryEventType.Grabbed &&
                                                 h.ParentId == 5 &&
                                                 h.DownloadId == "HASH" &&
                                                 h.SourceTitle == "Artist-Album-2010-FLAC-GRP");

            var data = _inserted[0].Data;

            data["Indexer"].Should().Be("idx");
            data["ReleaseGroup"].Should().Be("GRP");
            data["DownloadClient"].Should().Be("QBittorrent");
            data["DownloadClientName"].Should().Be("qbit");
            data["Size"].Should().Be("1234");
            data["TorrentInfoHash"].Should().Be("ABCDEF");
            data["Protocol"].Should().Be(((int)DownloadProtocol.Torrent).ToString());
            data.Should().ContainKeys("IndexerFlags", "IndexerId", "CustomFormatScore", "ReleaseSource", "PublishedDate", "DownloadForced", "Guid", "DownloadUrl");
        }

        [Test]
        public void should_write_a_size_the_blocklist_can_parse_for_a_release_with_no_size()
        {
            Subject.Handle(new ReleaseGrabbedEvent(NewRemoteAlbum(null, 11)) { DownloadId = "HASH" });

            long.TryParse(_inserted.Single().Data["Size"], out var size).Should().BeTrue();
            size.Should().Be(0);
        }

        [Test]
        public void should_write_the_release_hash_only_when_the_parser_found_one()
        {
            var withHash = NewRemoteAlbum(1, 11);
            withHash.ParsedAlbumInfo.ReleaseHash = "RELHASH";

            Subject.Handle(new ReleaseGrabbedEvent(withHash) { DownloadId = "HASH" });
            Subject.Handle(new ReleaseGrabbedEvent(NewRemoteAlbum(1, 12)) { DownloadId = "HASH" });

            _inserted[0].Data["ReleaseHash"].Should().Be("RELHASH");
            _inserted[1].Data.Should().NotContainKey("ReleaseHash");
        }

        [Test]
        public void should_not_write_music_history_for_a_grab_of_another_media_type()
        {
            var movie = new RemoteMovie { Release = new ReleaseInfo { Title = "Movie.2020" } };

            Subject.Handle(new ReleaseGrabbedEvent(movie) { DownloadId = "HASH" });

            _inserted.Should().BeEmpty();
        }

        [Test]
        public void should_write_a_failure_row_for_every_album_of_a_music_download()
        {
            Subject.Handle(new DownloadFailedEvent
            {
                Kind = MediaKind.Music,
                ParentId = 5,
                ItemIds = new List<int> { 11, 12, 13 },
                DownloadId = "HASH",
                SourceTitle = "Artist-Discography",
                Quality = new AudioQualityModel(AudioQuality.MP3_320).ToDisplayModel(),
                Message = "Disk full",
                DownloadClient = "QBittorrent",
                Data = new Dictionary<string, string> { { "indexer", "idx" }, { "size", "99" }, { "releaseGroup", "GRP" } }
            });

            _inserted.Should().HaveCount(3);
            _inserted.Select(h => h.ItemId).Should().Equal(11, 12, 13);
            _inserted.Should().OnlyContain(h => h.EventType == AlbumHistoryEventType.DownloadFailed &&
                                                 h.MediaKind == MediaKind.Music &&
                                                 h.ParentId == 5 &&
                                                 h.Quality.Equals(new AudioQualityModel(AudioQuality.MP3_320)));
            _inserted[0].Data["Message"].Should().Be("Disk full");
            _inserted[0].Data["Indexer"].Should().Be("idx");
            _inserted[0].Data["Size"].Should().Be("99");
            _inserted[0].Data["ReleaseGroup"].Should().Be("GRP");
        }

        [TestCase(MediaKind.Movies)]
        [TestCase(MediaKind.Tv)]
        public void should_not_write_a_failure_row_for_another_media_type(MediaKind kind)
        {
            Subject.Handle(new DownloadFailedEvent { Kind = kind, ParentId = 5, ItemIds = new List<int> { 11 }, Quality = new QualityModel(), Data = new Dictionary<string, string>() });

            _inserted.Should().BeEmpty();
        }

        [Test]
        public void should_write_an_ignored_row_for_every_album_in_one_batch()
        {
            Subject.Handle(new DownloadIgnoredEvent
            {
                Kind = MediaKind.Music,
                ParentId = 5,
                ItemIds = new List<int> { 11, 12 },
                DownloadId = "HASH",
                SourceTitle = "Artist-Album",
                Quality = new AudioQualityModel(AudioQuality.FLAC).ToDisplayModel(),
                Message = "ignored",
                DownloadClientInfo = new DownloadClientItemClientInfo { Type = "QBittorrent", Name = "qbit" }
            });

            _inserted.Should().HaveCount(2);
            _inserted.Should().OnlyContain(h => h.EventType == AlbumHistoryEventType.DownloadIgnored && h.MediaKind == MediaKind.Music);
            _inserted[0].Data["DownloadClientName"].Should().Be("qbit");
            Mocker.GetMock<IAlbumHistoryRepository>().Verify(r => r.InsertMany(It.IsAny<IList<AlbumHistory>>()), Times.Once());
        }

        [Test]
        public void should_not_write_an_ignored_row_for_another_media_type()
        {
            Subject.Handle(new DownloadIgnoredEvent { Kind = MediaKind.Tv, ParentId = 5, ItemIds = new List<int> { 11 }, Quality = new QualityModel() });

            _inserted.Should().BeEmpty();
        }

        [Test]
        public void should_write_an_import_row_for_every_track_of_a_new_download()
        {
            var local = new LocalTrack
            {
                Artist = new Artist { Id = 5 },
                Album = new Album { Id = 21 },
                Tracks = new List<Track> { new Track { Id = 31, AlbumId = 21 }, new Track { Id = 32, AlbumId = 21 } },
                Path = "/downloads/a/01.flac",
                Size = 555,
                ReleaseGroup = "GRP",
                Quality = new AudioQualityModel(AudioQuality.FLAC)
            };
            var imported = NewTrackFile(21, 31, 32);

            Subject.Handle(new TrackImportedEvent(local, imported, new List<TrackFile>(), true, new DownloadClientItem
            {
                DownloadId = "HASH",
                DownloadClientInfo = new DownloadClientItemClientInfo { Type = "QBittorrent", Name = "qbit" }
            }));

            _inserted.Should().HaveCount(2);
            _inserted.Select(h => h.Data[AlbumHistory.TRACK_ID]).Should().Equal("31", "32");
            _inserted.Should().OnlyContain(h => h.EventType == AlbumHistoryEventType.TrackFileImported &&
                                                 h.ParentId == 5 &&
                                                 h.ItemId == 21 &&
                                                 h.DownloadId == "HASH");
            _inserted[0].Data["FileId"].Should().Be("90");
            _inserted[0].Data["DroppedPath"].Should().Be("/downloads/a/01.flac");
            _inserted[0].Data["ImportedPath"].Should().Be("/music/a/01.flac");
            _inserted[0].Data["DownloadClientName"].Should().Be("qbit");
        }

        [Test]
        public void should_not_write_an_import_row_for_a_file_that_was_already_in_the_library()
        {
            var local = new LocalTrack { Artist = new Artist { Id = 5 }, Album = new Album { Id = 21 }, Tracks = new List<Track> { new Track { Id = 31 } } };

            Subject.Handle(new TrackImportedEvent(local, NewTrackFile(21, 31), new List<TrackFile>(), false, null));

            _inserted.Should().BeEmpty();
        }

        [Test]
        public void should_write_a_download_imported_row_for_every_album_when_the_core_completes_a_music_download()
        {
            var trackedDownload = new TrackedDownload
            {
                Kind = MediaKind.Music,
                DownloadItem = new DownloadClientItem { DownloadId = "HASH", Title = "Artist-Album" },
                RemoteRelease = NewRemoteAlbum(1, 11, 12)
            };

            Subject.Handle(new DownloadCompletedEvent(trackedDownload, 5));

            _inserted.Should().HaveCount(2);
            _inserted.Select(h => h.ItemId).Should().Equal(11, 12);
            _inserted.Should().OnlyContain(h => h.EventType == AlbumHistoryEventType.DownloadImported &&
                                                 h.MediaKind == MediaKind.Music &&
                                                 h.ParentId == 5 &&
                                                 h.DownloadId == "HASH" &&
                                                 h.SourceTitle == "Artist-Album");
        }

        [Test]
        public void should_not_write_a_download_imported_row_for_another_media_type_or_an_unmatched_download()
        {
            Subject.Handle(new DownloadCompletedEvent(new TrackedDownload { Kind = MediaKind.Tv, DownloadItem = new DownloadClientItem { DownloadId = "HASH" } }, 5));
            Subject.Handle(new DownloadCompletedEvent(new TrackedDownload { Kind = MediaKind.Music, DownloadItem = new DownloadClientItem { DownloadId = "HASH" } }, 5));

            _inserted.Should().BeEmpty();
        }

        [Test]
        public void should_not_write_a_download_imported_row_for_a_download_of_another_media_type_even_when_it_holds_a_music_release()
        {
            var trackedDownload = new TrackedDownload
            {
                Kind = MediaKind.Tv,
                DownloadItem = new DownloadClientItem { DownloadId = "HASH", Title = "Artist-Album" },
                RemoteRelease = NewRemoteAlbum(1, 11)
            };

            Subject.Handle(new DownloadCompletedEvent(trackedDownload, 5));

            _inserted.Should().BeEmpty();
        }

        [Test]
        public void should_write_a_delete_row_for_every_track_of_a_deleted_file()
        {
            Subject.Handle(new TrackFileDeletedEvent(NewTrackFile(21, 31, 32), DeleteMediaFileReason.Manual));

            _inserted.Should().HaveCount(2);
            _inserted.Select(h => h.Data[AlbumHistory.TRACK_ID]).Should().Equal("31", "32");
            _inserted.Should().OnlyContain(h => h.EventType == AlbumHistoryEventType.TrackFileDeleted &&
                                                 h.ParentId == 5 &&
                                                 h.ItemId == 21 &&
                                                 h.SourceTitle == "/music/a/01.flac");
            _inserted[0].Data["Reason"].Should().Be("Manual");
        }

        [TestCase(DeleteMediaFileReason.NoLinkedEpisodes)]
        [TestCase(DeleteMediaFileReason.ManualOverride)]
        public void should_not_write_a_delete_row_for_a_cleanup_or_an_override(DeleteMediaFileReason reason)
        {
            Subject.Handle(new TrackFileDeletedEvent(NewTrackFile(21, 31), reason));

            _inserted.Should().BeEmpty();
        }

        [Test]
        public void should_not_write_a_delete_row_when_the_artist_is_gone()
        {
            var file = NewTrackFile(21, 31);
            file.Artist = new LazyLoaded<Artist>(null);

            Subject.Handle(new TrackFileDeletedEvent(file, DeleteMediaFileReason.Manual));

            _inserted.Should().BeEmpty();
        }

        [Test]
        public void should_write_a_rename_row_for_every_track()
        {
            Subject.Handle(new TrackFileRenamedEvent(new Artist { Id = 5 }, NewTrackFile(21, 31, 32), "/music/a/old.flac"));

            _inserted.Should().HaveCount(2);
            _inserted.Should().OnlyContain(h => h.EventType == AlbumHistoryEventType.TrackFileRenamed &&
                                                 h.SourceTitle == "/music/a/old.flac" &&
                                                 h.Data["SourcePath"] == "/music/a/old.flac" &&
                                                 h.Data["Path"] == "/music/a/01.flac");
            _inserted.Select(h => h.Data[AlbumHistory.TRACK_ID]).Should().Equal("31", "32");
        }

        [Test]
        public void should_write_a_retag_row_with_the_tag_diff_for_every_track()
        {
            var diff = new Dictionary<string, Tuple<string, string>> { { "Title", Tuple.Create("Old", "New") } };

            Subject.Handle(new TrackFileRetaggedEvent(new Artist { Id = 5 }, NewTrackFile(21, 31), diff, true));

            var row = _inserted.Single();

            row.EventType.Should().Be(AlbumHistoryEventType.TrackFileRetagged);
            row.Data[AlbumHistory.TRACK_ID].Should().Be("31");
            row.Data["TagsScrubbed"].Should().Be("True");
            row.Data["Diff"].Should().Contain("Title").And.Contain("Old").And.Contain("New");
        }

        [Test]
        public void should_delete_the_history_of_deleted_artists()
        {
            Subject.Handle(new ArtistsDeletedEvent(new List<Artist> { new Artist { Id = 5 }, new Artist { Id = 6 } }, false, false));

            Mocker.GetMock<IAlbumHistoryRepository>()
                  .Verify(r => r.DeleteForArtists(It.Is<List<int>>(ids => ids.SequenceEqual(new[] { 5, 6 }))), Times.Once());
        }
    }
}
