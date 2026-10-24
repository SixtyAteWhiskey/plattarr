using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Crypto;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Configuration.Events;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Aggregation;
using NzbDrone.Core.Music.Events;
using NzbDrone.Core.Music.Parser;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Pending;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Queue;

namespace NzbDrone.Core.Test.MusicTests.PendingTests
{
    [TestFixture]
    public class PendingReleaseServiceFixture : PendingReleaseServiceFixtureBase
    {
        [Test]
        public void should_not_ignore_pending_items_from_available_indexer()
        {
            Mocker.GetMock<IIndexerStatusService>()
                .Setup(v => v.GetBlockedProviders())
                .Returns(new List<IndexerStatus>());

            GivenHeldRelease("a", "idx", _release.PublishDate);
            _heldReleases[0].Release.IndexerId = 1;

            Subject.GetPending().Should().ContainSingle();
        }

        [Test]
        public void should_ignore_pending_items_from_unavailable_indexer()
        {
            Mocker.GetMock<IIndexerStatusService>()
                .Setup(v => v.GetBlockedProviders())
                .Returns(new List<IndexerStatus> { new IndexerStatus { ProviderId = 1, DisabledTill = DateTime.UtcNow.AddHours(2) } });

            GivenHeldRelease("a", "idx", _release.PublishDate);
            _heldReleases[0].Release.IndexerId = 1;

            Subject.GetPending().Should().BeEmpty();
        }

        [Test]
        public void should_tell_each_pending_release_why_it_is_pending()
        {
            Mocker.GetMock<IIndexerStatusService>()
                .Setup(v => v.GetBlockedProviders())
                .Returns(new List<IndexerStatus>());

            GivenHeldRelease("a", "idx", _release.PublishDate, PendingReleaseReason.DownloadClientUnavailable);

            Subject.GetPending().Single().PendingReleaseReason.Should().Be(PendingReleaseReason.DownloadClientUnavailable);
        }

        [Test]
        public void should_skip_a_pending_release_whose_artist_is_gone()
        {
            GivenHeldRelease("a", "idx", _release.PublishDate);
            _heldReleases[0].ParentId = 4242;

            LoadCache();

            Subject.GetPendingRemoteAlbums(4242).Should().BeEmpty();
            Subject.GetPendingQueue().Should().BeEmpty();
        }

        [Test]
        public void should_map_the_albums_of_a_pending_release()
        {
            GivenHeldRelease("a", "idx", _release.PublishDate);

            var remote = Subject.GetPendingRemoteAlbums(_artist.Id).Single();

            remote.Artist.Should().BeSameAs(_artist);
            remote.Albums.Should().ContainSingle().Which.Id.Should().Be(_album.Id);

            Mocker.GetMock<IRemoteAlbumAggregationService>()
                  .Verify(v => v.Augment(It.Is<RemoteAlbum>(r => r.Artist == _artist)), Times.AtLeastOnce());
        }

        [Test]
        public void should_return_the_oldest_pending_release_for_the_albums()
        {
            var older = GivenHeldRelease("older", "idx", _release.PublishDate);
            var newer = GivenHeldRelease("newer", "idx", _release.PublishDate);
            older.Release.PublishDate = DateTime.UtcNow.AddHours(-10);
            newer.Release.PublishDate = DateTime.UtcNow.AddHours(-1);

            LoadCache();

            Subject.OldestPendingRelease(_artist.Id, new[] { _album.Id }).Release.Title.Should().Be("older");
        }

        [Test]
        public void should_return_no_oldest_release_for_other_albums()
        {
            GivenHeldRelease("a", "idx", _release.PublishDate);

            Subject.OldestPendingRelease(_artist.Id, new[] { 9999 }).Should().BeNull();
        }

        [Test]
        public void should_not_queue_fallback_releases()
        {
            GivenHeldRelease("a", "idx", _release.PublishDate, PendingReleaseReason.Fallback);

            Subject.GetPendingQueue().Should().BeEmpty();
        }

