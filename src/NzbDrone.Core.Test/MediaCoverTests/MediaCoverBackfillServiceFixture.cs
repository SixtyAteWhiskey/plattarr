using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Covers;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Covers;

namespace NzbDrone.Core.Test.MediaCoverTests
{
    [TestFixture]
    public class MediaCoverBackfillServiceFixture : CoreTest<MediaCoverBackfillService>
    {
        private Movie _movie;
        private Series _series;
        private HashSet<string> _files;

        [SetUp]
        public void Setup()
        {
            _movie = new Movie { Id = 2 };
            _movie.MovieMetadata.Value.Images = new List<MediaCover.MediaCover>
            {
                new(MediaCoverTypes.Poster, "https://example.com/movie.jpg")
            };

            _series = new Series
            {
                Id = 2,
                Images = new List<MediaCover.MediaCover>
                {
                    new(MediaCoverTypes.Poster, "https://example.com/series.jpg")
                }
            };

            _files = new HashSet<string>();

            Mocker.GetMock<IMovieService>()
                .Setup(s => s.GetMovies(It.IsAny<IEnumerable<int>>()))
                .Returns(new List<Movie> { _movie });

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetSeries(It.IsAny<IEnumerable<int>>()))
                .Returns(new List<Series> { _series });

            Mocker.GetMock<IEnsureMovieCovers>()
                .Setup(s => s.GetCoverPath(2, MediaCoverTypes.Poster, It.IsAny<int?>()))
                .Returns((int id, MediaCoverTypes type, int? height) => CoverPath("/covers/2", height));

            Mocker.GetMock<IEnsureSeriesCovers>()
                .Setup(s => s.GetCoverPath(2, MediaCoverTypes.Poster, It.IsAny<int?>()))
                .Returns((int id, MediaCoverTypes type, int? height) => CoverPath("/covers/Series/2", height));

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FileExists(It.IsAny<string>()))
                .Returns((string path) => _files.Contains(path));

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFileSize(It.IsAny<string>()))
                .Returns((string path) => _files.Contains(path) ? 10 : 0);

            Mocker.SetConstant<IEnumerable<IMediaCoverBackfillSource>>(new IMediaCoverBackfillSource[]
            {
                Mocker.Resolve<MovieCoverBackfillSource>(),
                Mocker.Resolve<SeriesCoverBackfillSource>()
            });
        }

        [Test]
        public void should_fill_movie_and_tv_covers_with_the_same_numeric_id_in_separate_folders()
        {
            Mocker.GetMock<IEnsureMovieCovers>()
                .Setup(s => s.EnsureCovers(_movie))
                .Callback(() => AddCovers("/covers/2"));

            Mocker.GetMock<IEnsureSeriesCovers>()
                .Setup(s => s.EnsureCovers(_series))
                .Callback(() => AddCovers("/covers/Series/2"));

            var result = Subject.Backfill(new[] { 2 }, new[] { 2 });

            result.Checked.Should().Be(2);
            result.Missing.Should().Be(0);
            _files.Should().HaveCount(6);
        }

        [Test]
        public void should_leave_existing_covers_alone()
        {
            AddCovers("/covers/2");
            AddCovers("/covers/Series/2");

            Subject.Backfill(new[] { 2 }, new[] { 2 }).Missing.Should().Be(0);

            Mocker.GetMock<IEnsureMovieCovers>().Verify(s => s.EnsureCovers(It.IsAny<Movie>()), Times.Never());
            Mocker.GetMock<IEnsureSeriesCovers>().Verify(s => s.EnsureCovers(It.IsAny<Series>()), Times.Never());
        }

        [Test]
        public void should_repair_resized_covers_when_the_original_is_present()
        {
            _files.Add("/covers/2/poster.jpg");
            AddCovers("/covers/Series/2");

            Mocker.GetMock<IEnsureMovieCovers>()
                .Setup(s => s.EnsureCovers(_movie))
                .Callback(() => AddCovers("/covers/2"));

            var result = Subject.Backfill(new[] { 2 }, new[] { 2 });

            result.Missing.Should().Be(0);
            Mocker.GetMock<IEnsureMovieCovers>().Verify(s => s.EnsureCovers(_movie), Times.Once());
            Mocker.GetMock<IEnsureSeriesCovers>().Verify(s => s.EnsureCovers(It.IsAny<Series>()), Times.Never());
        }

        [Test]
        public void scheduled_retry_should_scan_all_movies_and_tv_shows()
        {
            Mocker.GetMock<IMovieService>()
                .Setup(s => s.GetAllMovies())
                .Returns(new List<Movie> { _movie });

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetAllSeries())
                .Returns(new List<Series> { _series });

            Mocker.GetMock<IEnsureMovieCovers>()
                .Setup(s => s.EnsureCovers(_movie))
                .Callback(() => AddCovers("/covers/2"));

            Mocker.GetMock<IEnsureSeriesCovers>()
                .Setup(s => s.EnsureCovers(_series))
                .Callback(() => AddCovers("/covers/Series/2"));

            Subject.Execute(new BackfillMediaCoversCommand());

            _files.Should().HaveCount(6);
            Mocker.GetMock<IMovieService>().Verify(s => s.GetAllMovies(), Times.Once());
            Mocker.GetMock<ISeriesService>().Verify(s => s.GetAllSeries(), Times.Once());
        }

        [Test]
        public void should_count_a_failed_movie_cover_and_continue_to_tv()
        {
            Mocker.GetMock<IEnsureMovieCovers>()
                .Setup(s => s.EnsureCovers(_movie))
                .Throws(new InvalidOperationException("download failed"));

            Mocker.GetMock<IEnsureSeriesCovers>()
                .Setup(s => s.EnsureCovers(_series))
                .Callback(() => AddCovers("/covers/Series/2"));

            var result = Subject.Backfill(new[] { 2 }, new[] { 2 });

            result.Checked.Should().Be(2);
            result.Missing.Should().Be(1);
            Mocker.GetMock<IEnsureSeriesCovers>().Verify(s => s.EnsureCovers(_series), Times.Once());
        }

        private static string CoverPath(string directory, int? height)
        {
            return directory + "/poster" + (height.HasValue ? "-" + height.Value : string.Empty) + ".jpg";
        }

        private void AddCovers(string directory)
        {
            _files.Add(CoverPath(directory, null));
            _files.Add(CoverPath(directory, 500));
            _files.Add(CoverPath(directory, 250));
        }
    }
}
