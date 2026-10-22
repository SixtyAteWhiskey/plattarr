using System.Collections.Generic;
using System.IO;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.History;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.DecisionEngineTests.ReleaseSpecifications;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Test.Qualities;

namespace NzbDrone.Core.Test.HistoryTests
{
    public class HistoryServiceFixture : CoreTest<HistoryService>
    {
        private QualityProfile _profile;
        private QualityProfile _profileCustom;

        [SetUp]
        public void Setup()
        {
            _profile = new QualityProfile { Cutoff = Quality.WEBDL720p.Id, Items = QualityFixture.GetDefaultQualities() };
            _profileCustom = new QualityProfile { Cutoff = Quality.WEBDL720p.Id, Items = QualityFixture.GetDefaultQualities(Quality.DVD) };
        }

        [Test]
        public void should_return_null_if_no_history()
        {
            Mocker.GetMock<IHistoryRepository>()
                .Setup(v => v.GetBestQualityInHistory(2))
                .Returns(new List<QualityModel>());

            var quality = Subject.GetBestQualityInHistory(_profile, 2);

            quality.Should().BeNull();
        }

        [Test]
        public void should_return_best_quality()
        {
            Mocker.GetMock<IHistoryRepository>()
                .Setup(v => v.GetBestQualityInHistory(2))
                .Returns(new List<QualityModel> { new QualityModel(Quality.DVD), new QualityModel(Quality.Bluray1080p) });

            var quality = Subject.GetBestQualityInHistory(_profile, 2);

            quality.Should().Be(new QualityModel(Quality.Bluray1080p));
        }

        [Test]
        public void should_return_best_quality_with_custom_order()
        {
            Mocker.GetMock<IHistoryRepository>()
                .Setup(v => v.GetBestQualityInHistory(2))
                .Returns(new List<QualityModel> { new QualityModel(Quality.DVD), new QualityModel(Quality.Bluray1080p) });

            var quality = Subject.GetBestQualityInHistory(_profileCustom, 2);

            quality.Should().Be(new QualityModel(Quality.DVD));
        }

        [Test]
        public void should_use_file_name_for_source_title_if_scene_name_is_null()
        {
            var movie = Builder<Movie>.CreateNew().Build();
            var movieFile = Builder<MovieFile>.CreateNew()
                                                  .With(f => f.SceneName = null)
                                                  .Build();

            var localMovie = new LocalMovie()
            {
                Movie = movie,
                Path = @"C:\Test\Unsorted\Movie.2011.mkv"
            };

            var downloadClientItem = new DownloadClientItem
            {
                DownloadClientInfo = new DownloadClientItemClientInfo
                {
                    Protocol = DownloadProtocol.Usenet,
                    Id = 1,
                    Name = "sab"
                },
                DownloadId = "abcd"
            };

            Subject.Handle(new MovieFileImportedEvent(localMovie, movieFile, new List<DeletedMovieFile>(), true, downloadClientItem));

            Mocker.GetMock<IHistoryRepository>()
                .Verify(v => v.Insert(It.Is<MovieHistory>(h => h.SourceTitle == Path.GetFileNameWithoutExtension(localMovie.Path))));
        }

        [Test]
        public void should_not_write_movie_history_for_a_grab_of_another_media_type()
        {
            var tv = new TvRemoteRelease { Series = new TvParent { Id = 5, Title = "A Series" }, Release = new ReleaseInfo { Title = "A.Series.S01E01" } };

            Subject.Handle(new ReleaseGrabbedEvent(tv) { DownloadId = "HASH" });

            Mocker.GetMock<IHistoryRepository>().Verify(r => r.Insert(It.IsAny<MovieHistory>()), Times.Never());
        }

        [Test]
        public void should_write_movie_history_for_a_movie_download_failure()
        {
            Subject.Handle(new DownloadFailedEvent
            {
                Kind = MediaKind.Movies,
                ParentId = 9,
                DownloadId = "HASH",
                SourceTitle = "Movie.2020.1080p",
                Quality = new QualityModel(Quality.Bluray1080p)
            });

            Mocker.GetMock<IHistoryRepository>()
                .Verify(r => r.Insert(It.Is<MovieHistory>(h => h.ParentId == 9 && h.EventType == MovieHistoryEventType.DownloadFailed && h.MediaKind == MediaKind.Movies)), Times.Once());
        }

        [Test]
        public void should_not_write_movie_history_for_a_download_failure_of_another_media_type()
        {
            Subject.Handle(new DownloadFailedEvent { Kind = MediaKind.Tv, ParentId = 5, DownloadId = "HASH", SourceTitle = "A.Series.S01E01" });

            Mocker.GetMock<IHistoryRepository>().Verify(r => r.Insert(It.IsAny<MovieHistory>()), Times.Never());
        }

        [Test]
        public void should_write_movie_history_for_an_ignored_movie_download()
        {
            Subject.Handle(new DownloadIgnoredEvent
            {
                Kind = MediaKind.Movies,
                ParentId = 9,
                DownloadId = "HASH",
                SourceTitle = "Movie.2020.1080p",
                Quality = new QualityModel(Quality.Bluray1080p),
                DownloadClientInfo = new DownloadClientItemClientInfo { Id = 1, Name = "sab", Type = "Sabnzbd" }
            });

            Mocker.GetMock<IHistoryRepository>()
                .Verify(r => r.Insert(It.Is<MovieHistory>(h => h.ParentId == 9 && h.EventType == MovieHistoryEventType.DownloadIgnored)), Times.Once());
        }

        [Test]
        public void should_not_write_movie_history_for_an_ignored_download_of_another_media_type()
        {
            Subject.Handle(new DownloadIgnoredEvent
            {
                Kind = MediaKind.Tv,
                ParentId = 5,
                DownloadId = "HASH",
                DownloadClientInfo = new DownloadClientItemClientInfo { Id = 1, Name = "sab", Type = "Sabnzbd" }
            });

            Mocker.GetMock<IHistoryRepository>().Verify(r => r.Insert(It.IsAny<MovieHistory>()), Times.Never());
        }
    }
}
