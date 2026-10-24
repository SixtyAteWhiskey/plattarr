using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Cache;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Events;
using NzbDrone.Core.Music.MediaFiles.Events;
using NzbDrone.Core.Music.Notifications;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.Emby;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.NotificationTests
{
    // Emby / Jellyfin takes the album delete and the retag as Lidarr's provider does: both are announced when
    // "Send Notifications" is on, and an album delete also refreshes the artist when "Update Library" is on.
    // Lidarr's provider has no failed-download handler, so this one has none either.
    [TestFixture]
    public class MediaBrowserMusicEventsFixture : CoreTest<MediaBrowser>
    {
        private MediaBrowserSettings _settings;

        [SetUp]
        public void Setup()
        {
            Mocker.SetConstant<ICacheManager>(Mocker.Resolve<CacheManager>());

            _settings = new MediaBrowserSettings { Host = "emby.local", ApiKey = "key", Notify = true, UpdateLibrary = true };

            Subject.Definition = new NotificationDefinition { Settings = _settings };
        }

        private static ItemDeleteMessage AlbumDeleted(bool deletedFiles = true)
        {
            return new ItemDeleteMessage
            {
                Message = "Brothers - Album removed and all files were deleted",
                Subject = MusicGoldenData.Renderer().Describe(MusicGoldenData.Artist()),
                DeletedFiles = deletedFiles
            };
        }

        [Test]
        public void should_support_album_delete_and_retag_but_not_download_failure()
        {
            Subject.SupportsOnAlbumDelete.Should().BeTrue();
            Subject.SupportsOnTrackRetag.Should().BeTrue();
            Subject.SupportsOnDownloadFailure.Should().BeFalse();
        }

        [Test]
        public void should_announce_a_deleted_album_by_the_item_noun_of_the_subject()
        {
            Subject.OnItemDelete(AlbumDeleted());

            Mocker.GetMock<IMediaBrowserService>()
                  .Verify(s => s.Notify(_settings, "Radarr - Album Deleted", "Brothers - Album removed and all files were deleted"), Times.Once());
        }

        [Test]
        public void should_announce_a_deleted_album_without_a_subject_as_an_album()
        {
            Subject.OnItemDelete(new ItemDeleteMessage { Message = "gone" });

            Mocker.GetMock<IMediaBrowserService>()
                  .Verify(s => s.Notify(_settings, "Radarr - Album Deleted", "gone"), Times.Once());

            Subject.ProcessQueue();

            Mocker.GetMock<IMediaBrowserService>()
                  .Verify(s => s.Update(It.IsAny<MediaBrowserSettings>(), It.IsAny<ActivitySubject>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_not_announce_a_deleted_album_when_notifications_are_off()
        {
            _settings.Notify = false;

            Subject.OnItemDelete(AlbumDeleted());

            Mocker.GetMock<IMediaBrowserService>()
                  .Verify(s => s.Notify(It.IsAny<MediaBrowserSettings>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_refresh_the_artist_as_modified_after_an_album_delete()
        {
            var message = AlbumDeleted();

            Subject.OnItemDelete(message);

            // Nothing is sent until the queue runs: the refresh is batched per artist.
            Mocker.GetMock<IMediaBrowserService>()
                  .Verify(s => s.Update(It.IsAny<MediaBrowserSettings>(), It.IsAny<ActivitySubject>(), It.IsAny<string>()), Times.Never());

            Subject.ProcessQueue();

            Mocker.GetMock<IMediaBrowserService>()
                  .Verify(s => s.Update(_settings, message.Subject, "Modified"), Times.Once());
        }

        [Test]
        public void should_refresh_the_artist_even_when_the_files_were_kept()
        {
            // The album is gone from the library either way (Lidarr refreshes regardless of the file flag).
            var message = AlbumDeleted(false);

            Subject.OnItemDelete(message);
            Subject.ProcessQueue();

            Mocker.GetMock<IMediaBrowserService>()
                  .Verify(s => s.Update(_settings, message.Subject, "Modified"), Times.Once());
        }

        [Test]
        public void should_not_refresh_the_library_when_it_is_off()
        {
            _settings.UpdateLibrary = false;

            Subject.OnItemDelete(AlbumDeleted());
            Subject.ProcessQueue();

            Mocker.GetMock<IMediaBrowserService>()
                  .Verify(s => s.Update(It.IsAny<MediaBrowserSettings>(), It.IsAny<ActivitySubject>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_refresh_an_artist_once_for_two_album_deletes()
        {
            var first = AlbumDeleted();
            var second = AlbumDeleted();

            Subject.OnItemDelete(first);
            Subject.OnItemDelete(second);
            Subject.ProcessQueue();

            Mocker.GetMock<IMediaBrowserService>()
                  .Verify(s => s.Update(It.IsAny<MediaBrowserSettings>(), It.IsAny<ActivitySubject>(), "Modified"), Times.Once());
        }

        [Test]
        public void should_leave_the_update_type_to_the_server_when_a_delete_and_an_import_share_the_queue()
        {
            var message = AlbumDeleted();

            Subject.OnItemDelete(message);
            Subject.OnDownload(new DownloadMessage { Subject = message.Subject, Message = "Brothers imported" });
            Subject.ProcessQueue();

            Mocker.GetMock<IMediaBrowserService>()
                  .Verify(s => s.Update(_settings, It.IsAny<ActivitySubject>(), null), Times.Once());
        }

        [Test]
        public void should_announce_a_retag_and_never_refresh_the_library()
        {
            Subject.OnRetag(new RetagMessage { Message = "/music/a.flac:\nTitle: A -> B", Subject = MusicGoldenData.Renderer().Describe(MusicGoldenData.Artist()) });
            Subject.ProcessQueue();

            Mocker.GetMock<IMediaBrowserService>()
                  .Verify(s => s.Notify(_settings, "Radarr - Track Retagged", "/music/a.flac:\nTitle: A -> B"), Times.Once());

            Mocker.GetMock<IMediaBrowserService>()
                  .Verify(s => s.Update(It.IsAny<MediaBrowserSettings>(), It.IsAny<ActivitySubject>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_not_announce_a_retag_when_notifications_are_off()
        {
            _settings.Notify = false;

            Subject.OnRetag(new RetagMessage { Message = "m" });

            Mocker.GetMock<IMediaBrowserService>()
                  .Verify(s => s.Notify(It.IsAny<MediaBrowserSettings>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_do_nothing_for_a_failed_download()
        {
            Subject.OnDownloadFailure(new DownloadFailureMessage { Message = "Failed download detected", SourceTitle = "x" });
            Subject.ProcessQueue();

            Mocker.GetMock<IMediaBrowserService>().VerifyNoOtherCalls();
        }

        [Test]
        public void should_receive_an_album_delete_from_the_music_handler()
        {
            var artist = MusicGoldenData.Artist();
            var album = MusicGoldenData.Album();
            album.Artist = new LazyLoaded<Artist>(artist);

            var handler = new MusicNotificationHandler(new GoldenData.ProviderDispatcher(Subject), MusicGoldenData.Renderer());

            handler.Handle(new AlbumDeletedEvent(album, true, false));
            Subject.ProcessQueue();

            Mocker.GetMock<IMediaBrowserService>()
                  .Verify(s => s.Notify(_settings, "Radarr - Album Deleted", "Brothers - Album removed and all files were deleted"), Times.Once());
            Mocker.GetMock<IMediaBrowserService>()
                  .Verify(s => s.Update(_settings, It.Is<ActivitySubject>(x => x.ParentId == artist.Id && x.ParentPath == "/music/The Black Keys"), "Modified"), Times.Once());
        }

        [Test]
        public void should_receive_a_retag_from_the_music_handler()
        {
            var artist = MusicGoldenData.Artist();
            var album = MusicGoldenData.Album();
            var file = MusicGoldenData.TrackFile(artist, album, 51, "01 - Everlasting Light.flac", AudioQuality.FLAC);

            var handler = new MusicNotificationHandler(new GoldenData.ProviderDispatcher(Subject), MusicGoldenData.Renderer());

            handler.Handle(new TrackFileRetaggedEvent(
                artist,
                file,
                new Dictionary<string, Tuple<string, string>> { { "Title", Tuple.Create("Everlasting Lite", "Everlasting Light") } },
                false));

            Mocker.GetMock<IMediaBrowserService>()
                  .Verify(s => s.Notify(_settings, "Radarr - Track Retagged", It.Is<string>(m => m.Contains("Title: Everlasting Lite -> Everlasting Light"))), Times.Once());
        }
    }
}
