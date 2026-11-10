using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.History;
using NzbDrone.Core.Queue;
using NzbDrone.Test.Common;
using Radarr.Api.V3.ProactiveSearch;

namespace NzbDrone.Api.Test.v3.ProactiveSearch
{
    [TestFixture]
    public class ProactiveSearchPreviewControllerFixture : TestBase
    {
        [Test]
        public void preview_reports_eligible_and_skipped_movies_without_searching()
        {
            var movies = new List<Movie>
            {
                new() { Id = 1, Title = "Alpha", Monitored = true, MinimumAvailability = MovieStatusType.Announced },
                new() { Id = 2, Title = "Beta", Monitored = false, MinimumAvailability = MovieStatusType.Announced }
            };
            var movieService = Mocker.GetMock<IMovieService>();
            movieService.Setup(x => x.MoviesWithoutFiles(It.IsAny<PagingSpec<Movie>>()))
                .Returns(new PagingSpec<Movie> { Records = movies });
            var queue = Mocker.GetMock<IQueueService>();
            queue.Setup(x => x.GetQueue()).Returns(new List<NzbDrone.Core.Queue.Queue>());
            var history = Mocker.GetMock<IHistoryService>();
            history.Setup(x => x.Since(It.IsAny<DateTime>(), MovieHistoryEventType.DownloadFailed))
                .Returns(new List<MovieHistory>());

            var config = Mocker.GetMock<IConfigService>();
            config.SetupGet(x => x.ProactiveMovieSearchEnabled).Returns(true);
            config.SetupGet(x => x.ProactiveMovieActiveDownloadLimit).Returns(5);
            config.SetupGet(x => x.ProactiveMovieRecentGrabHours).Returns(24);
            var preview = new ProactiveSearchPreviewController(movieService.Object, queue.Object, history.Object, config.Object)
                .Get(0, 50, null).Value;

            preview.MissingMovies.Should().Be(2);
            preview.NextMovies.Should().ContainSingle().Which.Title.Should().Be("Alpha");
            preview.SkippedMovies.Should().ContainSingle().Which.Reason.Should().Be("unmonitored");
            preview.Reasons["eligible"].Should().Be(1);
        }
    }
}
