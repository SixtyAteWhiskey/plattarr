using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Blocklisting;
using NzbDrone.Core.Tv.Events;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class BlocklistServiceFixture : CoreTest<BlocklistService>
    {
        private static readonly DateTime Published = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        private void GivenBlocklisted(params Blocklist[] items)
        {
            Mocker.GetMock<IBlocklistRepository>()
                  .Setup(v => v.BlocklistedByTitle(7, It.IsAny<string>()))
                  .Returns(items.ToList());

            Mocker.GetMock<IBlocklistRepository>()
                  .Setup(v => v.BlocklistedByTorrentInfoHash(7, It.IsAny<string>()))
                  .Returns(items.ToList());
        }

        private static Blocklist Item(DownloadProtocol protocol, string indexer = "idx", string hash = null, DateTime? published = null)
        {
            return new Blocklist
            {
                ParentId = 7,
                SourceTitle = "Show.S01E01",
                Protocol = protocol,
                Indexer = indexer,
                TorrentInfoHash = hash,
                PublishedDate = published ?? Published
            };
        }

        private static ReleaseInfo Nzb(string indexer = "idx", DateTime? published = null)
        {
            return new ReleaseInfo { Title = "Show.S01E01", DownloadProtocol = DownloadProtocol.Usenet, Indexer = indexer, PublishDate = published ?? Published };
        }

        private static TorrentInfo Torrent(string hash, string indexer = "idx")
        {
            return new TorrentInfo { Title = "Show.S01E01", DownloadProtocol = DownloadProtocol.Torrent, Indexer = indexer, InfoHash = hash };
        }

        [Test]
        public void should_blocklist_the_same_nzb_from_the_same_indexer_and_date()
        {
            GivenBlocklisted(Item(DownloadProtocol.Usenet));

            Subject.Blocklisted(7, Nzb()).Should().BeTrue();
        }

        [Test]
        public void should_not_blocklist_an_nzb_from_another_indexer()
        {
            GivenBlocklisted(Item(DownloadProtocol.Usenet));

            Subject.Blocklisted(7, Nzb("other")).Should().BeFalse();
        }

        [Test]
        public void should_not_blocklist_an_nzb_with_another_publish_date()
        {
            GivenBlocklisted(Item(DownloadProtocol.Usenet));

            Subject.Blocklisted(7, Nzb(published: Published.AddHours(1))).Should().BeFalse();
        }

        [Test]
        public void should_not_match_an_nzb_to_a_torrent_blocklist_entry()
        {
            GivenBlocklisted(Item(DownloadProtocol.Torrent));

            Subject.Blocklisted(7, Nzb()).Should().BeFalse();
        }

        [Test]
        public void should_blocklist_a_torrent_by_info_hash_ignoring_case()
        {
            GivenBlocklisted(Item(DownloadProtocol.Torrent, hash: "ABC123"));

            Subject.Blocklisted(7, Torrent("abc123")).Should().BeTrue();
        }

        [Test]
        public void should_not_blocklist_a_torrent_with_another_info_hash()
        {
            GivenBlocklisted(Item(DownloadProtocol.Torrent, hash: "ABC123"));

            Subject.Blocklisted(7, Torrent("zzz999")).Should().BeFalse();
        }

        [Test]
        public void should_look_a_torrent_with_a_hash_up_by_its_hash_only()
        {
            Mocker.GetMock<IBlocklistRepository>()
                  .Setup(v => v.BlocklistedByTorrentInfoHash(7, "ABC123"))
                  .Returns(new List<Blocklist> { Item(DownloadProtocol.Torrent, hash: "ABC123") });

            Subject.Blocklisted(7, Torrent("ABC123")).Should().BeTrue();

            Mocker.GetMock<IBlocklistRepository>().Verify(v => v.BlocklistedByTitle(It.IsAny<int>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_look_a_torrent_without_a_hash_up_by_its_title_only()
        {
            Mocker.GetMock<IBlocklistRepository>()
                  .Setup(v => v.BlocklistedByTitle(7, "Show.S01E01"))
                  .Returns(new List<Blocklist> { Item(DownloadProtocol.Torrent) });

            Subject.Blocklisted(7, Torrent(null)).Should().BeTrue();

            Mocker.GetMock<IBlocklistRepository>().Verify(v => v.BlocklistedByTorrentInfoHash(It.IsAny<int>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_blocklist_a_torrent_without_a_hash_by_title_and_indexer()
        {
            GivenBlocklisted(Item(DownloadProtocol.Torrent));

            Subject.Blocklisted(7, Torrent(null)).Should().BeTrue();
            Subject.Blocklisted(7, Torrent(null, "other")).Should().BeFalse();
        }

        [Test]
        public void should_not_match_a_hashless_torrent_to_a_usenet_entry()
        {
            GivenBlocklisted(Item(DownloadProtocol.Usenet));

            Subject.Blocklisted(7, Torrent(null)).Should().BeFalse();
        }

        [Test]
        public void should_not_blocklist_a_torrent_release_that_is_not_torrent_info()
        {
            GivenBlocklisted(Item(DownloadProtocol.Torrent));

            Subject.Blocklisted(7, new ReleaseInfo { Title = "Show.S01E01", DownloadProtocol = DownloadProtocol.Torrent, Indexer = "idx" }).Should().BeFalse();
        }

        [Test]
        public void should_match_a_torrent_hash_ignoring_case()
        {
            GivenBlocklisted(Item(DownloadProtocol.Torrent, hash: "ABC123"));

            Subject.BlocklistedTorrentHash(7, "abc123").Should().BeTrue();
            Subject.BlocklistedTorrentHash(7, "other").Should().BeFalse();
        }

        [Test]
        public void should_block_a_remote_episode()
        {
            Blocklist inserted = null;

            Mocker.GetMock<IBlocklistRepository>()
                  .Setup(v => v.Insert(It.IsAny<Blocklist>()))
                  .Callback<Blocklist>(b => inserted = b)
                  .Returns<Blocklist>(b => b);

            var remote = new RemoteEpisode
            {
                Series = new Series { Id = 7 },
                Episodes = new List<Episode> { new Episode { Id = 1 }, new Episode { Id = 2 } },
                ParsedEpisodeInfo = new ParsedEpisodeInfo
                {
                    Quality = new QualityModel(Quality.HDTV720p),
                    Languages = new List<Language> { Language.French },
                    SeasonNumbers = new List<int> { 1 },
                    EpisodeNumbers = new[] { 1, 2 }
                },
                Release = new TorrentInfo
                {
                    Title = "Show.S01E01E02",
                    Indexer = "idx",
                    DownloadProtocol = DownloadProtocol.Torrent,
                    InfoHash = "HASH",
                    PublishDate = Published,
                    Size = 99
                }
            };

            Subject.Block(remote, "bad file");

            inserted.MediaKind.Should().Be(MediaKind.Tv);
            inserted.ParentId.Should().Be(7);
            inserted.ItemIds.Should().Equal(1, 2);
            inserted.SourceTitle.Should().Be("Show.S01E01E02");
            inserted.Quality.Quality.Should().Be(Quality.HDTV720p);
            inserted.Languages.Should().Equal(Language.French);
            inserted.PublishedDate.Should().Be(Published);
            inserted.Size.Should().Be(99);
            inserted.Indexer.Should().Be("idx");
            inserted.Protocol.Should().Be(DownloadProtocol.Torrent);
            inserted.ReleaseType.Should().Be(ReleaseType.MultiEpisode);
            inserted.Message.Should().Be("bad file");
            inserted.TorrentInfoHash.Should().Be("HASH");
            inserted.Date.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Test]
        public void should_not_set_a_hash_when_blocking_a_usenet_release()
        {
            Blocklist inserted = null;

            Mocker.GetMock<IBlocklistRepository>()
                  .Setup(v => v.Insert(It.IsAny<Blocklist>()))
                  .Callback<Blocklist>(b => inserted = b)
                  .Returns<Blocklist>(b => b);

            Subject.Block(
                new RemoteEpisode
                {
                    Series = new Series { Id = 7 },
                    Episodes = new List<Episode>(),
                    ParsedEpisodeInfo = new ParsedEpisodeInfo { Quality = new QualityModel(), SeasonNumbers = new List<int> { 1 }, EpisodeNumbers = new[] { 1 } },
                    Release = Nzb()
                },
                "bad");

            inserted.TorrentInfoHash.Should().BeNull();
            inserted.ReleaseType.Should().Be(ReleaseType.SingleEpisode);
        }

        [Test]
        public void should_delete_and_page_through_the_repository()
        {
            Subject.Delete(3);
            Subject.Delete(new List<int> { 4, 5 });
            Subject.Paged(new PagingSpec<Blocklist>());
            Subject.GetBySeries(7);

            Mocker.GetMock<IBlocklistRepository>().Verify(v => v.Delete(3), Times.Once());
            Mocker.GetMock<IBlocklistRepository>().Verify(v => v.DeleteMany(It.Is<IEnumerable<int>>(l => l.SequenceEqual(new[] { 4, 5 }))), Times.Once());
            Mocker.GetMock<IBlocklistRepository>().Verify(v => v.GetPaged(It.IsAny<PagingSpec<Blocklist>>()), Times.Once());
            Mocker.GetMock<IBlocklistRepository>().Verify(v => v.BlocklistedBySeries(7), Times.Once());
        }

        private static DownloadFailedEvent Failure(MediaKind kind, int parentId = 7)
        {
            return new DownloadFailedEvent
            {
                Kind = kind,
                ParentId = parentId,
                ItemIds = new List<int> { 1, 2 },
                SourceTitle = "Show.S01E01E02",
                Quality = new QualityModel(Quality.HDTV720p),
                Message = "failed",
                Languages = new List<Language> { Language.French },
                Data = new Dictionary<string, string>
                {
                    { "publishedDate", "2026-10-01T12:00:00Z" },
                    { "size", "99" },
                    { "indexer", "idx" },
                    { "protocol", ((int)DownloadProtocol.Torrent).ToString() },
                    { "torrentInfoHash", "HASH" },
                    { "indexerFlags", "G_Freeleech" },
                    { "releaseType", "MultiEpisode" }
                }
            };
        }

        [Test]
        public void should_blocklist_a_failed_tv_download_for_every_episode_it_covered()
        {
            Blocklist inserted = null;

            Mocker.GetMock<IBlocklistRepository>()
                  .Setup(v => v.Insert(It.IsAny<Blocklist>()))
                  .Callback<Blocklist>(b => inserted = b)
                  .Returns<Blocklist>(b => b);

            Subject.Handle(Failure(MediaKind.Tv));

            inserted.Should().NotBeNull();
            inserted.MediaKind.Should().Be(MediaKind.Tv);
            inserted.ParentId.Should().Be(7);
            inserted.ItemIds.Should().Equal(1, 2);
            inserted.SourceTitle.Should().Be("Show.S01E01E02");
            inserted.Size.Should().Be(99);
            inserted.Indexer.Should().Be("idx");
            inserted.Protocol.Should().Be(DownloadProtocol.Torrent);
            inserted.TorrentInfoHash.Should().Be("HASH");
            inserted.IndexerFlags.Should().Be(IndexerFlags.G_Freeleech);
            inserted.ReleaseType.Should().Be(ReleaseType.MultiEpisode);
            inserted.Languages.Should().Equal(Language.French);
            inserted.PublishedDate.Value.ToUniversalTime().Should().Be(Published);
        }

        [Test]
        public void should_not_blocklist_a_failed_download_of_another_media_type()
        {
            Subject.Handle(Failure(MediaKind.Movies));

            Mocker.GetMock<IBlocklistRepository>().Verify(v => v.Insert(It.IsAny<Blocklist>()), Times.Never());
        }

        [Test]
        public void should_purge_the_series_blocklist_when_cleared()
        {
            Subject.Execute(new ClearTvBlocklistCommand());

            Mocker.GetMock<IBlocklistRepository>().Verify(v => v.Purge(false), Times.Once());
        }

        [Test]
        public void should_delete_the_blocklist_of_deleted_series()
        {
            Subject.HandleAsync(new SeriesDeletedEvent(new List<Series> { new Series { Id = 7 }, new Series { Id = 8 } }, false, false));

            Mocker.GetMock<IBlocklistRepository>()
                  .Verify(v => v.DeleteForSeriesIds(It.Is<List<int>>(ids => ids.SequenceEqual(new[] { 7, 8 }))), Times.Once());
        }
    }
}
