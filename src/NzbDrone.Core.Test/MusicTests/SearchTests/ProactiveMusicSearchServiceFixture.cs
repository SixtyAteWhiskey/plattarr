using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.History;
using NzbDrone.Core.Music.Search;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.SearchTests
{
    [TestFixture]
    public class ProactiveMusicSearchServiceFixture : CoreTest<ProactiveMusicSearchService>
    {
        private readonly List<ProactiveMusicSearchActivity> _runs = new();

        [SetUp]
        public void Setup()
        {
            _runs.Clear();
            Mocker.GetMock<IConfigService>().SetupGet(x => x.ProactiveMusicActiveDownloadLimit).Returns(5);
            Mocker.GetMock<IConfigService>().SetupGet(x => x.ProactiveMusicRecentGrabHours).Returns(24);
            Mocker.GetMock<IProactiveMusicSearchActivityRepository>()
                .Setup(x => x.Insert(It.IsAny<ProactiveMusicSearchActivity>()))
                .Callback<ProactiveMusicSearchActivity>(run => _runs.Add(run));
            Mocker.GetMock<IQueueService>().Setup(x => x.GetQueue()).Returns(new List<Queue.Queue>());
            Mocker.GetMock<IHistoryService>()
                .Setup(x => x.Since(It.IsAny<DateTime>(), AlbumHistoryEventType.DownloadFailed))
                .Returns(new List<AlbumHistory>());
            Mocker.GetMock<IHistoryService>()
                .Setup(x => x.Since(It.IsAny<DateTime>(), AlbumHistoryEventType.Grabbed))
                .Returns(new List<AlbumHistory>());
            Mocker.GetMock<IAlbumService>()
                .Setup(x => x.AlbumsWithoutFiles(It.IsAny<PagingSpec<Album>>()))
                .Returns(new PagingSpec<Album>
                {
                    Records = new List<Album> { Album(11), Album(12) }
                });
            Mocker.GetMock<ISearchForReleases>()
                .Setup(x => x.AlbumSearch(It.IsAny<int>(), false, false, false))
                .ReturnsAsync(new List<DownloadDecision>());
            Mocker.GetMock<IProcessDownloadDecisions>()
                .Setup(x => x.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                .ReturnsAsync(new ProcessedDecisions(new List<DownloadDecision>(), new List<DownloadDecision>(), new List<DownloadDecision>()));
        }

        private static Album Album(int id)
        {
            return new Album
            {
                Id = id,
                Title = $"Album {id}",
                Monitored = true,
                Artist = new Artist { Monitored = true },
                ReleaseDate = DateTime.UtcNow.AddDays(-10)
            };
        }

        [Test]
        public void disabled_run_records_reason_without_searching()
        {
            Subject.Execute(new ProactiveMusicSearchCommand());

            _runs.Should().ContainSingle().Which.Outcome.Should().Be("disabled");
            Mocker.GetMock<IAlbumService>().Verify(x => x.AlbumsWithoutFiles(It.IsAny<PagingSpec<Album>>()), Times.Never());
            Mocker.GetMock<ISearchForReleases>().Verify(x => x.AlbumSearch(It.IsAny<int>(), false, false, false), Times.Never());
        }

        [Test]
        public void enabled_run_searches_only_one_album()
        {
            Mocker.GetMock<IConfigService>().SetupGet(x => x.ProactiveMusicSearchEnabled).Returns(true);

            Subject.Execute(new ProactiveMusicSearchCommand());

            Mocker.GetMock<ISearchForReleases>().Verify(x => x.AlbumSearch(It.IsAny<int>(), false, false, false), Times.Once());
            _runs.Should().ContainSingle().Which.AlbumId.Should().Be(11);
            _runs[0].Outcome.Should().Be("no-grab");
        }

        [Test]
        public void active_limit_pauses_before_search()
        {
            Mocker.GetMock<IConfigService>().SetupGet(x => x.ProactiveMusicSearchEnabled).Returns(true);
            Mocker.GetMock<IConfigService>().SetupGet(x => x.ProactiveMusicActiveDownloadLimit).Returns(1);
            Mocker.GetMock<IQueueService>().Setup(x => x.GetQueue()).Returns(new List<Queue.Queue>
            {
                new() { Id = 7, DownloadId = "other", Status = QueueStatus.Downloading }
            });

            Subject.Execute(new ProactiveMusicSearchCommand());

            _runs.Should().ContainSingle().Which.Outcome.Should().Be("paused");
            Mocker.GetMock<ISearchForReleases>().Verify(x => x.AlbumSearch(It.IsAny<int>(), false, false, false), Times.Never());
        }

        [Test]
        public void recent_grab_skips_album_and_searches_the_next()
        {
            Mocker.GetMock<IConfigService>().SetupGet(x => x.ProactiveMusicSearchEnabled).Returns(true);
            Mocker.GetMock<IHistoryService>()
                .Setup(x => x.Since(It.IsAny<DateTime>(), AlbumHistoryEventType.Grabbed))
                .Returns(new List<AlbumHistory> { new() { ItemId = 11, Date = DateTime.UtcNow } });

            Subject.Execute(new ProactiveMusicSearchCommand());

            _runs.Should().ContainSingle().Which.AlbumId.Should().Be(12);
            Mocker.GetMock<ISearchForReleases>().Verify(x => x.AlbumSearch(11, false, false, false), Times.Never());
        }

        [Test]
        public void three_distinct_failures_block_an_album_but_repeated_download_does_not()
        {
            var now = DateTime.UtcNow;
            var album = Album(11);
            var failures = new List<AlbumHistory>
            {
                new() { ItemId = 11, EventType = AlbumHistoryEventType.DownloadFailed, DownloadId = "a", Date = now },
                new() { ItemId = 11, EventType = AlbumHistoryEventType.DownloadFailed, DownloadId = "a", Date = now },
                new() { ItemId = 11, EventType = AlbumHistoryEventType.DownloadFailed, DownloadId = "b", Date = now }
            };

            MusicProactiveEligibility.Evaluate(new[] { album }, Array.Empty<int>(), failures, now).Should()
                .ContainSingle().Which.Reason.Should().Be("eligible");
            failures[1].DownloadId = "c";
            MusicProactiveEligibility.Evaluate(new[] { album }, Array.Empty<int>(), failures, now).Should()
                .ContainSingle().Which.Reason.Should().Be("retry-limit");
        }
    }
}
