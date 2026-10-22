using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Downloads;
using NzbDrone.Core.Movies.Events;
using NzbDrone.Core.Movies.MediaFiles;
using NzbDrone.Core.Movies.MediaFiles.Events;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Test.DecisionEngineTests.ReleaseSpecifications;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download.TrackedDownloads
{
    [TestFixture]
    public class MovieTrackedDownloadRefresherFixture : CoreTest<MovieTrackedDownloadRefresher>
    {
        private TrackedDownload _matched;
        private TrackedDownload _unmatched;
        private TrackedDownload _otherMovie;
        private TrackedDownload _tv;

        private static TrackedDownload Tracked(string id, MediaKind? kind, RemoteRelease release)
        {
            return new TrackedDownload
            {
                Kind = kind,
                DownloadItem = new DownloadClientItem { Title = id, DownloadId = id },
                RemoteRelease = release
            };
        }

        [SetUp]
        public void Setup()
        {
            _matched = Tracked("matched", MediaKind.Movies, new RemoteMovie { Movie = new Movie { Id = 3, TmdbId = 30 } });
            _unmatched = Tracked("unmatched", MediaKind.Movies, null);
            _otherMovie = Tracked("other", MediaKind.Movies, new RemoteMovie { Movie = new Movie { Id = 4, TmdbId = 40 } });
            _tv = Tracked("tv", MediaKind.Tv, new TvRemoteRelease { Series = new TvParent { Id = 3 } });

            Mocker.GetMock<ITrackedDownloadService>()
                  .Setup(s => s.GetTrackedDownloads())
                  .Returns(new List<TrackedDownload> { _matched, _unmatched, _otherMovie, _tv });
        }

        private void VerifyRematched(params TrackedDownload[] expected)
        {
            Mocker.GetMock<ITrackedDownloadService>()
                  .Verify(s => s.Rematch(It.Is<List<TrackedDownload>>(l => l.OrderBy(t => t.DownloadItem.DownloadId).SequenceEqual(expected.OrderBy(t => t.DownloadItem.DownloadId)))), Times.Once());
        }

        [Test]
        public void movie_deleted_should_rematch_only_the_downloads_matched_to_it()
        {
            Subject.Handle(new MoviesDeletedEvent(new List<Movie> { new Movie { Id = 3, TmdbId = 30 } }, false, false));

            VerifyRematched(_matched);
        }

        [Test]
        public void movie_edited_should_rematch_only_the_downloads_matched_to_it()
        {
            Subject.Handle(new MovieEditedEvent(new Movie { Id = 4, TmdbId = 40 }, new Movie { Id = 4, TmdbId = 40 }));

            VerifyRematched(_otherMovie);
        }

        [Test]
        public void movies_bulk_edited_should_rematch_the_downloads_matched_to_any_of_them()
        {
            Subject.Handle(new MoviesBulkEditedEvent(new List<Movie> { new Movie { Id = 3, TmdbId = 30 }, new Movie { Id = 4, TmdbId = 40 } }));

            VerifyRematched(_matched, _otherMovie);
        }

        [Test]
        public void movie_added_should_rematch_unmatched_downloads_and_those_of_the_same_tmdb_id()
        {
            Subject.Handle(new MovieAddedEvent(new Movie { Id = 9, TmdbId = 30 }));

            VerifyRematched(_matched, _unmatched);
        }

        [Test]
        public void movie_file_imported_should_request_a_download_refresh()
        {
            Subject.Handle(new MovieFileImportedEvent(new LocalMovie(), new MovieFile(), new List<DeletedMovieFile>(), true, null));

            Mocker.GetMock<IEventAggregator>()
                  .Verify(e => e.PublishEvent(It.IsAny<RefreshDownloadsRequestedEvent>()), Times.Once());
        }

        [Test]
        public void should_never_rematch_a_download_of_another_media_type()
        {
            Subject.Handle(new MovieAddedEvent(new Movie { Id = 9, TmdbId = 99 }));

            Mocker.GetMock<ITrackedDownloadService>()
                  .Verify(s => s.Rematch(It.Is<List<TrackedDownload>>(l => l.Contains(_tv))), Times.Never());
        }
    }
}
