using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Downloads;
using NzbDrone.Core.Music.Events;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.MediaFiles.Events;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.Download.TrackedDownloads
{
    [TestFixture]
    public class MusicTrackedDownloadRefresherFixture : CoreTest<MusicTrackedDownloadRefresher>
    {
        private TrackedDownload _matched;
        private TrackedDownload _unmatched;
        private TrackedDownload _otherArtist;
        private TrackedDownload _movie;

        private static TrackedDownload Tracked(string id, MediaKind? kind, RemoteRelease release)
        {
            return new TrackedDownload
            {
                Kind = kind,
                DownloadItem = new DownloadClientItem { Title = id, DownloadId = id },
                RemoteRelease = release
            };
        }

        private static RemoteAlbum Remote(int artistId, string foreignId, params int[] albumIds)
        {
            return new RemoteAlbum
            {
                Artist = new Artist { Id = artistId, Metadata = new ArtistMetadata { ForeignArtistId = foreignId } },
                Albums = albumIds.Select(i => new Album { Id = i }).ToList()
            };
        }

        [SetUp]
        public void Setup()
        {
            _matched = Tracked("matched", MediaKind.Music, Remote(3, "f-3", 301, 302));
            _unmatched = Tracked("unmatched", MediaKind.Music, null);
            _otherArtist = Tracked("other", MediaKind.Music, Remote(4, "f-4", 401));
            _movie = Tracked("movie", MediaKind.Movies, new RemoteMovie { Movie = new Movie { Id = 3, TmdbId = 30 } });

            Mocker.GetMock<ITrackedDownloadService>()
                  .Setup(s => s.GetTrackedDownloads())
                  .Returns(new List<TrackedDownload> { _matched, _unmatched, _otherArtist, _movie });
        }

        private static Artist NewArtist(int id, string foreignId)
        {
            return new Artist { Id = id, Metadata = new ArtistMetadata { ForeignArtistId = foreignId } };
        }

        private void VerifyRematched(params TrackedDownload[] expected)
        {
            Mocker.GetMock<ITrackedDownloadService>()
                  .Verify(s => s.Rematch(It.Is<List<TrackedDownload>>(l => l.OrderBy(t => t.DownloadItem.DownloadId).SequenceEqual(expected.OrderBy(t => t.DownloadItem.DownloadId)))), Times.Once());
        }

        [Test]
        public void artists_deleted_should_rematch_only_the_downloads_matched_to_them()
        {
            Subject.Handle(new ArtistsDeletedEvent(new List<Artist> { NewArtist(3, "f-3") }, false, false));

            VerifyRematched(_matched);
        }

        [Test]
        public void artist_edited_should_rematch_only_the_downloads_matched_to_it()
        {
            Subject.Handle(new ArtistEditedEvent(NewArtist(4, "f-4"), NewArtist(4, "f-4")));

            VerifyRematched(_otherArtist);
        }

        [Test]
        public void artist_added_should_rematch_unmatched_downloads_and_those_of_the_same_foreign_id()
        {
            Subject.Handle(new ArtistAddedEvent(NewArtist(9, "f-3")));

            VerifyRematched(_matched, _unmatched);
        }

        [Test]
        public void album_deleted_should_rematch_only_the_downloads_that_held_it()
        {
            Subject.Handle(new AlbumDeletedEvent(new Album { Id = 302 }, false, false));

            VerifyRematched(_matched);
        }

        [Test]
        public void removed_albums_should_rematch_only_the_downloads_that_held_them()
        {
            Subject.Handle(new AlbumInfoRefreshedEvent(NewArtist(3, "f-3"), new List<Album>(), new List<Album>(), new List<Album> { new Album { Id = 301 } }));

            VerifyRematched(_matched);
        }

        [Test]
        public void refreshed_albums_without_removals_should_not_rematch_anything()
        {
            Subject.Handle(new AlbumInfoRefreshedEvent(NewArtist(3, "f-3"), new List<Album> { new Album { Id = 303 } }, new List<Album> { new Album { Id = 301 } }, new List<Album>()));

            Mocker.GetMock<ITrackedDownloadService>()
                  .Verify(s => s.Rematch(It.Is<List<TrackedDownload>>(l => l.Any())), Times.Never());
        }

        [Test]
        public void track_imported_should_request_a_download_refresh()
        {
            Subject.Handle(new TrackImportedEvent(new LocalTrack(), new TrackFile(), new List<TrackFile>(), true, null));

            Mocker.GetMock<IEventAggregator>()
                  .Verify(e => e.PublishEvent(It.IsAny<RefreshDownloadsRequestedEvent>()), Times.Once());
        }

        [Test]
        public void should_never_rematch_a_download_of_another_media_type()
        {
            Subject.Handle(new ArtistAddedEvent(NewArtist(9, "f-99")));

            Mocker.GetMock<ITrackedDownloadService>()
                  .Verify(s => s.Rematch(It.Is<List<TrackedDownload>>(l => l.Contains(_movie))), Times.Never());
        }
    }
}
