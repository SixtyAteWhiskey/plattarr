using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Blocklisting;
using NzbDrone.Core.Music.Events;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class MusicBlocklistServiceFixture : CoreTest<BlocklistService>
    {
        private static readonly DateTime Published = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        private Blocklist _inserted;

        [SetUp]
        public void Setup()
        {
            _inserted = null;

            Mocker.GetMock<IBlocklistRepository>()
                  .Setup(v => v.Insert(It.IsAny<Blocklist>()))
                  .Callback<Blocklist>(b => _inserted = b)
                  .Returns<Blocklist>(b => b);
        }

        private static DownloadFailedEvent Failure(MediaKind kind, int parentId = 7)
        {
            return new DownloadFailedEvent
            {
                Kind = kind,
                ParentId = parentId,
                ItemIds = new List<int> { 1, 2 },
                SourceTitle = "Artist - Album [FLAC]",
                Quality = new AudioQualityModel(AudioQuality.FLAC, new Revision(2)).ToDisplayModel(),
                Message = "failed",
                Data = new Dictionary<string, string>
                {
                    { "publishedDate", "2026-10-01T12:00:00Z" },
                    { "size", "99" },
                    { "indexer", "idx" },
                    { "protocol", ((int)DownloadProtocol.Torrent).ToString() },
                    { "torrentInfoHash", "HASH" },
                    { "indexerFlags", "G_Freeleech" }
                }
            };
        }

        [Test]
        public void should_blocklist_a_failed_music_download_for_every_album_it_covered()
        {
            Subject.Handle(Failure(MediaKind.Music));

            _inserted.Should().NotBeNull();
            _inserted.MediaKind.Should().Be(MediaKind.Music);
            _inserted.ParentId.Should().Be(7);
            _inserted.ItemIds.Should().Equal(1, 2);
            _inserted.SourceTitle.Should().Be("Artist - Album [FLAC]");
            _inserted.Quality.Should().Be(new AudioQualityModel(AudioQuality.FLAC, new Revision(2)));
            _inserted.Size.Should().Be(99);
            _inserted.Indexer.Should().Be("idx");
            _inserted.Protocol.Should().Be(DownloadProtocol.Torrent);
            _inserted.TorrentInfoHash.Should().Be("HASH");
            _inserted.IndexerFlags.Should().Be(IndexerFlags.G_Freeleech);
            _inserted.Message.Should().Be("failed");
            _inserted.PublishedDate.Value.ToUniversalTime().Should().Be(Published);
            _inserted.Date.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Test]
        public void should_prefer_the_download_id_of_a_tracked_torrent_as_the_hash()
        {
            var failure = Failure(MediaKind.Music);
            failure.TrackedDownload = new TrackedDownload
            {
                Protocol = DownloadProtocol.Torrent,
                DownloadItem = new DownloadClientItem { DownloadId = "TRACKEDHASH" }
            };

            Subject.Handle(failure);

            _inserted.TorrentInfoHash.Should().Be("TRACKEDHASH");
        }

        [Test]
        public void should_keep_a_usenet_failure_without_a_hash_and_a_missing_size_as_zero()
        {
            var failure = Failure(MediaKind.Music);
            failure.Data["protocol"] = ((int)DownloadProtocol.Usenet).ToString();
            failure.Data.Remove("torrentInfoHash");
            failure.Data.Remove("size");
            failure.Data.Remove("indexerFlags");

            Subject.Handle(failure);

            _inserted.Protocol.Should().Be(DownloadProtocol.Usenet);
            _inserted.TorrentInfoHash.Should().BeNull();
            _inserted.Size.Should().Be(0);
            _inserted.IndexerFlags.Should().Be(default(IndexerFlags));
        }

        [TestCase(MediaKind.Movies)]
        [TestCase(MediaKind.Tv)]
        public void should_not_blocklist_a_failed_download_of_another_media_type(MediaKind kind)
        {
            Subject.Handle(Failure(kind));

            Mocker.GetMock<IBlocklistRepository>().Verify(v => v.Insert(It.IsAny<Blocklist>()), Times.Never());
        }

        [Test]
        public void should_purge_the_music_blocklist_when_cleared()
        {
            Subject.Execute(new ClearMusicBlocklistCommand());

            Mocker.GetMock<IBlocklistRepository>().Verify(v => v.Purge(false), Times.Once());
        }

        [Test]
        public void should_delete_the_blocklist_of_deleted_artists()
        {
            Subject.HandleAsync(new ArtistsDeletedEvent(new List<Artist> { new Artist { Id = 7 }, new Artist { Id = 8 } }, false, false));

            Mocker.GetMock<IBlocklistRepository>()
                  .Verify(v => v.DeleteForArtistIds(It.Is<List<int>>(ids => ids.SequenceEqual(new[] { 7, 8 }))), Times.Once());
        }
    }
}
