using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Crypto;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Tv.Pending;
using NzbDrone.Core.Tv.Search;

namespace NzbDrone.Core.Test.TvTests.PendingTests
{
    [TestFixture]
    public class RemoveGrabbedFixture : PendingReleaseServiceFixtureBase
    {
        [Test]
        public void should_delete_if_the_grabbed_quality_is_the_same()
        {
            GivenHeldRelease("a", "idx", _release.PublishDate, quality: _parsedEpisodeInfo.Quality);

            Subject.Handle(new ReleaseGrabbedEvent(_remoteEpisode));

            VerifyDelete(Times.Once());
        }

        [Test]
        public void should_delete_if_the_grabbed_quality_is_the_higher()
        {
            GivenHeldRelease("a", "idx", _release.PublishDate, quality: new QualityModel(Quality.SDTV));

            Subject.Handle(new ReleaseGrabbedEvent(_remoteEpisode));

            VerifyDelete(Times.Once());
        }

        [Test]
        public void should_not_delete_if_the_grabbed_quality_is_the_lower()
        {
            GivenHeldRelease("a", "idx", _release.PublishDate, quality: new QualityModel(Quality.Bluray720p));

            Subject.Handle(new ReleaseGrabbedEvent(_remoteEpisode));

            VerifyDelete(Times.Never());
        }

        [Test]
        public void should_only_delete_the_releases_for_the_grabbed_episodes()
        {
            GivenHeldRelease("a", "idx", _release.PublishDate);

            _remoteEpisode.Episodes = new List<Episode> { new Episode { Id = 555, SeriesId = _series.Id } };

            Subject.Handle(new ReleaseGrabbedEvent(_remoteEpisode));

            VerifyDelete(Times.Never());
        }

        [Test]
        public void should_only_delete_the_releases_for_the_grabbed_series()
        {
            GivenHeldRelease("a", "idx", _release.PublishDate);

            _remoteEpisode.Series = new Series { Id = 99, QualityProfile = _profile };

            Subject.Handle(new ReleaseGrabbedEvent(_remoteEpisode));

            VerifyDelete(Times.Never());
        }

        [Test]
        public void should_ignore_a_grab_of_another_media_type()
        {
            GivenHeldRelease("a", "idx", _release.PublishDate);

            Subject.Handle(new ReleaseGrabbedEvent(new RemoteMovie { Release = _release }));

            VerifyDelete(Times.Never());
        }

        [Test]
        public void should_reload_the_pending_releases_after_a_grab()
        {
            GivenHeldRelease("a", "idx", _release.PublishDate);
            Mocker.GetMock<NzbDrone.Core.Messaging.Events.IEventAggregator>().Invocations.Clear();

            Subject.Handle(new ReleaseGrabbedEvent(_remoteEpisode));

            Mocker.GetMock<NzbDrone.Core.Messaging.Events.IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<PendingReleasesUpdatedEvent>()), Times.Once());
        }

        private void VerifyDelete(Times times)
        {
            Mocker.GetMock<IPendingReleaseRepository>()
                .Verify(v => v.Delete(It.IsAny<PendingRelease>()), times);
        }
    }

    [TestFixture]
    public class RemoveRejectedFixture : PendingReleaseServiceFixtureBase
    {
        private DownloadDecision Rejected(ReleaseInfo release)
        {
            var remote = new RemoteEpisode { Series = _series, Episodes = _remoteEpisode.Episodes, ParsedEpisodeInfo = _parsedEpisodeInfo, Release = release };

            return new DownloadDecision(remote, new DownloadRejection(DownloadRejectionReason.MinimumAgeDelay, "Rejected", RejectionType.Permanent));
        }

        private void Handle(params DownloadDecision[] rejected)
        {
            Subject.Handle(new RssSyncCompleteEvent(new ProcessedDecisions(new List<DownloadDecision>(), new List<DownloadDecision>(), rejected.ToList())));
        }

        [Test]
        public void should_delete_the_pending_release_that_was_rejected()
        {
            var held = GivenHeldRelease(_release.Title, _release.Indexer, _release.PublishDate);

            Handle(Rejected(_release));

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Verify(v => v.Delete(It.Is<PendingRelease>(p => p.Id == held.Id)), Times.Once());
        }

        [Test]
        public void should_not_delete_when_the_title_is_different()
        {
            GivenHeldRelease(_release.Title + "-RP", _release.Indexer, _release.PublishDate);

            Handle(Rejected(_release));

            VerifyNoDelete();
        }

        [Test]
        public void should_not_delete_when_the_indexer_is_different()
        {
            GivenHeldRelease(_release.Title, "AnotherIndexer", _release.PublishDate);

            Handle(Rejected(_release));

            VerifyNoDelete();
        }

        [Test]
        public void should_not_delete_when_the_publish_date_is_different()
        {
            GivenHeldRelease(_release.Title, _release.Indexer, _release.PublishDate.AddHours(1));

            Handle(Rejected(_release));

            VerifyNoDelete();
        }

        [Test]
        public void should_reload_the_pending_releases_after_a_sync()
        {
            Mocker.GetMock<NzbDrone.Core.Messaging.Events.IEventAggregator>().Invocations.Clear();

            Handle();

            Mocker.GetMock<NzbDrone.Core.Messaging.Events.IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<PendingReleasesUpdatedEvent>()), Times.Once());
        }

        private void VerifyNoDelete()
        {
            Mocker.GetMock<IPendingReleaseRepository>()
                .Verify(v => v.Delete(It.IsAny<PendingRelease>()), Times.Never());
        }
    }

    [TestFixture]
    public class RemovePendingFixture : PendingReleaseServiceFixtureBase
    {
        private static int QueueId(PendingRelease held) => HashConverter.GetHashInt31(string.Format("pending-{0}", held.Id));

        [Test]
        public void should_remove_the_releases_for_the_same_season_and_episodes()
        {
            var first = GivenHeldRelease("a", "idx", _release.PublishDate);
            var second = GivenHeldRelease("b", "idx", _release.PublishDate);

            Subject.RemovePendingQueueItems(QueueId(first));

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Verify(c => c.DeleteMany(It.Is<IEnumerable<int>>(s => s.OrderBy(i => i).SequenceEqual(new[] { first.Id, second.Id }))), Times.Once());
        }

        [Test]
        public void should_keep_the_releases_for_other_episodes()
        {
            var first = GivenHeldRelease("a", "idx", _release.PublishDate);
            var other = GivenHeldRelease("b", "idx", _release.PublishDate);
            other.ParsedInfo.EpisodeNumbers = new[] { 2 };

            Subject.RemovePendingQueueItems(QueueId(first));

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Verify(c => c.DeleteMany(It.Is<IEnumerable<int>>(s => s.SequenceEqual(new[] { first.Id }))), Times.Once());
        }

        [Test]
        public void should_keep_the_releases_for_other_seasons()
        {
            var first = GivenHeldRelease("a", "idx", _release.PublishDate);
            var other = GivenHeldRelease("b", "idx", _release.PublishDate);
            other.ParsedInfo.SeasonNumbers = new List<int> { 2 };

            Subject.RemovePendingQueueItems(QueueId(first));

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Verify(c => c.DeleteMany(It.Is<IEnumerable<int>>(s => s.SequenceEqual(new[] { first.Id }))), Times.Once());
        }
    }
}
