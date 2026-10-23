using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Tv.Pending;

namespace NzbDrone.Core.Test.TvTests.PendingTests
{
    [TestFixture]
    public class AddFixture : PendingReleaseServiceFixtureBase
    {
        [Test]
        public void should_add()
        {
            Subject.Add(_temporarilyRejected, PendingReleaseReason.Delay);

            VerifyInsert();
        }

        [Test]
        public void should_store_the_series_the_parse_the_release_and_how_it_matched()
        {
            PendingRelease inserted = null;

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Setup(v => v.Insert(It.IsAny<PendingRelease>()))
                  .Callback<PendingRelease>(p => inserted = p)
                  .Returns<PendingRelease>(p => p);

            _remoteEpisode.SeriesMatchType = SeriesMatchType.Alias;
            _remoteEpisode.ReleaseSource = ReleaseSourceType.Rss;

            Subject.Add(_temporarilyRejected, PendingReleaseReason.DownloadClientUnavailable);

            inserted.MediaKind.Should().Be(MediaKind.Tv);
            inserted.ParentId.Should().Be(_series.Id);
            inserted.Title.Should().Be(_release.Title);
            inserted.ParsedInfo.Should().BeSameAs(_parsedEpisodeInfo);
            inserted.Release.Should().BeSameAs(_release);
            inserted.Reason.Should().Be(PendingReleaseReason.DownloadClientUnavailable);
            inserted.AdditionalInfo.SeriesMatchType.Should().Be(SeriesMatchType.Alias);
            inserted.AdditionalInfo.ReleaseSource.Should().Be(ReleaseSourceType.Rss);
            inserted.Added.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Test]
        public void should_be_the_tv_pending_handler()
        {
            Subject.Kind.Should().Be(MediaKind.Tv);
        }

        [Test]
        public void should_not_add_if_it_is_the_same_release_from_the_same_indexer()
        {
            GivenHeldRelease(_release.Title, _release.Indexer, _release.PublishDate);

            Subject.Add(_temporarilyRejected, PendingReleaseReason.Delay);

            VerifyNoInsert();
        }

        [Test]
        public void should_not_add_if_it_is_the_same_release_from_the_same_indexer_twice()
        {
            GivenHeldRelease(_release.Title, _release.Indexer, _release.PublishDate, PendingReleaseReason.DownloadClientUnavailable);
            GivenHeldRelease(_release.Title, _release.Indexer, _release.PublishDate, PendingReleaseReason.Fallback);

            Subject.Add(_temporarilyRejected, PendingReleaseReason.Delay);

            VerifyNoInsert();
        }

        [Test]
        public void should_remove_duplicate_if_it_is_the_same_release_from_the_same_indexer_twice()
        {
            GivenHeldRelease(_release.Title, _release.Indexer, _release.PublishDate, PendingReleaseReason.DownloadClientUnavailable);
            var duplicate = GivenHeldRelease(_release.Title, _release.Indexer, _release.PublishDate, PendingReleaseReason.Fallback);

            Subject.Add(_temporarilyRejected, PendingReleaseReason.Fallback);

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Verify(v => v.Delete(duplicate.Id), Times.Once());
            Mocker.GetMock<IPendingReleaseRepository>()
                  .Verify(v => v.Delete(It.IsAny<int>()), Times.Once());
        }

        [Test]
        public void should_change_the_reason_of_the_held_release()
        {
            var held = GivenHeldRelease(_release.Title, _release.Indexer, _release.PublishDate, PendingReleaseReason.Delay);

            Subject.Add(_temporarilyRejected, PendingReleaseReason.Fallback);

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Verify(v => v.Update(It.Is<PendingRelease>(p => p.Id == held.Id && p.Reason == PendingReleaseReason.Fallback)), Times.Once());
            VerifyNoInsert();
        }

        [Test]
        public void should_not_change_the_reason_when_the_download_client_was_unavailable()
        {
            GivenHeldRelease(_release.Title, _release.Indexer, _release.PublishDate, PendingReleaseReason.DownloadClientUnavailable);

            Subject.Add(_temporarilyRejected, PendingReleaseReason.Delay);

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Verify(v => v.Update(It.IsAny<PendingRelease>()), Times.Never());
            VerifyNoInsert();
        }