        [Test]
        public void should_queue_a_pending_release_as_a_queue_item_of_the_release()
        {
            var held = GivenHeldRelease("a", "idx", _release.PublishDate, PendingReleaseReason.Delay);

            var item = Subject.GetPendingQueue().Should().ContainSingle().Subject;

            item.Id.Should().Be(HashConverter.GetHashInt31(string.Format("pending-{0}", held.Id)));
            item.Title.Should().Be("a");
            item.Status.Should().Be(QueueStatus.Delay);
            item.RemoteRelease.Should().BeOfType<RemoteAlbum>();
            item.GetArtist().Should().BeSameAs(_artist);
            item.GetRemoteAlbum().Albums.Should().ContainSingle();
            item.Quality.Should().BeEquivalentTo(held.ParsedInfo.Quality.ToDisplayModel());
            item.Added.Should().Be(held.Added);
            item.Indexer.Should().Be("idx");
            item.ErrorMessage.Should().BeNull();
        }

        [Test]
        public void should_report_a_status_for_a_download_client_that_is_unavailable()
        {
            GivenHeldRelease("a", "idx", _release.PublishDate, PendingReleaseReason.DownloadClientUnavailable);

            Subject.GetPendingQueue().Single().Status.Should().Be(QueueStatus.DownloadClientUnavailable);
        }

        [Test]
        public void should_wait_for_the_next_rss_sync_when_the_delay_has_passed()
        {
            var next = DateTime.UtcNow.AddMinutes(7);

            Mocker.GetMock<ITaskManager>()
                  .Setup(v => v.GetNextExecution(typeof(RssSyncCommand)))
                  .Returns(next);

            GivenHeldRelease("a", "idx", DateTime.UtcNow.AddDays(-1));

            Subject.GetPendingQueue().Single().EstimatedCompletionTime.Should().Be(next);
        }

        [Test]
        public void should_add_the_rss_interval_when_the_delay_ends_after_the_next_sync()
        {
            Mocker.GetMock<ITaskManager>()
                  .Setup(v => v.GetNextExecution(typeof(RssSyncCommand)))
                  .Returns(DateTime.UtcNow.AddMinutes(1));

            Mocker.GetMock<IConfigService>()
                  .SetupGet(v => v.RssSyncInterval)
                  .Returns(15);

            Mocker.GetMock<IDelayProfileService>()
                  .Setup(s => s.AllForTags(It.IsAny<HashSet<int>>()))
                  .Returns(new List<DelayProfile> { new DelayProfile { UsenetDelay = 120, TorrentDelay = 120 } });

            var published = DateTime.UtcNow;

            GivenHeldRelease("a", "idx", published);
            _heldReleases[0].Release.DownloadProtocol = DownloadProtocol.Usenet;

            LoadCache();

            Subject.GetPendingQueue().Single().EstimatedCompletionTime.Should().BeCloseTo(published.AddMinutes(135), TimeSpan.FromSeconds(1));
        }

        [Test]
        public void should_not_queue_a_release_without_albums()
        {
            GivenHeldRelease("a", "idx", _release.PublishDate);

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetAlbums(It.IsAny<ParsedAlbumInfo>(), It.IsAny<Artist>(), null))
                  .Returns(new List<Album>());

            LoadCache();

            Subject.GetPendingQueue().Should().BeEmpty("a release without albums has no album group to dedupe on");
        }

        [Test]
        public void should_return_the_best_quality_release_for_each_group_of_albums()
        {
            GivenHeldRelease("192", "idx", _release.PublishDate, quality: new AudioQualityModel(AudioQuality.MP3_192));
            GivenHeldRelease("flac", "idx", _release.PublishDate, quality: new AudioQualityModel(AudioQuality.FLAC));
            GivenHeldRelease("256", "idx", _release.PublishDate, quality: new AudioQualityModel(AudioQuality.MP3_256));

            Subject.GetPendingQueue().Should().ContainSingle().Which.Title.Should().Be("flac");
        }

