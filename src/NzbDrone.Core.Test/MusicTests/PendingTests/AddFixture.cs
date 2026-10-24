using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Pending;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Test.MusicTests.PendingTests
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
        public void should_store_the_artist_the_parse_the_release_and_where_it_came_from()
        {
            PendingRelease inserted = null;

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Setup(v => v.Insert(It.IsAny<PendingRelease>()))
                  .Callback<PendingRelease>(p => inserted = p)
                  .Returns<PendingRelease>(p => p);

            _remoteAlbum.ReleaseSource = ReleaseSourceType.Rss;

            Subject.Add(_temporarilyRejected, PendingReleaseReason.DownloadClientUnavailable);

            inserted.MediaKind.Should().Be(MediaKind.Music);
            inserted.ParentId.Should().Be(_artist.Id);
            inserted.Title.Should().Be(_release.Title);
            inserted.ParsedInfo.Should().BeSameAs(_parsedAlbumInfo);
            inserted.Release.Should().BeSameAs(_release);
            inserted.Reason.Should().Be(PendingReleaseReason.DownloadClientUnavailable);
            inserted.AdditionalInfo.ReleaseSource.Should().Be(ReleaseSourceType.Rss);
            inserted.Added.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Test]
        public void should_be_the_music_pending_handler()
        {
            Subject.Kind.Should().Be(MediaKind.Music);
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
        public void should_not_add_a_release_that_covers_several_albums_twice()
        {
            _remoteAlbum.Albums = new List<Album> { _album, new Album { Id = 101, ArtistId = _artist.Id } };
            GivenHeldRelease(_release.Title, _release.Indexer, _release.PublishDate);

            Subject.Add(_temporarilyRejected, PendingReleaseReason.Delay);

            VerifyNoInsert();
        }

        [Test]
        public void should_add_a_decision_for_each_artist()
        {
            var otherArtist = new Artist { Id = 11, Tags = new HashSet<int>() };
            otherArtist.QualityProfile = new LazyLoaded<AudioQualityProfile>(_profile);

            var otherRelease = new ReleaseInfo { Title = "Other - Album - FLAC-GRP", Indexer = _release.Indexer, PublishDate = _release.PublishDate };
            var otherRemote = new RemoteAlbum
            {
                Artist = otherArtist,
                Albums = new List<Album> { new Album { Id = 300, ArtistId = otherArtist.Id } },
                ParsedAlbumInfo = _parsedAlbumInfo,
                Release = otherRelease
            };

            Mocker.GetMock<IArtistService>()
                  .Setup(s => s.GetArtists(It.IsAny<IEnumerable<int>>()))
                  .Returns(new List<Artist>());

            Subject.AddMany(new List<Tuple<DownloadDecision, PendingReleaseReason>>
            {
                Tuple.Create(_temporarilyRejected, PendingReleaseReason.Delay),
                Tuple.Create(new DownloadDecision(otherRemote, new DownloadRejection(DownloadRejectionReason.MinimumAgeDelay, "Temp", RejectionType.Temporary)), PendingReleaseReason.Delay)
            });

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Verify(v => v.Insert(It.Is<PendingRelease>(p => p.ParentId == _artist.Id)), Times.Once());
            Mocker.GetMock<IPendingReleaseRepository>()
                  .Verify(v => v.Insert(It.Is<PendingRelease>(p => p.ParentId == otherArtist.Id)), Times.Once());
        }

        [Test]
        public void should_publish_that_the_pending_releases_were_updated()
        {
            // The fixture's own cache load published one already.
            Mocker.GetMock<IEventAggregator>().Invocations.Clear();

            Subject.Add(_temporarilyRejected, PendingReleaseReason.Delay);

            Mocker.GetMock<IEventAggregator>()
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
