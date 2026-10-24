using System;
using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers.Overrides;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.DecisionEngine.Specifications;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MusicTests.DecisionEngineTests
{
    [TestFixture]
    public class EarlyReleaseSpecificationFixture : TestBase<EarlyReleaseSpecification>
    {
        private Artist _artist;
        private Album _album1;
        private Album _album2;
        private RemoteAlbum _remoteAlbum;
        private IndexerOverride _override;

        [SetUp]
        public void Setup()
        {
            _artist = Builder<Artist>.CreateNew().With(s => s.Id = 1).Build();
            _album1 = Builder<Album>.CreateNew().With(s => s.ReleaseDate = DateTime.Today).Build();
            _album2 = Builder<Album>.CreateNew().With(s => s.ReleaseDate = DateTime.Today).Build();

            _remoteAlbum = new RemoteAlbum
            {
                Artist = _artist,
                Albums = new List<Album> { _album1 },
                Release = new TorrentInfo
                {
                    IndexerId = 1,
                    Title = "Artist - Album [FLAC-RlsGrp]",
                    PublishDate = DateTime.Today
                }
            };

            _override = new IndexerOverride { IndexerId = 1, MediaKind = MediaKind.Music, EarlyReleaseLimit = 5 };

            Mocker.GetMock<IIndexerOverrideService>()
                  .Setup(v => v.Get(1, MediaKind.Music))
                  .Returns(_override);
        }

        private void GivenPublishDateFromToday(int days)
        {
            _remoteAlbum.Release.PublishDate = DateTime.Today.AddDays(days);
        }

        [Test]
        public void should_return_true_if_indexer_not_specified()
        {
            _remoteAlbum.Release.IndexerId = 0;

            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_true_if_release_contains_multiple_albums()
        {
            _remoteAlbum.Albums.Add(_album2);

            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_true_if_indexer_has_no_music_override()
        {
            Mocker.GetMock<IIndexerOverrideService>()
                  .Setup(v => v.Get(1, MediaKind.Music))
                  .Returns(new IndexerOverride { IndexerId = 1, MediaKind = MediaKind.Music });

            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [TestCase(-2)]
        [TestCase(-5)]
        public void should_return_true_if_publish_date_above_or_equal_to_limit(int days)
        {
            GivenPublishDateFromToday(days);

            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [TestCase(-10)]
        [TestCase(-20)]
        public void should_return_false_if_publish_date_belove_limit(int days)
        {
            GivenPublishDateFromToday(days);

            var rejection = Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation());

            rejection.Accepted.Should().BeFalse();

            rejection.Reason.Should().Be(DownloadRejectionReason.EarlyRelease);
        }

        [TestCase(-10)]
        [TestCase(-100)]
        public void should_return_true_if_limit_null(int days)
        {
            GivenPublishDateFromToday(days);

            _override.EarlyReleaseLimit = null;

            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }
    }
}
