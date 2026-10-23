using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Events;
using NzbDrone.Core.Tv.History;
using NzbDrone.Core.Tv.MediaFiles;
using NzbDrone.Core.Tv.MediaFiles.Events;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Test.TvTests
{
    // The TV history handlers: what a grab, a failure, an ignore, a delete and a rename write, one row
    // per episode, and that they leave the events of other media types alone.
    [TestFixture]
    public class HistoryEventsFixture : CoreTest<HistoryService>
    {
        private List<EpisodeHistory> _inserted;

        [SetUp]
        public void Setup()
        {
            _inserted = new List<EpisodeHistory>();

            Mocker.GetMock<IHistoryRepository>()
                  .Setup(r => r.Insert(It.IsAny<EpisodeHistory>()))
                  .Callback<EpisodeHistory>(h => _inserted.Add(h))
                  .Returns<EpisodeHistory>(h => h);

            Mocker.GetMock<IHistoryRepository>()
                  .Setup(r => r.InsertMany(It.IsAny<IList<EpisodeHistory>>()))
                  .Callback<IList<EpisodeHistory>>(h => _inserted.AddRange(h));
        }

        private static RemoteEpisode NewRemoteEpisode(long? size = 1234, params int[] episodeIds)
        {
            return new RemoteEpisode
            {
                Series = new Series { Id = 5, Title = "A Series" },
                Episodes = episodeIds.Select(id => new Episode { Id = id, SeriesId = 5, SeasonNumber = 1, EpisodeNumber = id }).ToList(),
                ParsedEpisodeInfo = new ParsedEpisodeInfo
                {
                    Quality = new QualityModel(Quality.WEBDL1080p),
                    ReleaseGroup = "GRP"
                },
                Languages = new List<Language> { Language.English },
                Release = new TorrentInfo
                {
                    Title = "A.Series.S01E01E02.1080p.WEB-DL-GRP",
                    Indexer = "idx",
                    Size = size,
                    InfoHash = "ABCDEF",
                    DownloadProtocol = DownloadProtocol.Torrent
                }
            };
        }

        [Test]
        public void should_write_a_grab_row_for_every_episode()
        {
            Subject.Handle(new ReleaseGrabbedEvent(NewRemoteEpisode(1234, 11, 12))
            {
                DownloadId = "HASH",
                DownloadClient = "QBittorrent",
                DownloadClientName = "qbit"
            });

            _inserted.Should().HaveCount(2);
            _inserted.Select(h => h.ItemId).Should().Equal(11, 12);
            _inserted.Should().OnlyContain(h => h.MediaKind == MediaKind.Tv &&
                                                 h.EventType == EpisodeHistoryEventType.Grabbed &&
                                                 h.ParentId == 5 &&
                                                 h.DownloadId == "HASH" &&
                                                 h.SourceTitle == "A.Series.S01E01E02.1080p.WEB-DL-GRP");

            var data = _inserted[0].Data;

            data["Indexer"].Should().Be("idx");
            data["ReleaseGroup"].Should().Be("GRP");
            data["DownloadClient"].Should().Be("QBittorrent");
            data["DownloadClientName"].Should().Be("qbit");
            data["Size"].Should().Be("1234");
            data["TorrentInfoHash"].Should().Be("ABCDEF");
            data.Should().ContainKeys("IndexerFlags", "IndexerId", "ReleaseType", "CustomFormatScore", "SeriesMatchType", "ReleaseSource", "Protocol", "PublishedDate");
        }

        [Test]
        public void should_write_a_size_the_blocklist_can_parse_for_a_release_with_no_size()
        {
            Subject.Handle(new ReleaseGrabbedEvent(NewRemoteEpisode(null, 11)) { DownloadId = "HASH" });

            long.TryParse(_inserted.Single().Data["Size"], out var size).Should().BeTrue();
            size.Should().Be(0);
        }

        [Test]
        public void should_not_write_tv_history_for_a_grab_of_another_media_type()
        {
            var movie = new RemoteMovie { Release = new ReleaseInfo { Title = "Movie.2020" } };

            Subject.Handle(new ReleaseGrabbedEvent(movie) { DownloadId = "HASH" });

            _inserted.Should().BeEmpty();
        }

        [Test]
        public void should_write_a_failure_row_for_every_episode_of_a_tv_download()
        {
            Subject.Handle(new DownloadFailedEvent
            {
                Kind = MediaKind.Tv,
                ParentId = 5,
                ItemIds = new List<int> { 11, 12, 13 },
                DownloadId = "HASH",
                SourceTitle = "A.Series.S01",
                Quality = new QualityModel(Quality.HDTV720p),
                Message = "Disk full",
                DownloadClient = "QBittorrent",
                Data = new Dictionary<string, string> { { "indexer", "idx" }, { "size", "99" }, { "releasegroup", "GRP" } }
            });

            _inserted.Should().HaveCount(3);
            _inserted.Select(h => h.ItemId).Should().Equal(11, 12, 13);
            _inserted.Should().OnlyContain(h => h.EventType == EpisodeHistoryEventType.DownloadFailed &&
                                                 h.MediaKind == MediaKind.Tv &&
                                                 h.ParentId == 5);
            _inserted[0].Data["Message"].Should().Be("Disk full");
        }

        [Test]
        public void should_not_write_tv_history_for_a_failure_of_another_media_type()
        {
            Subject.Handle(new DownloadFailedEvent { Kind = MediaKind.Movies, ParentId = 9, ItemIds = new List<int> { 9 }, DownloadId = "HASH" });

            _inserted.Should().BeEmpty();
        }

        [Test]
        public void should_write_an_ignored_row_for_every_episode()
        {
            Subject.Handle(new DownloadIgnoredEvent
            {
                Kind = MediaKind.Tv,
                ParentId = 5,
                ItemIds = new List<int> { 11, 12 },
                DownloadId = "HASH",
                SourceTitle = "A.Series.S01E01E02",
                Quality = new QualityModel(Quality.HDTV720p),
                DownloadClientInfo = new DownloadClientItemClientInfo { Id = 1, Name = "sab", Type = "Sabnzbd" }
            });

            _inserted.Should().HaveCount(2);
            _inserted.Should().OnlyContain(h => h.EventType == EpisodeHistoryEventType.DownloadIgnored && h.ParentId == 5);
            _inserted.Select(h => h.ItemId).Should().Equal(11, 12);
        }

        [Test]
        public void should_not_write_tv_history_for_an_ignored_download_of_another_media_type()
        {
            Subject.Handle(new DownloadIgnoredEvent
            {
                Kind = MediaKind.Movies,
                ParentId = 9,
                ItemIds = new List<int> { 9 },
                DownloadId = "HASH",
                DownloadClientInfo = new DownloadClientItemClientInfo { Id = 1, Name = "sab", Type = "Sabnzbd" }
            });

            _inserted.Should().BeEmpty();
            Mocker.GetMock<IHistoryRepository>().Verify(r => r.InsertMany(It.IsAny<IList<EpisodeHistory>>()), Times.Never());
        }

        [TestCase(DeleteMediaFileReason.NoLinkedEpisodes)]
        [TestCase(DeleteMediaFileReason.ManualOverride)]
        public void should_not_write_a_row_for_a_housekeeping_file_delete(DeleteMediaFileReason reason)
        {
            var file = new EpisodeFile { Id = 3, SeriesId = 5, Quality = new QualityModel(Quality.HDTV720p) };

            Subject.Handle(new EpisodeFileDeletedEvent(file, reason));

            _inserted.Should().BeEmpty();
        }

        [Test]
        public void should_write_a_row_for_every_episode_of_a_deleted_file()
        {
            var file = new EpisodeFile
            {
                Id = 3,
                SeriesId = 5,
                RelativePath = "Season 01/a.mkv",
                Quality = new QualityModel(Quality.HDTV720p),
                Languages = new List<Language> { Language.English },
                Episodes = new List<Episode> { new Episode { Id = 11 }, new Episode { Id = 12 } }
            };

            Subject.Handle(new EpisodeFileDeletedEvent(file, DeleteMediaFileReason.Manual));

            _inserted.Should().HaveCount(2);
            _inserted.Should().OnlyContain(h => h.EventType == EpisodeHistoryEventType.EpisodeFileDeleted && h.ParentId == 5);
            _inserted[0].Data["Reason"].Should().Be(DeleteMediaFileReason.Manual.ToString());
        }

        [Test]
        public void should_delete_the_history_of_deleted_series()
        {
            Subject.Handle(new SeriesDeletedEvent(new List<Series> { new Series { Id = 5 }, new Series { Id = 6 } }, false, false));

            Mocker.GetMock<IHistoryRepository>()
                  .Verify(r => r.DeleteForSeries(It.Is<List<int>>(ids => ids.SequenceEqual(new[] { 5, 6 }))), Times.Once());
        }

        [Test]
        public void should_not_write_a_row_for_a_download_that_is_not_new()
        {
            var info = new LocalEpisode
            {
                Series = new Series { Id = 5, Path = "/tv/a" },
                Episodes = new List<Episode> { new Episode { Id = 11 } },
                Path = "/downloads/a.mkv",
                Quality = new QualityModel(Quality.HDTV720p)
            };

            Subject.Handle(new EpisodeImportedEvent(info, new EpisodeFile { Id = 3, SeriesId = 5, RelativePath = "a.mkv" }, new List<DeletedEpisodeFile>(), false, null));

            _inserted.Should().BeEmpty();
        }

        [Test]
        public void should_write_an_import_row_for_every_episode_of_a_new_download()
        {
            var info = new LocalEpisode
            {
                Series = new Series { Id = 5, Path = "/tv/a" },
                Episodes = new List<Episode> { new Episode { Id = 11 }, new Episode { Id = 12 } },
                Path = "/downloads/a.mkv",
                Quality = new QualityModel(Quality.HDTV720p),
                Languages = new List<Language> { Language.English }
            };
            var item = new DownloadClientItem
            {
                DownloadId = "HASH",
                DownloadClientInfo = new DownloadClientItemClientInfo { Id = 1, Name = "qbit", Type = "QBittorrent" }
            };

            Subject.Handle(new EpisodeImportedEvent(info, new EpisodeFile { Id = 3, SeriesId = 5, RelativePath = "a.mkv", SceneName = "A.Series.S01E01E02" }, new List<DeletedEpisodeFile>(), true, item));

            _inserted.Should().HaveCount(2);
            _inserted.Should().OnlyContain(h => h.EventType == EpisodeHistoryEventType.DownloadFolderImported &&
                                                 h.DownloadId == "HASH" &&
                                                 h.SourceTitle == "A.Series.S01E01E02" &&
                                                 h.ParentId == 5);
            _inserted[0].Data["DownloadClientName"].Should().Be("qbit");
        }
    }
}
