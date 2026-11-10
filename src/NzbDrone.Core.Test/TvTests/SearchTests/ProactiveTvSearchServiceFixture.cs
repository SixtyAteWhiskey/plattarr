using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.History;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Tv.Search;

namespace NzbDrone.Core.Test.TvTests.SearchTests
{
    [TestFixture]
    public class ProactiveTvSearchServiceFixture : CoreTest<ProactiveTvSearchService>
    {
        private readonly List<ProactiveTvSearchActivity> _runs = new();

        [SetUp]
        public void Setup()
        {
            _runs.Clear();
            Mocker.GetMock<IProactiveTvSearchActivityRepository>()
                .Setup(x => x.Insert(It.IsAny<ProactiveTvSearchActivity>()))
                .Callback<ProactiveTvSearchActivity>(run => _runs.Add(run));
            Mocker.GetMock<NzbDrone.Core.Queue.IQueueService>()
                .Setup(x => x.GetQueue()).Returns(new List<NzbDrone.Core.Queue.Queue>());
            Mocker.GetMock<IHistoryService>()
                .Setup(x => x.Since(It.IsAny<DateTime>(), EpisodeHistoryEventType.DownloadFailed))
                .Returns(new List<EpisodeHistory>());
            Mocker.GetMock<ISeriesService>()
                .Setup(x => x.GetAllSeries()).Returns(new List<Series>
                {
                    new() { Id = 1, Title = "Alpha", Monitored = true }
                });
            Mocker.GetMock<IEpisodeService>()
                .Setup(x => x.EpisodesWithoutFiles(It.IsAny<PagingSpec<Episode>>(), true, It.IsAny<HashSet<int>>()))
                .Returns(new PagingSpec<Episode>
                {
                    Records = new List<Episode>
                    {
                        new() { Id = 11, SeriesId = 1, Monitored = true },
                        new() { Id = 12, SeriesId = 1, Monitored = true }
                    }
                });
            Mocker.GetMock<ISearchForReleases>()
                .Setup(x => x.BoundedEpisodeSearch(It.IsAny<Episode>()))
                .ReturnsAsync(new List<DownloadDecision>());
            Mocker.GetMock<IProcessDownloadDecisions>()
                .Setup(x => x.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                .ReturnsAsync(new ProcessedDecisions(new List<DownloadDecision>(), new List<DownloadDecision>(), new List<DownloadDecision>()));
        }

        [Test]
        public void disabled_run_records_reason_without_reading_candidates_or_searching()
        {
            Subject.Execute(new ProactiveTvSearchCommand());

            _runs.Should().ContainSingle().Which.Outcome.Should().Be("disabled");
            Mocker.GetMock<IEpisodeService>().Verify(x => x.EpisodesWithoutFiles(It.IsAny<PagingSpec<Episode>>(), true, It.IsAny<HashSet<int>>()), Times.Never());
            Mocker.GetMock<ISearchForReleases>().Verify(x => x.BoundedEpisodeSearch(It.IsAny<Episode>()), Times.Never());
        }

        [Test]
        public void enabled_run_searches_only_one_episode_and_records_outcome()
        {
            Mocker.GetMock<IConfigService>().SetupGet(x => x.ProactiveTvSearchEnabled).Returns(true);

            Subject.Execute(new ProactiveTvSearchCommand());

            Mocker.GetMock<ISearchForReleases>().Verify(x => x.BoundedEpisodeSearch(It.IsAny<Episode>()), Times.Once());
            _runs.Should().ContainSingle().Which.Outcome.Should().Be("no-grab");
            _runs[0].EpisodeId.Should().Be(11);
            _runs[0].SeriesTitle.Should().Be("Alpha");
        }

        [Test]
        public void monitored_recent_or_retry_limited_episodes_are_not_searched()
        {
            Mocker.GetMock<IConfigService>().SetupGet(x => x.ProactiveTvSearchEnabled).Returns(true);
            Mocker.GetMock<IEpisodeService>()
                .Setup(x => x.EpisodesWithoutFiles(It.IsAny<PagingSpec<Episode>>(), true, It.IsAny<HashSet<int>>()))
                .Returns(new PagingSpec<Episode>
                {
                    Records = new List<Episode>
                    {
                        new() { Id = 11, SeriesId = 1, Monitored = false },
                        new() { Id = 12, SeriesId = 1, Monitored = true, LastSearchTime = DateTime.UtcNow.AddHours(-1) }
                    }
                });

            Subject.Execute(new ProactiveTvSearchCommand());

            _runs.Should().ContainSingle().Which.Outcome.Should().Be("no-candidate");
            Mocker.GetMock<ISearchForReleases>().Verify(x => x.BoundedEpisodeSearch(It.IsAny<Episode>()), Times.Never());
        }

        [Test]
        public void queued_episode_is_skipped_and_only_the_other_episode_is_searched()
        {
            Mocker.GetMock<IConfigService>().SetupGet(x => x.ProactiveTvSearchEnabled).Returns(true);
            Mocker.GetMock<NzbDrone.Core.Queue.IQueueService>().Setup(x => x.GetQueue())
                .Returns(new List<NzbDrone.Core.Queue.Queue>
                {
                    new() { RemoteRelease = new RemoteEpisode { Episodes = new List<Episode> { new() { Id = 11 } } } }
                });

            Subject.Execute(new ProactiveTvSearchCommand());

            _runs.Should().ContainSingle().Which.EpisodeId.Should().Be(12);
            Mocker.GetMock<ISearchForReleases>().Verify(x => x.BoundedEpisodeSearch(It.Is<Episode>(episode => episode.Id == 11)), Times.Never());
        }

        [Test]
        public void three_distinct_failed_downloads_block_the_show()
        {
            var now = DateTime.UtcNow;
            Mocker.GetMock<IConfigService>().SetupGet(x => x.ProactiveTvSearchEnabled).Returns(true);
            Mocker.GetMock<IHistoryService>()
                .Setup(x => x.Since(It.IsAny<DateTime>(), EpisodeHistoryEventType.DownloadFailed))
                .Returns(new List<EpisodeHistory>
                {
                    new() { ParentId = 1, EventType = EpisodeHistoryEventType.DownloadFailed, DownloadId = "a", Date = now },
                    new() { ParentId = 1, EventType = EpisodeHistoryEventType.DownloadFailed, DownloadId = "b", Date = now },
                    new() { ParentId = 1, EventType = EpisodeHistoryEventType.DownloadFailed, DownloadId = "c", Date = now }
                });

            Subject.Execute(new ProactiveTvSearchCommand());

            _runs.Should().ContainSingle().Which.Outcome.Should().Be("no-candidate");
            Mocker.GetMock<ISearchForReleases>().Verify(x => x.BoundedEpisodeSearch(It.IsAny<Episode>()), Times.Never());
        }

        [Test]
        public void failed_search_records_the_attempt_before_failing_the_command()
        {
            Mocker.GetMock<IConfigService>().SetupGet(x => x.ProactiveTvSearchEnabled).Returns(true);
            Mocker.GetMock<ISearchForReleases>()
                .Setup(x => x.BoundedEpisodeSearch(It.IsAny<Episode>()))
                .ThrowsAsync(new InvalidOperationException("provider error"));

            Action run = () => Subject.Execute(new ProactiveTvSearchCommand());

            run.Should().Throw<InvalidOperationException>();
            _runs.Should().ContainSingle().Which.Outcome.Should().Be("failed");
            _runs[0].EpisodeId.Should().Be(11);
        }

        [Test]
        public void repeated_failure_for_one_download_does_not_exhaust_retry_limit()
        {
            var now = DateTime.UtcNow;
            var failures = new[]
            {
                new EpisodeHistory { ParentId = 1, EventType = EpisodeHistoryEventType.DownloadFailed, DownloadId = "same", Date = now },
                new EpisodeHistory { ParentId = 1, EventType = EpisodeHistoryEventType.DownloadFailed, DownloadId = "same", Date = now },
                new EpisodeHistory { ParentId = 1, EventType = EpisodeHistoryEventType.DownloadFailed, DownloadId = "other", Date = now }
            };

            TvProactiveFailurePolicy.LimitedSeries(failures, now).Should().BeEmpty();
            failures[2].DownloadId = "third";
            TvProactiveFailurePolicy.LimitedSeries(failures, now).Should().BeEmpty();
            failures[1].DownloadId = "second";
            TvProactiveFailurePolicy.LimitedSeries(failures, now).Should().Contain(1);
        }
    }
}
