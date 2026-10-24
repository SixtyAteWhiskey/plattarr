using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Crypto;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Pending;
using NzbDrone.Core.Music.Search;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Test.MusicTests.PendingTests
{
    [TestFixture]
    public class RemoveGrabbedFixture : PendingReleaseServiceFixtureBase
    {
        [Test]
        public void should_delete_if_the_grabbed_quality_is_the_same()
        {
            GivenHeldRelease("a", "idx", _release.PublishDate, quality: _parsedAlbumInfo.Quality);

            Subject.Handle(new ReleaseGrabbedEvent(_remoteAlbum));

            VerifyDelete(Times.Once());
        }

        [Test]
        public void should_delete_if_the_grabbed_quality_is_the_higher()
        {
            GivenHeldRelease("a", "idx", _release.PublishDate, quality: new AudioQualityModel(AudioQuality.MP3_192));

            Subject.Handle(new ReleaseGrabbedEvent(_remoteAlbum));

            VerifyDelete(Times.Once());
        }

        [Test]
        public void should_not_delete_if_the_grabbed_quality_is_the_lower()
        {
            GivenHeldRelease("a", "idx", _release.PublishDate, quality: new AudioQualityModel(AudioQuality.FLAC));

            Subject.Handle(new ReleaseGrabbedEvent(_remoteAlbum));

            VerifyDelete(Times.Never());
        }

        [Test]
        public void should_only_delete_the_releases_for_the_grabbed_albums()
        {
            GivenHeldRelease("a", "idx", _release.PublishDate);

            _remoteAlbum.Albums = new List<Album> { new Album { Id = 555, ArtistId = _artist.Id } };

            Subject.Handle(new ReleaseGrabbedEvent(_remoteAlbum));

            VerifyDelete(Times.Never());
        }

        [Test]
        public void should_only_delete_the_releases_for_the_grabbed_artist()
        {
            GivenHeldRelease("a", "idx", _release.PublishDate);

            _remoteAlbum.Artist = new Artist { Id = 99, QualityProfile = _artist.QualityProfile };

            Subject.Handle(new ReleaseGrabbedEvent(_remoteAlbum));

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
            Mocker.GetMock<IEventAggregator>().Invocations.Clear();

            Subject.Handle(new ReleaseGrabbedEvent(_remoteAlbum));

            Mocker.GetMock<IEventAggregator>()
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
            var remote = new RemoteAlbum { Artist = _artist, Albums = _remoteAlbum.Albums, ParsedAlbumInfo = _parsedAlbumInfo, Release = release };

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
            Mocker.GetMock<IEventAggregator>().Invocations.Clear();

            Handle();

            Mocker.GetMock<IEventAggregator>()
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
        public void should_remove_the_releases_for_the_same_album()
        {
            var first = GivenHeldRelease("a", "idx", _release.PublishDate);
            var second = GivenHeldRelease("b", "idx", _release.PublishDate);

            Subject.RemovePendingQueueItems(QueueId(first));

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Verify(c => c.DeleteMany(It.Is<IEnumerable<int>>(s => s.OrderBy(i => i).SequenceEqual(new[] { first.Id, second.Id }))), Times.Once());
        }

        [Test]
        public void should_keep_the_releases_for_other_albums()
        {
            var first = GivenHeldRelease("a", "idx", _release.PublishDate);
            GivenHeldRelease("b", "idx", _release.PublishDate, albumTitle: "Other Album");

            Subject.RemovePendingQueueItems(QueueId(first));

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Verify(c => c.DeleteMany(It.Is<IEnumerable<int>>(s => s.SequenceEqual(new[] { first.Id }))), Times.Once());
        }

        [Test]
        public void should_keep_the_releases_of_other_artists()
        {
            var first = GivenHeldRelease("a", "idx", _release.PublishDate);
            var other = GivenHeldRelease("b", "idx", _release.PublishDate);
            other.ParentId = 99;

            Subject.RemovePendingQueueItems(QueueId(first));

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Verify(c => c.DeleteMany(It.Is<IEnumerable<int>>(s => s.SequenceEqual(new[] { first.Id }))), Times.Once());
        }
    }
}
