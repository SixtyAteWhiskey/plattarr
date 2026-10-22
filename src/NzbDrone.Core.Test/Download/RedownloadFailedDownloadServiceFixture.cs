using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.Downloads;
using NzbDrone.Core.Movies.Search;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download
{
    // The core applies the user's redownload settings; the module of the failed download's media
    // type decides what a new search is.
    [TestFixture]
    public class RedownloadFailedDownloadServiceFixture : CoreTest<RedownloadFailedDownloadService>
    {
        private Mock<IRedownloadSearcher> _movies;
        private Mock<IRedownloadSearcher> _tv;

        [SetUp]
        public void Setup()
        {
            _movies = GivenSearcher(MediaKind.Movies);
            _tv = GivenSearcher(MediaKind.Tv);

            Mocker.SetConstant<IEnumerable<IRedownloadSearcher>>(new[] { _movies.Object, _tv.Object });

            Mocker.GetMock<IConfigService>().SetupGet(c => c.AutoRedownloadFailed).Returns(true);
            Mocker.GetMock<IConfigService>().SetupGet(c => c.AutoRedownloadFailedFromInteractiveSearch).Returns(true);
        }

        private static Mock<IRedownloadSearcher> GivenSearcher(MediaKind kind)
        {
            var mock = new Mock<IRedownloadSearcher>();
            mock.SetupGet(s => s.Kind).Returns(kind);

            return mock;
        }

        private static DownloadFailedEvent Failure(MediaKind kind)
        {
            return new DownloadFailedEvent { Kind = kind, ParentId = 4, ReleaseSource = ReleaseSourceType.Rss };
        }

        [Test]
        public void should_ask_the_searcher_of_the_failed_downloads_media_type()
        {
            var failure = Failure(MediaKind.Tv);

            Subject.Handle(failure);

            _tv.Verify(s => s.Search(failure), Times.Once());
            _movies.Verify(s => s.Search(It.IsAny<DownloadFailedEvent>()), Times.Never());
        }

        [Test]
        public void should_not_search_when_the_user_asked_to_skip()
        {
            var failure = Failure(MediaKind.Movies);
            failure.SkipRedownload = true;

            Subject.Handle(failure);

            _movies.Verify(s => s.Search(It.IsAny<DownloadFailedEvent>()), Times.Never());
        }

        [Test]
        public void should_not_search_when_auto_redownload_is_off()
        {
            Mocker.GetMock<IConfigService>().SetupGet(c => c.AutoRedownloadFailed).Returns(false);

            Subject.Handle(Failure(MediaKind.Movies));

            _movies.Verify(s => s.Search(It.IsAny<DownloadFailedEvent>()), Times.Never());
        }

        [Test]
        public void should_not_search_after_an_interactive_grab_when_that_is_off()
        {
            Mocker.GetMock<IConfigService>().SetupGet(c => c.AutoRedownloadFailedFromInteractiveSearch).Returns(false);

            var failure = Failure(MediaKind.Movies);
            failure.ReleaseSource = ReleaseSourceType.InteractiveSearch;

            Subject.Handle(failure);

            _movies.Verify(s => s.Search(It.IsAny<DownloadFailedEvent>()), Times.Never());
        }

        [Test]
        public void should_search_after_an_automatic_grab_when_interactive_redownload_is_off()
        {
            Mocker.GetMock<IConfigService>().SetupGet(c => c.AutoRedownloadFailedFromInteractiveSearch).Returns(false);

            Subject.Handle(Failure(MediaKind.Movies));

            _movies.Verify(s => s.Search(It.IsAny<DownloadFailedEvent>()), Times.Once());
        }

        [Test]
        public void should_do_nothing_for_a_media_type_with_no_searcher()
        {
            Mocker.SetConstant<IEnumerable<IRedownloadSearcher>>(new[] { _movies.Object });

            Subject.Handle(Failure(MediaKind.Tv));

            _movies.Verify(s => s.Search(It.IsAny<DownloadFailedEvent>()), Times.Never());
        }
    }

    [TestFixture]
    public class MovieRedownloadSearcherFixture : CoreTest<MovieRedownloadSearcher>
    {
        [Test]
        public void should_search_again_for_the_movie_that_failed()
        {
            Subject.Search(new DownloadFailedEvent { Kind = MediaKind.Movies, ParentId = 12 });

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(c => c.Push(It.Is<MoviesSearchCommand>(m => m.MovieIds.Count == 1 && m.MovieIds[0] == 12)), Times.Once());
        }

        [Test]
        public void should_not_search_when_the_failure_has_no_movie()
        {
            Subject.Search(new DownloadFailedEvent { Kind = MediaKind.Movies, ParentId = 0 });

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(c => c.Push(It.IsAny<MoviesSearchCommand>()), Times.Never());
        }

        [Test]
        public void should_be_the_movie_searcher()
        {
            Subject.Kind.Should().Be(MediaKind.Movies);
        }
    }
}
