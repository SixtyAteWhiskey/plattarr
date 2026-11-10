using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.History;
using NzbDrone.Core.Movies.Search;
using NzbDrone.Core.Queue;

namespace NzbDrone.Core.Test.MovieTests.SearchTests
{
    [TestFixture]
    public class MissingMoviesScheduledSearchFixture
    {
        private readonly Mock<IProactiveMovieSearchActivityRepository> _activity = new();
        private readonly List<ProactiveMovieSearchActivity> _runs = new();

        [SetUp]
        public void Setup()
        {
            _runs.Clear();
            _activity.Setup(x => x.Insert(It.IsAny<ProactiveMovieSearchActivity>()))
                .Callback<ProactiveMovieSearchActivity>(run => _runs.Add(run));
        }

        [Test]
        public void scheduled_search_skips_queued_recently_searched_and_repeatedly_failed_movies_and_searches_one_item()
        {
            var now = DateTime.UtcNow;
            var movies = Enumerable.Range(1, 14).Select(id => new Movie
            {
                Id = id,
                Monitored = true,
                MinimumAvailability = MovieStatusType.Announced,
                LastSearchTime = id == 3 ? now.AddHours(-1) : null
            }).ToList();
            var movieService = new Mock<IMovieService>();
            movieService.Setup(x => x.MoviesWithoutFiles(It.IsAny<PagingSpec<Movie>>()))
                .Returns(new PagingSpec<Movie> { Records = movies });
            var history = new Mock<IHistoryService>();
            history.Setup(x => x.Since(It.IsAny<DateTime>(), MovieHistoryEventType.DownloadFailed))
                .Returns(Enumerable.Range(1, 3).Select(id => new MovieHistory
                {
                    ParentId = 1,
                    EventType = MovieHistoryEventType.DownloadFailed,
                    DownloadId = id.ToString(),
                    Date = now.AddHours(-id)
                }).ToList());
            var queue = new Mock<IQueueService>();
            queue.Setup(x => x.GetQueue()).Returns(new List<NzbDrone.Core.Queue.Queue>
            {
                new() { RemoteRelease = new NzbDrone.Core.Movies.Parser.Model.RemoteMovie { Movie = movies[1] } }
            });
            var search = new Mock<ISearchForReleases>();
            search.Setup(x => x.MovieSearch(It.IsAny<int>(), false, false))
                .ReturnsAsync(new List<DownloadDecision>());
            var decisions = new Mock<IProcessDownloadDecisions>();
            decisions.Setup(x => x.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                .ReturnsAsync(new ProcessedDecisions(new List<DownloadDecision>(), new List<DownloadDecision>(), new List<DownloadDecision>()));
            var config = new Mock<IConfigService>();
            config.SetupGet(x => x.ProactiveMovieSearchEnabled).Returns(true);
            var service = new MovieSearchService(
                movieService.Object,
                new Mock<IMovieCutoffService>().Object,
                search.Object,
                decisions.Object,
                queue.Object,
                history.Object,
                config.Object,
                _activity.Object,
                NLog.LogManager.GetCurrentClassLogger());

            service.Execute(new MissingMoviesSearchCommand { Trigger = CommandTrigger.Scheduled });

            search.Verify(x => x.MovieSearch(4, false, false), Times.Once);
            search.Verify(x => x.MovieSearch(It.IsAny<int>(), false, false), Times.Once);
            Assert.That(_runs, Has.Count.EqualTo(1));
            Assert.That(_runs[0].MovieId, Is.EqualTo(4));
            Assert.That(_runs[0].MissingMovies, Is.EqualTo(14));
            Assert.That(_runs[0].EligibleMovies, Is.EqualTo(11));
            Assert.That(_runs[0].Outcome, Is.EqualTo("no-grab"));
            Assert.That(_runs[0].EndedAt, Is.GreaterThanOrEqualTo(_runs[0].StartedAt));
            foreach (var excluded in new[] { 1, 2, 3, 5, 14 })
            {
                search.Verify(x => x.MovieSearch(excluded, false, false), Times.Never);
            }
        }

        [Test]
        public void disabled_scheduled_search_does_not_read_candidates_or_search()
        {
            var movieService = new Mock<IMovieService>();
            var search = new Mock<ISearchForReleases>();
            var service = new MovieSearchService(
                movieService.Object,
                new Mock<IMovieCutoffService>().Object,
                search.Object,
                new Mock<IProcessDownloadDecisions>().Object,
                new Mock<IQueueService>().Object,
                new Mock<IHistoryService>().Object,
                new Mock<IConfigService>().Object,
                _activity.Object,
                NLog.LogManager.GetCurrentClassLogger());

            service.Execute(new MissingMoviesSearchCommand { Trigger = CommandTrigger.Scheduled });

            movieService.Verify(x => x.MoviesWithoutFiles(It.IsAny<PagingSpec<Movie>>()), Times.Never);
            search.Verify(x => x.MovieSearch(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
            Assert.That(_runs, Has.Count.EqualTo(1));
            Assert.That(_runs[0].Outcome, Is.EqualTo("disabled"));
        }

        [Test]
        public void manual_missing_search_still_runs_when_scheduled_search_is_disabled()
        {
            var movieService = new Mock<IMovieService>();
            movieService.Setup(x => x.MoviesWithoutFiles(It.IsAny<PagingSpec<Movie>>()))
                .Returns(new PagingSpec<Movie>
                {
                    Records = new List<Movie>
                    {
                        new() { Id = 7, Monitored = true, MinimumAvailability = MovieStatusType.Announced }
                    }
                });
            var queue = new Mock<IQueueService>();
            queue.Setup(x => x.GetQueue()).Returns(new List<NzbDrone.Core.Queue.Queue>());
            var search = new Mock<ISearchForReleases>();
            search.Setup(x => x.MovieSearch(7, true, false)).ReturnsAsync(new List<DownloadDecision>());
            var decisions = new Mock<IProcessDownloadDecisions>();
            decisions.Setup(x => x.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                .ReturnsAsync(new ProcessedDecisions(new List<DownloadDecision>(), new List<DownloadDecision>(), new List<DownloadDecision>()));
            var service = new MovieSearchService(
                movieService.Object,
                new Mock<IMovieCutoffService>().Object,
                search.Object,
                decisions.Object,
                queue.Object,
                new Mock<IHistoryService>().Object,
                new Mock<IConfigService>().Object,
                _activity.Object,
                NLog.LogManager.GetCurrentClassLogger());

            service.Execute(new MissingMoviesSearchCommand { Trigger = CommandTrigger.Manual });

            search.Verify(x => x.MovieSearch(7, true, false), Times.Once);
            Assert.That(_runs, Is.Empty);
        }

        [Test]
        public void enabled_run_with_no_candidates_is_recorded()
        {
            var movies = new List<Movie> { new() { Id = 7, Monitored = true, MinimumAvailability = MovieStatusType.Announced, LastSearchTime = DateTime.UtcNow } };
            var movieService = new Mock<IMovieService>();
            movieService.Setup(x => x.MoviesWithoutFiles(It.IsAny<PagingSpec<Movie>>()))
                .Returns(new PagingSpec<Movie> { Records = movies });
            var queue = new Mock<IQueueService>();
            queue.Setup(x => x.GetQueue()).Returns(new List<NzbDrone.Core.Queue.Queue>());
            var history = new Mock<IHistoryService>();
            history.Setup(x => x.Since(It.IsAny<DateTime>(), MovieHistoryEventType.DownloadFailed))
                .Returns(new List<MovieHistory>());
            var config = new Mock<IConfigService>();
            config.SetupGet(x => x.ProactiveMovieSearchEnabled).Returns(true);
            var search = new Mock<ISearchForReleases>();
            var service = new MovieSearchService(movieService.Object, new Mock<IMovieCutoffService>().Object,
                search.Object, new Mock<IProcessDownloadDecisions>().Object, queue.Object, history.Object,
                config.Object, _activity.Object, NLog.LogManager.GetCurrentClassLogger());

            service.Execute(new MissingMoviesSearchCommand { Trigger = CommandTrigger.Scheduled });

            Assert.That(_runs, Has.Count.EqualTo(1));
            Assert.That(_runs[0].Outcome, Is.EqualTo("no-candidate"));
            Assert.That(_runs[0].EligibleMovies, Is.Zero);
            search.Verify(x => x.MovieSearch(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
        }

        [Test]
        public void scheduled_grab_and_provider_failure_are_recorded()
        {
            var movies = new List<Movie> { new() { Id = 7, Title = "Example", Monitored = true, MinimumAvailability = MovieStatusType.Announced } };
            var movieService = new Mock<IMovieService>();
            movieService.Setup(x => x.MoviesWithoutFiles(It.IsAny<PagingSpec<Movie>>()))
                .Returns(new PagingSpec<Movie> { Records = movies });
            var queue = new Mock<IQueueService>();
            queue.Setup(x => x.GetQueue()).Returns(new List<NzbDrone.Core.Queue.Queue>());
            var history = new Mock<IHistoryService>();
            history.Setup(x => x.Since(It.IsAny<DateTime>(), MovieHistoryEventType.DownloadFailed))
                .Returns(new List<MovieHistory>());
            var config = new Mock<IConfigService>();
            config.SetupGet(x => x.ProactiveMovieSearchEnabled).Returns(true);
            var search = new Mock<ISearchForReleases>();
            search.Setup(x => x.MovieSearch(7, false, false)).ReturnsAsync(new List<DownloadDecision>());
            var decisions = new Mock<IProcessDownloadDecisions>();
            decisions.Setup(x => x.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                .ReturnsAsync(new ProcessedDecisions(new List<DownloadDecision> { new(null) }, new List<DownloadDecision>(), new List<DownloadDecision>()));
            var service = new MovieSearchService(movieService.Object, new Mock<IMovieCutoffService>().Object,
                search.Object, decisions.Object, queue.Object, history.Object, config.Object, _activity.Object,
                NLog.LogManager.GetCurrentClassLogger());

            var command = new MissingMoviesSearchCommand { Trigger = CommandTrigger.Scheduled };
            service.Execute(command);

            Assert.That(command.GrabbedCount, Is.EqualTo(1));
            Assert.That(_runs[0].Outcome, Is.EqualTo("grabbed"));
            Assert.That(_runs[0].MovieTitle, Is.EqualTo("Example"));

            search.Setup(x => x.MovieSearch(7, false, false)).ThrowsAsync(new InvalidOperationException("provider error"));
            Assert.Throws<InvalidOperationException>(() => service.Execute(new MissingMoviesSearchCommand { Trigger = CommandTrigger.Scheduled }));
            Assert.That(_runs, Has.Count.EqualTo(2));
            Assert.That(_runs[1].Outcome, Is.EqualTo("failed"));
            Assert.That(_runs[1].ErrorMessage, Is.EqualTo("provider error"));
            Assert.That(_runs[1].MovieId, Is.EqualTo(7));
        }
    }
}