        [Test]
        public void should_not_touch_the_cached_release_when_changing_the_reason()
        {
            var held = GivenHeldRelease(_release.Title, _release.Indexer, _release.PublishDate, PendingReleaseReason.Delay);

            Subject.Add(_temporarilyRejected, PendingReleaseReason.Fallback);

            // The change is made on a copy, then the cache is rebuilt from the repository.
            held.Reason.Should().Be(PendingReleaseReason.Delay);
        }

        [Test]
        public void should_add_if_title_is_different()
        {
            GivenHeldRelease(_release.Title + "-RP", _release.Indexer, _release.PublishDate);

            Subject.Add(_temporarilyRejected, PendingReleaseReason.Delay);

            VerifyInsert();
        }

        [Test]
        public void should_add_if_indexer_is_different()
        {
            GivenHeldRelease(_release.Title, "AnotherIndexer", _release.PublishDate);

            Subject.Add(_temporarilyRejected, PendingReleaseReason.Delay);

            VerifyInsert();
        }

        [Test]
        public void should_add_if_publish_date_is_different()
        {
            GivenHeldRelease(_release.Title, _release.Indexer, _release.PublishDate.AddHours(1));

            Subject.Add(_temporarilyRejected, PendingReleaseReason.Delay);

            VerifyInsert();
        }

        [Test]
        public void should_not_add_a_release_that_covers_several_episodes_twice()
        {
            _remoteEpisode.Episodes = new List<Episode> { _episode, new Episode { Id = 101, SeriesId = _series.Id, SeasonNumber = 1, EpisodeNumber = 2 } };
            GivenHeldRelease(_release.Title, _release.Indexer, _release.PublishDate);

            Subject.Add(_temporarilyRejected, PendingReleaseReason.Delay);

            VerifyNoInsert();
        }

        [Test]
        public void should_add_a_decision_for_each_series()
        {
            var otherSeries = new Series { Id = 11, Title = "Other", QualityProfile = _profile, Tags = new HashSet<int>() };
            var otherRelease = new ReleaseInfo { Title = "Other.S01E01.720p-GRP", Indexer = _release.Indexer, PublishDate = _release.PublishDate };
            var otherRemote = new RemoteEpisode
            {
                Series = otherSeries,
                Episodes = new List<Episode> { new Episode { Id = 300, SeriesId = otherSeries.Id } },
                ParsedEpisodeInfo = _parsedEpisodeInfo,
                Release = otherRelease
            };

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetSeries(It.IsAny<IEnumerable<int>>()))
                  .Returns(new List<Series>());

            Subject.AddMany(new List<Tuple<DownloadDecision, PendingReleaseReason>>
            {
                Tuple.Create(_temporarilyRejected, PendingReleaseReason.Delay),
                Tuple.Create(new DownloadDecision(otherRemote, new DownloadRejection(DownloadRejectionReason.MinimumAgeDelay, "Temp", RejectionType.Temporary)), PendingReleaseReason.Delay)
            });

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Verify(v => v.Insert(It.Is<PendingRelease>(p => p.ParentId == _series.Id)), Times.Once());
            Mocker.GetMock<IPendingReleaseRepository>()
                  .Verify(v => v.Insert(It.Is<PendingRelease>(p => p.ParentId == otherSeries.Id)), Times.Once());
        }

        [Test]
        public void should_publish_that_the_pending_releases_were_updated()
        {
            // The fixture's own cache load published one already.
            Mocker.GetMock<NzbDrone.Core.Messaging.Events.IEventAggregator>().Invocations.Clear();

            Subject.Add(_temporarilyRejected, PendingReleaseReason.Delay);

            Mocker.GetMock<NzbDrone.Core.Messaging.Events.IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<PendingReleasesUpdatedEvent>()), Times.Once());
        }

        private void VerifyInsert()
        {
            Mocker.GetMock<IPendingReleaseRepository>()
                .Verify(v => v.Insert(It.IsAny<PendingRelease>()), Times.Once());
        }

        private void VerifyNoInsert()
        {
            Mocker.GetMock<IPendingReleaseRepository>()
                .Verify(v => v.Insert(It.IsAny<PendingRelease>()), Times.Never());
        }
    }
}
