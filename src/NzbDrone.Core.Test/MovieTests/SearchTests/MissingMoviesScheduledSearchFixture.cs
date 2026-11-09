using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
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
            var service = new MovieSearchService(movieService.Object, new Mock<IMovieCutoffService>().Object,
                search.Object, decisions.Object, queue.Object, history.Object, NLog.LogManager.GetCurrentClassLogger());

            service.Execute(new MissingMoviesSearchCommand { Trigger = CommandTrigger.Scheduled });

            search.Verify(x => x.MovieSearch(4, false, false), Times.Once);
            search.Verify(x => x.MovieSearch(It.IsAny<int>(), false, false), Times.Once);
            foreach (var excluded in new[] { 1, 2, 3, 5, 14 })
            {
                search.Verify(x => x.MovieSearch(excluded, false, false), Times.Never);
            }
        }
    }
}
