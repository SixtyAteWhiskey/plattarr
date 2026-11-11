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
    public class ProactiveMovieUpgradePreviewControllerFixture : TestBase
    {
        [Test]
        public void preview_reports_the_same_cutoff_candidates_and_reasons_as_execution()
        {
            var movies = new List<Movie>
            {
                new() { Id = 11, Monitored = true, MinimumAvailability = MovieStatusType.Announced, MovieFileId = 1 },
                new() { Id = 12, Monitored = false, MinimumAvailability = MovieStatusType.Announced, MovieFileId = 2 }
            };
            var cutoff = Mocker.GetMock<IMovieCutoffService>();
            cutoff.Setup(x => x.MoviesWhereCutoffUnmet(It.IsAny<PagingSpec<Movie>>()))
                .Returns(new PagingSpec<Movie> { Records = movies });
            var queue = Mocker.GetMock<IQueueService>();
            queue.Setup(x => x.GetQueue()).Returns(new List<NzbDrone.Core.Queue.Queue>());
            var history = Mocker.GetMock<IHistoryService>();
            history.Setup(x => x.Since(It.IsAny<DateTime>(), MovieHistoryEventType.DownloadFailed))
                .Returns(new List<MovieHistory>());
            history.Setup(x => x.Since(It.IsAny<DateTime>(), MovieHistoryEventType.Grabbed))
                .Returns(new List<MovieHistory>());
            var config = Mocker.GetMock<IConfigService>();
            config.SetupGet(x => x.ProactiveMovieUpgradeActiveDownloadLimit).Returns(5);
            config.SetupGet(x => x.ProactiveMovieUpgradeRecentGrabHours).Returns(24);

            var preview = new ProactiveMovieUpgradePreviewController(cutoff.Object, queue.Object, history.Object, config.Object)
                .Get(0, 50, null).Value;

            preview.CutoffUnmetMovies.Should().Be(2);
            preview.NextMovies.Should().ContainSingle().Which.MovieId.Should().Be(11);
            preview.SkippedMovies.Should().ContainSingle().Which.Reason.Should().Be("unmonitored");
            preview.PauseReason.Should().Be("disabled");
        }
    }
}
