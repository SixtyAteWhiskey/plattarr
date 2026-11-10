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
            var cutoff = Mocker.GetMock<IMovieCutoffService>();
            cutoff.Setup(x => x.MoviesWhereCutoffUnmet(It.IsAny<PagingSpec<Movie>>()))
                .Returns(new PagingSpec<Movie> { Records = new List<Movie>() });
            var queue = Mocker.GetMock<IQueueService>();
            queue.Setup(x => x.GetQueue()).Returns(new List<NzbDrone.Core.Queue.Queue>());
            var history = Mocker.GetMock<IHistoryService>();
            history.Setup(x => x.Since(It.IsAny<DateTime>(), MovieHistoryEventType.DownloadFailed))
                .Returns(new List<MovieHistory>());

            var config = Mocker.GetMock<IConfigService>();
            config.SetupGet(x => x.ProactiveMovieSearchEnabled).Returns(true);
            config.SetupGet(x => x.ProactiveMovieActiveDownloadLimit).Returns(5);
            config.SetupGet(x => x.ProactiveMovieRecentGrabHours).Returns(24);
            var preview = new ProactiveSearchPreviewController(movieService.Object, cutoff.Object, queue.Object, history.Object, config.Object)
                .Get(0, 50, null).Value;

            preview.MissingMovies.Should().Be(2);
            preview.NextMovies.Should().ContainSingle().Which.Title.Should().Be("Alpha");
            preview.SkippedMovies.Should().ContainSingle().Which.Reason.Should().Be("unmonitored");
            preview.Reasons["eligible"].Should().Be(1);
        }

        [Test]
        public void preview_includes_cutoff_unmet_candidates_and_skip_reasons_without_searching()
        {
            var movie = new Movie { Id = 8, Title = "Upgrade", Monitored = true, MinimumAvailability = MovieStatusType.Announced };
            Mocker.GetMock<IMovieService>()
                .Setup(x => x.MoviesWithoutFiles(It.IsAny<PagingSpec<Movie>>()))
                .Returns(new PagingSpec<Movie> { Records = new List<Movie>() });
            Mocker.GetMock<IMovieCutoffService>()
                .Setup(x => x.MoviesWhereCutoffUnmet(It.IsAny<PagingSpec<Movie>>()))
                .Returns(new PagingSpec<Movie> { Records = new List<Movie> { movie } });
            Mocker.GetMock<IQueueService>().Setup(x => x.GetQueue())
                .Returns(new List<NzbDrone.Core.Queue.Queue>
                {
                    new() { RemoteRelease = new NzbDrone.Core.Movies.Parser.Model.RemoteMovie { Movie = movie } }
                });
            Mocker.GetMock<IHistoryService>()
                .Setup(x => x.Since(It.IsAny<DateTime>(), MovieHistoryEventType.DownloadFailed))
                .Returns(new List<MovieHistory>());
            Mocker.GetMock<IConfigService>().SetupGet(x => x.ProactiveMovieRecentGrabHours).Returns(24);

            var preview = new ProactiveSearchPreviewController(
                Mocker.GetMock<IMovieService>().Object,
                Mocker.GetMock<IMovieCutoffService>().Object,
                Mocker.GetMock<IQueueService>().Object,
                Mocker.GetMock<IHistoryService>().Object,
                Mocker.GetMock<IConfigService>().Object).Get().Value;

            preview.MissingMovies.Should().Be(0);
            preview.CutoffUnmetMovies.Should().Be(1);
            preview.SkippedMovies.Should().ContainSingle().Which.CandidateType.Should().Be("cutoff-unmet");
            preview.Reasons["in-queue"].Should().Be(1);
        }
    }
}
