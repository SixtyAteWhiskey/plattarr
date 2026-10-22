using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Downloads;
using NzbDrone.Core.Movies.Events;
using NzbDrone.Core.Movies.History;
using NzbDrone.Core.Movies.MediaFiles;
using NzbDrone.Core.Movies.MediaFiles.Events;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download.DownloadHistoryTests
{
    [TestFixture]
    public class MovieDownloadHistoryHandlerFixture : CoreTest<MovieDownloadHistoryHandler>
    {
        private DownloadHistory _inserted;
        private DownloadClientItem _item;

        [SetUp]
        public void Setup()
        {
            _inserted = null;

            _item = new DownloadClientItem
            {
                Title = "Movie.2020.1080p",
                DownloadId = "HASH",
                DownloadClientInfo = new DownloadClientItemClientInfo { Id = 3, Name = "qb-main", Type = "QBittorrent", Protocol = DownloadProtocol.Torrent }
            };

            Mocker.GetMock<IDownloadHistoryRepository>()
                .Setup(r => r.Insert(It.IsAny<DownloadHistory>()))
                .Callback<DownloadHistory>(h => _inserted = h)
                .Returns<DownloadHistory>(h => h);
        }

        private MovieFileImportedEvent Imported(bool newDownload, DownloadClientItem item)
        {
            return new MovieFileImportedEvent(
                new LocalMovie { Path = @"/movies/Movie (2020)/Movie.mkv" },
                new MovieFile { MovieId = 9 },
                new List<DeletedMovieFile>(),
                newDownload,
                item);
        }

        [Test]
        public void should_record_a_file_imported_by_a_new_download_as_a_movie()
        {
            Subject.Handle(Imported(true, _item));

            _inserted.MediaKind.Should().Be(MediaKind.Movies);
            _inserted.ParentId.Should().Be(9);
            _inserted.EventType.Should().Be(DownloadHistoryEventType.FileImported);
            _inserted.DownloadId.Should().Be("HASH");
            _inserted.DownloadClientId.Should().Be(3);
        }

        [Test]
        public void should_not_record_a_file_that_was_not_a_new_download()
        {
            Subject.Handle(Imported(false, _item));

            _inserted.Should().BeNull();
        }

        [Test]
        public void should_find_the_download_id_in_history_when_the_import_has_none()
        {
            _item.DownloadId = null;

            Mocker.GetMock<IHistoryService>()
                .Setup(h => h.FindDownloadId(It.IsAny<MovieFileImportedEvent>()))
                .Returns("FROM-HISTORY");

            Subject.Handle(Imported(true, _item));

            _inserted.DownloadId.Should().Be("FROM-HISTORY");
        }

        [Test]
        public void should_not_record_a_file_with_no_download_id_anywhere()
        {
            _item.DownloadId = null;

            Mocker.GetMock<IHistoryService>()
                .Setup(h => h.FindDownloadId(It.IsAny<MovieFileImportedEvent>()))
                .Returns((string)null);

            Subject.Handle(Imported(true, _item));

            _inserted.Should().BeNull();
        }

        [Test]
        public void should_delete_download_history_of_deleted_movies_for_the_movie_kind()
        {
            Subject.Handle(new MoviesDeletedEvent(new List<Movie> { new Movie { Id = 4 }, new Movie { Id = 5 } }, false, false));

            Mocker.GetMock<IDownloadHistoryRepository>()
                .Verify(r => r.DeleteByParentIds(MediaKind.Movies, It.Is<List<int>>(l => l.Count == 2 && l.Contains(4) && l.Contains(5))), Times.Once());
        }
    }
}
