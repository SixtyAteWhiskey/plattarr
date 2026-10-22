using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Overrides;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Downloads;
using NzbDrone.Core.Movies.History;
using NzbDrone.Core.Movies.Parser;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download.TrackedDownloads
{
    [TestFixture]
    public class MovieTrackedDownloadMatcherFixture : CoreTest<MovieTrackedDownloadMatcher>
    {
        private void GivenDownloadHistory()
        {
            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.FindByDownloadId(It.Is<string>(sr => sr == "35238")))
                .Returns(new List<MovieHistory>()
                {
                    new MovieHistory()
                    {
                        DownloadId = "35238",
                        SourceTitle = "TV Series S01",
                        ParentId = 3,
                    }
                });
        }

        private TrackedDownload Track(DownloadClientItem item, DownloadHistory downloadHistory)
        {
            var trackedDownload = new TrackedDownload { DownloadItem = item, Kind = MediaKind.Movies };

            Subject.Match(trackedDownload, downloadHistory);

            return trackedDownload;
        }

        [Test]
        public void should_track_downloads_using_the_source_title_if_it_cannot_be_found_using_the_download_title()
        {
            GivenDownloadHistory();

            var remoteMovie = new RemoteMovie
            {
                Movie = new Movie() { Id = 3 },

                ParsedMovieInfo = new ParsedMovieInfo()
                {
                    MovieTitles = new List<string> { "A Movie" },
                    Year = 1998
                }
            };

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.Map(It.Is<ParsedMovieInfo>(i => i.PrimaryMovieTitle == "A Movie"), It.IsAny<string>(), It.IsAny<int>(), null))
                  .Returns(remoteMovie);

            var client = new DownloadClientDefinition()
            {
                Id = 1,
                Protocol = DownloadProtocol.Torrent
            };

            var item = new DownloadClientItem()
            {
                Title = "A Movie 1998",
                DownloadId = "35238",
                DownloadClientInfo = new DownloadClientItemClientInfo
                {
                    Protocol = DownloadProtocol.Torrent,
                    Id = 1,
                    Name = "Client"
                }
            };

            var trackedDownload = Track(item, null);

            trackedDownload.Should().NotBeNull();
            trackedDownload.GetRemoteMovie().Should().NotBeNull();
            trackedDownload.GetRemoteMovie().Movie.Should().NotBeNull();
            trackedDownload.GetRemoteMovie().Movie.Id.Should().Be(3);
        }

        [Test]
        public void should_set_indexer()
        {
            var episodeHistory = new MovieHistory()
            {
                DownloadId = "35238",
                SourceTitle = "TV Series S01",
                ParentId = 3,
                EventType = MovieHistoryEventType.Grabbed,
            };
            episodeHistory.Data.Add("indexer", "MyIndexer (Prowlarr)");
            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.FindByDownloadId(It.Is<string>(sr => sr == "35238")))
                .Returns(new List<MovieHistory>()
                {
                    episodeHistory
                });

            var indexerDefinition = new IndexerDefinition
            {
                Id = 1,
                Name = "MyIndexer (Prowlarr)"
            };
            Mocker.GetMock<IIndexerOverrideService>()
                .Setup(v => v.Get(indexerDefinition.Id, MediaKind.Movies))
                .Returns(new IndexerOverride { IndexerId = indexerDefinition.Id, MediaKind = MediaKind.Movies, MultiLanguages = new List<int> { Language.Original.Id, Language.French.Id } });
            Mocker.GetMock<IIndexerFactory>()
                .Setup(v => v.Get(indexerDefinition.Id))
                .Returns(indexerDefinition);
            Mocker.GetMock<IIndexerFactory>()
                .Setup(v => v.All())
                .Returns(new List<IndexerDefinition>() { indexerDefinition });

            var remoteEpisode = new RemoteMovie
            {
                Movie = new Movie() { Id = 3 },
                ParsedMovieInfo = new ParsedMovieInfo()
                {
                    MovieTitles = new List<string> { "A Movie" },
                    Year = 1998
                }
            };

            Mocker.GetMock<IParsingService>()
                .Setup(s => s.Map(It.IsAny<ParsedMovieInfo>(), It.IsAny<string>(), It.IsAny<int>(), null))
                .Returns(remoteEpisode);

            var client = new DownloadClientDefinition()
            {
                Id = 1,
                Protocol = DownloadProtocol.Torrent
            };

            var item = new DownloadClientItem()
            {
                Title = "A Movie 1998",
                DownloadId = "35238",
                DownloadClientInfo = new DownloadClientItemClientInfo
                {
                    Protocol = DownloadProtocol.Torrent,
                    Id = 1,
                    Name = "Client"
                }
            };

            var trackedDownload = Track(item, null);

            trackedDownload.Should().NotBeNull();
            trackedDownload.GetRemoteMovie().Should().NotBeNull();
            trackedDownload.GetRemoteMovie().Release.Should().NotBeNull();
            trackedDownload.GetRemoteMovie().Release.Indexer.Should().Be("MyIndexer (Prowlarr)");
        }

        [Test]
        public void should_track_downloads_using_the_movie_id_for_already_imported_downloads()
        {
            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.FindByDownloadId(It.Is<string>(sr => sr == "35238")))
                .Returns([]);

            var downloadHistory = new DownloadHistory
            {
                ParentId = 5,
                EventType = DownloadHistoryEventType.DownloadImported,
            };

            var remoteMovie = new RemoteMovie
            {
                Movie = new Movie { Id = 5 },
                ParsedMovieInfo = new ParsedMovieInfo
                {
                    MovieTitles = { "A Movie" },
                    Year = 1998
                },
            };

            Mocker.GetMock<IParsingService>()
                .Setup(s => s.Map(It.Is<ParsedMovieInfo>(i => i.Year == 1998 && i.MovieTitle == "A Movie"), It.IsAny<int>()))
                .Returns(remoteMovie);

            var item = new DownloadClientItem
            {
                Title = "A Movie 1998",
                DownloadId = "35238",
                DownloadClientInfo = new DownloadClientItemClientInfo
                {
                    Protocol = DownloadProtocol.Torrent,
                    Id = 1,
                    Name = "Client"
                }
            };

            var trackedDownload = Track(item, downloadHistory);

            trackedDownload.Should().NotBeNull();
            trackedDownload.GetRemoteMovie().Should().NotBeNull();
            trackedDownload.GetRemoteMovie().Movie.Should().NotBeNull();
            trackedDownload.GetRemoteMovie().Movie.Id.Should().Be(5);
            trackedDownload.GetRemoteMovie().ParsedMovieInfo.Year.Should().Be(1998);
        }

        [Test]
        public void rematch_should_unmap_tracked_download_if_movie_deleted()
        {
            var remoteMovie = new RemoteMovie
            {
                Movie = new Movie() { Id = 3 },
                ParsedMovieInfo = new ParsedMovieInfo()
                {
                    MovieTitles = { "A Movie" },
                    Year = 1998
                }
            };

            var trackedDownload = new TrackedDownload
            {
                Kind = MediaKind.Movies,
                DownloadItem = new DownloadClientItem { Title = "A Movie 1998", DownloadId = "12345" },
                RemoteRelease = remoteMovie
            };

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.Map(It.IsAny<ParsedMovieInfo>(), It.IsAny<string>(), It.IsAny<int>(), null))
                  .Returns(default(RemoteMovie));

            Subject.Rematch(trackedDownload);

            trackedDownload.RemoteRelease.Should().BeNull();
        }

        [Test]
        public void rematch_should_not_throw_when_the_title_no_longer_parses()
        {
            var trackedDownload = new TrackedDownload
            {
                Kind = MediaKind.Movies,
                DownloadItem = new DownloadClientItem { Title = "A Movie 1998", DownloadId = "12345" },
                RemoteRelease = new RemoteMovie { Movie = new Movie() { Id = 3 } }
            };

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.Map(It.IsAny<ParsedMovieInfo>(), It.IsAny<string>(), It.IsAny<int>(), null))
                  .Returns(default(RemoteMovie));

            Subject.Invoking(s => s.Rematch(trackedDownload)).Should().NotThrow();
        }

        [Test]
        public void should_match_the_title_of_a_movie_release()
        {
            Subject.MatchesTitle("A Movie 1998 1080p BluRay x264-GROUP").Should().BeTrue();
        }

        [Test]
        public void should_not_match_the_title_of_an_episode_release()
        {
            Subject.MatchesTitle("Some Show S01E02 1080p WEB-DL x264-GROUP").Should().BeFalse();
        }
    }
}