        [Test]
        public void should_group_the_releases_by_the_same_albums_in_any_order()
        {
            GivenHeldRelease("192", "idx", _release.PublishDate, quality: new AudioQualityModel(AudioQuality.MP3_192), albumIds: new[] { 1, 2 });
            GivenHeldRelease("flac", "idx", _release.PublishDate, quality: new AudioQualityModel(AudioQuality.FLAC), albumIds: new[] { 2, 1 });

            Subject.GetPendingQueue().Should().ContainSingle().Which.Title.Should().Be("flac");
        }

        [Test]
        public void should_keep_a_release_for_each_different_group_of_albums()
        {
            GivenHeldRelease("one", "idx", _release.PublishDate, albumIds: new[] { 1 });
            GivenHeldRelease("two", "idx", _release.PublishDate, albumIds: new[] { 2 });
            GivenHeldRelease("both", "idx", _release.PublishDate, albumIds: new[] { 1, 2 });

            Subject.GetPendingQueue().Select(q => q.Title).Should().BeEquivalentTo("one", "two", "both");
        }

        [Test]
        public void should_prefer_the_preferred_protocol_when_the_quality_is_the_same()
        {
            Mocker.GetMock<IDelayProfileService>()
                  .Setup(s => s.BestForTags(It.IsAny<HashSet<int>>()))
                  .Returns(new DelayProfile { PreferredProtocol = DownloadProtocol.Torrent });

            var usenet = GivenHeldRelease("usenet", "idx", _release.PublishDate);
            var torrent = GivenHeldRelease("torrent", "idx", _release.PublishDate);
            usenet.Release.DownloadProtocol = DownloadProtocol.Usenet;
            torrent.Release.DownloadProtocol = DownloadProtocol.Torrent;

            LoadCache();

            Subject.GetPendingQueue().Should().ContainSingle().Which.Title.Should().Be("torrent");
        }

        [Test]
        public void should_find_a_queue_item_by_id()
        {
            var held = GivenHeldRelease("a", "idx", _release.PublishDate);

            Subject.FindPendingQueueItem(HashConverter.GetHashInt31(string.Format("pending-{0}", held.Id))).Title.Should().Be("a");
            Subject.FindPendingQueueItem(12345).Should().BeNull();
        }
    }

    [TestFixture]
    public class PendingReleaseEventsFixture : PendingReleaseServiceFixtureBase
    {
        private int Updates => Mocker.GetMock<IEventAggregator>().Invocations.Count(i => i.Method.Name == nameof(IEventAggregator.PublishEvent) && i.Arguments[0] is PendingReleasesUpdatedEvent);

        [SetUp]
        public void SetUp()
        {
            Mocker.GetMock<IEventAggregator>().Invocations.Clear();
        }

        [Test]
        public void should_delete_the_pending_releases_of_deleted_artists()
        {
            Subject.Handle(new ArtistsDeletedEvent(new List<Artist> { _artist, new Artist { Id = 77 } }, false, false));

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Verify(v => v.DeleteByArtistIds(It.Is<List<int>>(l => l.SequenceEqual(new[] { _artist.Id, 77 }))), Times.Once());
            Updates.Should().Be(1);
        }

        [Test]
        public void should_reload_when_an_artist_is_edited()
        {
            Subject.Handle(new ArtistEditedEvent(_artist, _artist));

            Updates.Should().Be(1);
        }

        [Test]
        public void should_reload_when_an_artist_is_updated()
        {
            Subject.Handle(new ArtistUpdatedEvent(_artist));

            Updates.Should().Be(1);
        }

        [Test]
        public void should_reload_when_the_config_is_saved()
        {
            Subject.Handle(new ConfigSavedEvent());

            Updates.Should().Be(1);
        }
    }
}
