using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Aggregation;
using NzbDrone.Core.Music.CustomFormats;
using NzbDrone.Core.Music.Downloads;
using NzbDrone.Core.Music.History;
using NzbDrone.Core.Music.Parser;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.Download.TrackedDownloads
{
    [TestFixture]
    public class MusicTrackedDownloadMatcherFixture : CoreTest<MusicTrackedDownloadMatcher>
    {
        private const string AlbumTitle = "Audio Artist - Audio Album [2018 - FLAC]";

        private RemoteAlbum _remoteAlbum;

        [SetUp]
        public void Setup()
        {
            _remoteAlbum = new RemoteAlbum
            {
                Artist = new Artist { Id = 5 },
                Albums = new List<Album> { new Album { Id = 4 } },
                ParsedAlbumInfo = new ParsedAlbumInfo { AlbumTitle = "Audio Album", ArtistName = "Audio Artist" }
            };
        }

        private void GivenHistory(params AlbumHistory[] history)
        {
            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.FindByDownloadId("35238"))
                .Returns(history.ToList());
        }

        private static AlbumHistory Grabbed(string sourceTitle = AlbumTitle)
        {
            var history = new AlbumHistory
            {
                DownloadId = "35238",
                SourceTitle = sourceTitle,
                ParentId = 5,
                ItemId = 4,
                EventType = AlbumHistoryEventType.Grabbed,
                Date = DateTime.UtcNow
            };

            history.Data["indexer"] = "MyIndexer";

            return history;
        }

        private TrackedDownload Track(string title, DownloadHistory downloadHistory = null)
        {
            var trackedDownload = new TrackedDownload
            {
                DownloadClient = 1,
                Kind = MediaKind.Music,
                DownloadItem = new DownloadClientItem
                {
                    Title = title,
                    DownloadId = "35238",
                    TotalSize = 1234
                }
            };

            Subject.Match(trackedDownload, downloadHistory);

            return trackedDownload;
        }

        [Test]
        public void should_match_the_download_title_to_an_artist_and_albums()
        {
            GivenHistory();

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.Map(It.Is<ParsedAlbumInfo>(i => i.AlbumTitle == "Audio Album" && i.ArtistName == "Audio Artist"), null))
                  .Returns(_remoteAlbum);

            var trackedDownload = Track(AlbumTitle);

            trackedDownload.GetRemoteAlbum().Should().BeSameAs(_remoteAlbum);
            trackedDownload.Status.Should().Be(TrackedDownloadStatus.Ok);
        }

        [Test]
        public void should_track_downloads_using_the_source_title_if_it_cannot_be_found_using_the_download_title()
        {
            GivenHistory(Grabbed());

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.Map(It.Is<ParsedAlbumInfo>(i => i.AlbumTitle == "Audio Album" && i.ArtistName == "Audio Artist"), It.IsAny<int>(), It.IsAny<IEnumerable<int>>()))
                  .Returns(_remoteAlbum);

            var trackedDownload = Track("The torrent release folder");

            trackedDownload.GetRemoteAlbum().Should().NotBeNull();
            trackedDownload.GetRemoteAlbum().Artist.Id.Should().Be(5);
            trackedDownload.GetRemoteAlbum().Albums.First().Id.Should().Be(4);
        }

        [Test]
        public void should_map_the_history_with_the_grabbed_artist_and_albums()
        {
            GivenHistory(Grabbed());

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.Map(It.IsAny<ParsedAlbumInfo>(), It.IsAny<int>(), It.IsAny<IEnumerable<int>>()))
                  .Returns(_remoteAlbum);

            Track("The torrent release folder");

            Mocker.GetMock<IParsingService>()
                  .Verify(s => s.Map(It.IsAny<ParsedAlbumInfo>(), 5, It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 4 }))), Times.Once());
        }

        [Test]
        public void should_not_warn_when_history_references_missing_artist_and_album()
        {
            GivenHistory(Grabbed("An unparseable download folder"));

            var trackedDownload = Track("An unparseable download folder");

            trackedDownload.Status.Should().Be(TrackedDownloadStatus.Ok);
            trackedDownload.GetRemoteAlbum().Should().BeNull();
        }

        [Test]
        public void should_set_indexer_and_added_from_the_grab()
        {
            var grabbed = Grabbed();
            GivenHistory(grabbed);

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.Map(It.IsAny<ParsedAlbumInfo>(), It.IsAny<int>(), It.IsAny<IEnumerable<int>>()))
                  .Returns(_remoteAlbum);

            var trackedDownload = Track("The torrent release folder");

            trackedDownload.Indexer.Should().Be("MyIndexer");
            trackedDownload.Added.Should().Be(grabbed.Date);
            trackedDownload.GetRemoteAlbum().Release.Indexer.Should().Be("MyIndexer");
        }

        [Test]
        public void should_set_indexer_flags_and_indexer_id_from_the_grab()
        {
            var grabbed = Grabbed();
            grabbed.Data["indexerFlags"] = IndexerFlags.G_Freeleech.ToString();
            GivenHistory(grabbed);

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.Map(It.IsAny<ParsedAlbumInfo>(), It.IsAny<int>(), It.IsAny<IEnumerable<int>>()))
                  .Returns(_remoteAlbum);

            var trackedDownload = Track("The torrent release folder", new DownloadHistory { IndexerId = 7 });

            trackedDownload.GetRemoteAlbum().Release.IndexerId.Should().Be(7);
            trackedDownload.GetRemoteAlbum().Release.IndexerFlags.Should().NotBeEmpty();
        }

        [Test]
        public void should_augment_the_remote_album_and_calculate_custom_formats()
        {
            GivenHistory();

            var formats = new List<NzbDrone.Core.CustomFormats.CustomFormat> { new NzbDrone.Core.CustomFormats.CustomFormat("Lossless") };

            Mocker.GetMock<IParsingService>().Setup(s => s.Map(It.IsAny<ParsedAlbumInfo>(), null)).Returns(_remoteAlbum);
            Mocker.GetMock<IMusicCustomFormatCalculationService>().Setup(s => s.ParseCustomFormat(_remoteAlbum, 1234)).Returns(formats);

            var trackedDownload = Track(AlbumTitle);

            trackedDownload.GetRemoteAlbum().CustomFormats.Should().BeSameAs(formats);

            Mocker.GetMock<IRemoteAlbumAggregationService>().Verify(s => s.Augment(_remoteAlbum), Times.Once());
        }

        [Test]
        public void should_leave_the_download_unmatched_when_nothing_maps()
        {
            GivenHistory();

            Mocker.GetMock<IParsingService>().Setup(s => s.Map(It.IsAny<ParsedAlbumInfo>(), null)).Returns((RemoteAlbum)null);

            var trackedDownload = Track(AlbumTitle);

            trackedDownload.RemoteRelease.Should().BeNull();
            trackedDownload.Status.Should().Be(TrackedDownloadStatus.Ok);
        }

        [Test]
        public void should_warn_when_the_title_matches_several_artists()
        {
            GivenHistory();

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.Map(It.IsAny<ParsedAlbumInfo>(), null))
                  .Throws(new MultipleArtistsFoundException(new List<Artist> { new Artist { Name = "One" }, new Artist { Name = "Two" } }, "Found multiple artists"));

            var trackedDownload = Track(AlbumTitle);

            trackedDownload.Status.Should().Be(TrackedDownloadStatus.Warning);
            trackedDownload.StatusMessages.Single().Messages.Single().Should().Contain("found multiple artists");
        }

        [Test]
        public void should_warn_when_matching_throws()
        {
            GivenHistory();

            Mocker.GetMock<IParsingService>().Setup(s => s.Map(It.IsAny<ParsedAlbumInfo>(), null)).Throws<InvalidOperationException>();

            var trackedDownload = Track(AlbumTitle);

            trackedDownload.Status.Should().Be(TrackedDownloadStatus.Warning);
            trackedDownload.StatusMessages.Single().Messages.Single().Should().Contain("Unable to parse albums from title");
        }

        [Test]
        public void matches_title_should_be_false_when_the_title_is_not_an_album()
        {
            Subject.MatchesTitle("Some.Show.S01E01.720p.HDTV.x264-GRP").Should().BeFalse();

            Mocker.GetMock<IParsingService>().Verify(s => s.GetArtist(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void matches_title_should_be_false_when_no_library_artist_matches()
        {
            Mocker.GetMock<IParsingService>().Setup(s => s.GetArtist(It.IsAny<string>())).Returns((Artist)null);

            Subject.MatchesTitle(AlbumTitle).Should().BeFalse();
        }

        [Test]
        public void matches_title_should_be_true_when_a_library_artist_matches()
        {
            Mocker.GetMock<IParsingService>().Setup(s => s.GetArtist(AlbumTitle)).Returns(new Artist { Id = 5 });

            Subject.MatchesTitle(AlbumTitle).Should().BeTrue();
        }

        [Test]
        public void matches_title_should_be_true_when_several_library_artists_match()
        {
            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetArtist(AlbumTitle))
                  .Throws(new MultipleArtistsFoundException(new List<Artist>(), "Found multiple artists"));

            Subject.MatchesTitle(AlbumTitle).Should().BeTrue();
        }

        [Test]
        public void rematch_should_map_the_title_again_and_augment()
        {
            Mocker.GetMock<IParsingService>().Setup(s => s.Map(It.IsAny<ParsedAlbumInfo>(), null)).Returns(_remoteAlbum);

            var trackedDownload = new TrackedDownload { Kind = MediaKind.Music, DownloadItem = new DownloadClientItem { Title = AlbumTitle } };

            Subject.Rematch(trackedDownload);

            trackedDownload.GetRemoteAlbum().Should().BeSameAs(_remoteAlbum);
            Mocker.GetMock<IRemoteAlbumAggregationService>().Verify(s => s.Augment(_remoteAlbum), Times.Once());
        }

        [Test]
        public void rematch_should_unmap_the_download_when_the_album_no_longer_maps()
        {
            Mocker.GetMock<IParsingService>().Setup(s => s.Map(It.IsAny<ParsedAlbumInfo>(), null)).Returns((RemoteAlbum)null);

            var trackedDownload = new TrackedDownload
            {
                Kind = MediaKind.Music,
                DownloadItem = new DownloadClientItem { Title = AlbumTitle },
                RemoteRelease = _remoteAlbum
            };

            Subject.Rematch(trackedDownload);

            trackedDownload.RemoteRelease.Should().BeNull();
        }

        [Test]
        public void rematch_should_unmap_the_download_when_the_title_no_longer_parses()
        {
            var trackedDownload = new TrackedDownload
            {
                Kind = MediaKind.Music,
                DownloadItem = new DownloadClientItem { Title = "Some.Show.S01E01.720p.HDTV.x264-GRP" },
                RemoteRelease = _remoteAlbum
            };

            Subject.Rematch(trackedDownload);

            trackedDownload.RemoteRelease.Should().BeNull();
        }
    }
}
