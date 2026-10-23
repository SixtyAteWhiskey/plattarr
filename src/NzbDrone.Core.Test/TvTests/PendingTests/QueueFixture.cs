using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Crypto;
using NzbDrone.Core.Configuration.Events;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Aggregation;
using NzbDrone.Core.Tv.Events;
using NzbDrone.Core.Tv.Parser;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Tv.Pending;

namespace NzbDrone.Core.Test.TvTests.PendingTests
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
        public void should_reparse_the_languages_of_a_pending_release_that_has_none()
        {
            var held = GivenHeldRelease("Series.S01E01.FRENCH.720p.HDTV.x264-GRP", "idx", _release.PublishDate);

            var remote = Subject.GetPendingRemoteEpisodes(_series.Id).Single();

            held.ParsedInfo.Languages.Should().Contain(Language.French);
            remote.ParsedEpisodeInfo.Languages.Should().Contain(Language.French);
        }

        [Test]
        public void should_skip_a_pending_release_whose_series_is_gone()
        {
            GivenHeldRelease("a", "idx", _release.PublishDate);
            _heldReleases[0].ParentId = 4242;

            LoadCache();

            Subject.GetPendingRemoteEpisodes(4242).Should().BeEmpty();
            Subject.GetPendingQueue().Should().BeEmpty();
        }

        [Test]
        public void should_map_the_episodes_of_a_pending_release()
        {
            GivenHeldRelease("a", "idx", _release.PublishDate);

            var remote = Subject.GetPendingRemoteEpisodes(_series.Id).Single();

            remote.Series.Should().BeSameAs(_series);
            remote.Episodes.Should().ContainSingle().Which.Id.Should().Be(_episode.Id);
            remote.MappedSeasonNumber.Should().Be(1);

            Mocker.GetMock<IRemoteEpisodeAggregationService>()
                  .Verify(v => v.Augment(It.Is<RemoteEpisode>(r => r.Series == _series)), Times.AtLeastOnce());
        }

        [Test]
        public void should_return_the_oldest_pending_release_for_the_episodes()
        {
            var older = GivenHeldRelease("older", "idx", _release.PublishDate);
            var newer = GivenHeldRelease("newer", "idx", _release.PublishDate);
            older.Release.PublishDate = DateTime.UtcNow.AddHours(-10);
            newer.Release.PublishDate = DateTime.UtcNow.AddHours(-1);

            LoadCache();

            Subject.OldestPendingRelease(_series.Id, new[] { _episode.Id }).Release.Title.Should().Be("older");
        }

        [Test]
        public void should_return_no_oldest_release_for_other_episodes()
        {
            GivenHeldRelease("a", "idx", _release.PublishDate);

            Subject.OldestPendingRelease(_series.Id, new[] { 9999 }).Should().BeNull();
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
            item.RemoteRelease.Should().BeOfType<RemoteEpisode>();
            item.GetSeries().Should().BeSameAs(_series);
            item.GetRemoteEpisode().Episodes.Should().ContainSingle();
            item.Quality.Should().BeEquivalentTo(held.ParsedInfo.Quality);
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
                  .Setup(v => v.GetNextExecution(typeof(NzbDrone.Core.IndexerSearch.RssSyncCommand)))
                  .Returns(next);

            GivenHeldRelease("a", "idx", DateTime.UtcNow.AddDays(-1));

            Subject.GetPendingQueue().Single().EstimatedCompletionTime.Should().Be(next);
        }

        [Test]
        public void should_add_the_rss_interval_when_the_delay_ends_after_the_next_sync()
        {
            Mocker.GetMock<ITaskManager>()
                  .Setup(v => v.GetNextExecution(typeof(NzbDrone.Core.IndexerSearch.RssSyncCommand)))
                  .Returns(DateTime.UtcNow.AddMinutes(1));

            Mocker.GetMock<NzbDrone.Core.Configuration.IConfigService>()
                  .SetupGet(v => v.RssSyncInterval)
                  .Returns(15);

            Mocker.GetMock<IDelayProfileService>()
                  .Setup(s => s.AllForTags(It.IsAny<HashSet<int>>()))
                  .Returns(new List<DelayProfile> { new DelayProfile { UsenetDelay = 120, TorrentDelay = 120 } });

            var published = DateTime.UtcNow;

            GivenHeldRelease("a", "idx", published);
            _heldReleases[0].Release.DownloadProtocol = NzbDrone.Core.Indexers.DownloadProtocol.Usenet;

            LoadCache();

            Subject.GetPendingQueue().Single().EstimatedCompletionTime.Should().BeCloseTo(published.AddMinutes(135), TimeSpan.FromSeconds(1));
        }

        [Test]
        public void should_not_queue_a_release_without_episodes()
        {
            GivenHeldRelease("a", "idx", _release.PublishDate);

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<Series>()))
                  .Returns(new RemoteEpisode { Series = _series, Episodes = new List<Episode>() });

            LoadCache();

            Subject.GetPendingQueue().Should().BeEmpty("a release without episodes has no episode group to dedupe on");
        }

        [Test]
        public void should_return_the_best_quality_release_for_each_group_of_episodes()
        {
            GivenHeldRelease("sd", "idx", _release.PublishDate, quality: new QualityModel(Quality.SDTV));
            GivenHeldRelease("720", "idx", _release.PublishDate, quality: new QualityModel(Quality.WEBDL720p));
            GivenHeldRelease("hdtv", "idx", _release.PublishDate, quality: new QualityModel(Quality.HDTV720p));

            Subject.GetPendingQueue().Should().ContainSingle().Which.Title.Should().Be("720");
        }

        [Test]
        public void should_group_the_releases_by_the_same_episodes_in_any_order()
        {
            GivenHeldRelease("sd", "idx", _release.PublishDate, quality: new QualityModel(Quality.SDTV), episodeIds: new[] { 1, 2 });
            GivenHeldRelease("720", "idx", _release.PublishDate, quality: new QualityModel(Quality.WEBDL720p), episodeIds: new[] { 2, 1 });

            Subject.GetPendingQueue().Should().ContainSingle().Which.Title.Should().Be("720");
        }

        [Test]
        public void should_keep_a_release_for_each_different_group_of_episodes()
        {
            GivenHeldRelease("one", "idx", _release.PublishDate, episodeIds: new[] { 1 });
            GivenHeldRelease("two", "idx", _release.PublishDate, episodeIds: new[] { 2 });
            GivenHeldRelease("both", "idx", _release.PublishDate, episodeIds: new[] { 1, 2 });

            Subject.GetPendingQueue().Select(q => q.Title).Should().BeEquivalentTo("one", "two", "both");
        }

        [Test]
        public void should_prefer_the_preferred_protocol_when_the_quality_is_the_same()
        {
            Mocker.GetMock<IDelayProfileService>()
                  .Setup(s => s.BestForTags(It.IsAny<HashSet<int>>()))
                  .Returns(new DelayProfile { PreferredProtocol = NzbDrone.Core.Indexers.DownloadProtocol.Torrent });

            var usenet = GivenHeldRelease("usenet", "idx", _release.PublishDate);
            var torrent = GivenHeldRelease("torrent", "idx", _release.PublishDate);
            usenet.Release.DownloadProtocol = NzbDrone.Core.Indexers.DownloadProtocol.Usenet;
            torrent.Release.DownloadProtocol = NzbDrone.Core.Indexers.DownloadProtocol.Torrent;

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
        public void should_delete_the_pending_releases_of_deleted_series()
        {
            Subject.Handle(new SeriesDeletedEvent(new List<Series> { _series, new Series { Id = 77 } }, false, false));

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Verify(v => v.DeleteBySeriesIds(It.Is<List<int>>(l => l.SequenceEqual(new[] { _series.Id, 77 }))), Times.Once());
            Updates.Should().Be(1);
        }

        [Test]
        public void should_reload_when_a_series_is_edited()
        {
            Subject.Handle(new SeriesEditedEvent(_series, _series));

            Updates.Should().Be(1);
        }

        [Test]
        public void should_reload_when_a_series_is_updated()
        {
            Subject.Handle(new SeriesUpdatedEvent(_series));

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
