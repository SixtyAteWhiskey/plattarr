using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.History;
using NzbDrone.Core.Movies.Search;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MovieTests.SearchTests
{
    [TestFixture]
    public class ProactiveMovieUpgradeServiceFixture : CoreTest<ProactiveMovieUpgradeService>
    {
        private readonly List<ProactiveMovieUpgradeActivity> _runs = new();

        [SetUp]
        public void Setup()
        {
            _runs.Clear();
            Mocker.GetMock<IConfigService>().SetupGet(x => x.ProactiveMovieUpgradeActiveDownloadLimit).Returns(5);
            Mocker.GetMock<IConfigService>().SetupGet(x => x.ProactiveMovieUpgradeRecentGrabHours).Returns(24);
            Mocker.GetMock<IProactiveMovieUpgradeActivityRepository>()
                .Setup(x => x.Insert(It.IsAny<ProactiveMovieUpgradeActivity>()))
                .Callback<ProactiveMovieUpgradeActivity>(run => _runs.Add(run));
            Mocker.GetMock<IQueueService>().Setup(x => x.GetQueue()).Returns(new List<Queue.Queue>());
            Mocker.GetMock<IHistoryService>()
                .Setup(x => x.Since(It.IsAny<DateTime>(), MovieHistoryEventType.DownloadFailed))
                .Returns(new List<MovieHistory>());
            Mocker.GetMock<IHistoryService>()
                .Setup(x => x.Since(It.IsAny<DateTime>(), MovieHistoryEventType.Grabbed))
                .Returns(new List<MovieHistory>());
            Mocker.GetMock<IMovieCutoffService>()
                .Setup(x => x.MoviesWhereCutoffUnmet(It.IsAny<PagingSpec<Movie>>()))
                .Returns(new PagingSpec<Movie>
                {
                    Records = new List<Movie> { Movie(11), Movie(12) }
                });
            Mocker.GetMock<ISearchForReleases>()
                .Setup(x => x.MovieSearch(It.IsAny<int>(), false, false))
                .ReturnsAsync(new List<DownloadDecision>());
            Mocker.GetMock<IProcessDownloadDecisions>()
                .Setup(x => x.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                .ReturnsAsync(new ProcessedDecisions(new List<DownloadDecision>(), new List<DownloadDecision>(), new List<DownloadDecision>()));
        }

        private static Movie Movie(int id)
        {
            return new Movie { Id = id, Monitored = true, MinimumAvailability = MovieStatusType.Announced, MovieFileId = id };
        }

        [Test]
        public void disabled_run_records_reason_without_searching()
        {
            Subject.Execute(new ProactiveMovieUpgradeCommand());

            _runs.Should().ContainSingle().Which.Outcome.Should().Be("disabled");
            Mocker.GetMock<IMovieCutoffService>().Verify(x => x.MoviesWhereCutoffUnmet(It.IsAny<PagingSpec<Movie>>()), Times.Never());
        }

        [Test]
        public void enabled_run_searches_only_one_cutoff_unmet_movie()
        {
            Mocker.GetMock<IConfigService>().SetupGet(x => x.ProactiveMovieUpgradeEnabled).Returns(true);

            Subject.Execute(new ProactiveMovieUpgradeCommand());

            Mocker.GetMock<ISearchForReleases>().Verify(x => x.MovieSearch(It.IsAny<int>(), false, false), Times.Once());
            _runs.Should().ContainSingle().Which.MovieId.Should().Be(11);
            _runs[0].Outcome.Should().Be("no-grab");
        }

        [Test]
        public void active_limit_pauses_before_search()
        {
            Mocker.GetMock<IConfigService>().SetupGet(x => x.ProactiveMovieUpgradeEnabled).Returns(true);
            Mocker.GetMock<IConfigService>().SetupGet(x => x.ProactiveMovieUpgradeActiveDownloadLimit).Returns(1);
            Mocker.GetMock<IQueueService>().Setup(x => x.GetQueue()).Returns(new List<Queue.Queue>
            {
                new() { Id = 7, DownloadId = "other", Status = QueueStatus.Downloading }
            });

            Subject.Execute(new ProactiveMovieUpgradeCommand());

            _runs.Should().ContainSingle().Which.Outcome.Should().Be("paused");
            Mocker.GetMock<ISearchForReleases>().Verify(x => x.MovieSearch(It.IsAny<int>(), false, false), Times.Never());
        }

        [Test]
        public void recent_grab_skips_movie_and_searches_the_next()
        {
            Mocker.GetMock<IConfigService>().SetupGet(x => x.ProactiveMovieUpgradeEnabled).Returns(true);
            Mocker.GetMock<IHistoryService>()
                .Setup(x => x.Since(It.IsAny<DateTime>(), MovieHistoryEventType.Grabbed))
                .Returns(new List<MovieHistory> { new() { ParentId = 11, Date = DateTime.UtcNow } });

            Subject.Execute(new ProactiveMovieUpgradeCommand());

            _runs.Should().ContainSingle().Which.MovieId.Should().Be(12);
            Mocker.GetMock<ISearchForReleases>().Verify(x => x.MovieSearch(11, false, false), Times.Never());
        }
    }
}
