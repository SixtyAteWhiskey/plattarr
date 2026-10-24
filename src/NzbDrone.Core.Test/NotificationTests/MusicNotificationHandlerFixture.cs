using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Events;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.MediaFiles.Events;
using NzbDrone.Core.Music.Notifications;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.NotificationTests
{
    [TestFixture]
    public class MusicNotificationHandlerFixture : TestBase
    {
        private Mock<INotificationDispatcher> _dispatcher;
        private MusicNotificationHandler _handler;
        private Artist _artist;
        private Album _album;
        private List<TrackFile> _files;

        [SetUp]
        public void Setup()
        {
            _dispatcher = new Mock<INotificationDispatcher>();
            _dispatcher.Setup(d => d.AnyEnabled(It.IsAny<NotificationEvent>())).Returns(true);

            _handler = new MusicNotificationHandler(_dispatcher.Object, MusicGoldenData.Renderer());

            _artist = MusicGoldenData.Artist();
            _album = MusicGoldenData.Album();
            _files = new List<TrackFile>
            {
                MusicGoldenData.TrackFile(_artist, _album, 51, "01.flac", AudioQuality.FLAC),
                MusicGoldenData.TrackFile(_artist, _album, 52, "02.flac", AudioQuality.FLAC)
            };
        }

        [Test]
        public void should_not_notify_for_a_grab_of_another_media_type()
        {
            _handler.Handle(new ReleaseGrabbedEvent(new RemoteMovie { Release = new ReleaseInfo { Title = "Movie.2020" } }) { DownloadId = "HASH" });

            _dispatcher.Verify(d => d.Grab(It.IsAny<GrabMessage>()), Times.Never());
        }

        [Test]
        public void should_notify_a_grab_with_the_message_lidarr_sends()
        {
            GrabMessage sent = null;
            _dispatcher.Setup(d => d.Grab(It.IsAny<GrabMessage>())).Callback<GrabMessage>(m => sent = m);

            _handler.Handle(new ReleaseGrabbedEvent(MusicGoldenData.Remote(_artist, new List<Album> { _album })) { DownloadClient = "qBittorrent", DownloadClientName = "qb", DownloadId = "HASH" });

            sent.Message.Should().Be("The Black Keys - Brothers - [FLAC Proper]");
            sent.QualityName.Should().Be("FLAC");
            sent.DownloadId.Should().Be("HASH");
            sent.DownloadClientType.Should().Be("qBittorrent");
            sent.DownloadClientName.Should().Be("qb");
            sent.Subject.Kind.Should().Be(MediaKind.Music);
            sent.Data.Should().BeOfType<MusicNotificationData>().Which.Artist.Should().BeSameAs(_artist);
            sent.Release.Title.Should().Be("The.Black.Keys-Brothers-2010-FLAC-GRP");
        }

        [Test]
        public void should_build_nothing_when_no_provider_wants_the_event()
        {
            _dispatcher.Setup(d => d.AnyEnabled(It.IsAny<NotificationEvent>())).Returns(false);

            _handler.Handle(new ReleaseGrabbedEvent(MusicGoldenData.Remote(_artist, new List<Album> { _album })));
            _handler.Handle(new AlbumImportedEvent(_artist, _album, MusicGoldenData.Release(), _files, new List<TrackFile>(), true, null));
            _handler.Handle(new ArtistAddCompletedEvent(_artist));
            _handler.Handle(new ArtistRenamedEvent(_artist, new List<RenamedTrackFile>()));
            _handler.Handle(new ArtistsDeletedEvent(new List<Artist> { _artist }, true, false));

            _dispatcher.Verify(d => d.Grab(It.IsAny<GrabMessage>()), Times.Never());
            _dispatcher.Verify(d => d.Download(It.IsAny<DownloadMessage>()), Times.Never());
            _dispatcher.Verify(d => d.MediaAdded(It.IsAny<MediaAddedMessage>()), Times.Never());
            _dispatcher.Verify(d => d.Rename(It.IsAny<RenameMessage>()), Times.Never());
            _dispatcher.Verify(d => d.MediaDelete(It.IsAny<MediaDeleteMessage>()), Times.Never());
        }

        [Test]
        public void should_not_notify_an_import_that_is_not_a_new_download()
        {
            _handler.Handle(new AlbumImportedEvent(_artist, _album, MusicGoldenData.Release(), _files, new List<TrackFile>(), false, null));

            _dispatcher.Verify(d => d.Download(It.IsAny<DownloadMessage>()), Times.Never());
        }

        [Test]
        public void should_notify_an_imported_album_once_with_its_track_count()
        {
            DownloadMessage sent = null;
            _dispatcher.Setup(d => d.Download(It.IsAny<DownloadMessage>())).Callback<DownloadMessage>(m => sent = m);

            _handler.Handle(new AlbumImportedEvent(_artist, _album, MusicGoldenData.Release(), _files, new List<TrackFile>(), true, null));

            _dispatcher.Verify(d => d.Download(It.IsAny<DownloadMessage>()), Times.Once());
            sent.Message.Should().Be("The Black Keys - Brothers (2 Tracks Imported)");
            sent.IsUpgrade.Should().BeFalse();
            sent.File.Id.Should().Be(51);
            sent.SourcePath.Should().Be("Brothers/01.flac");
            sent.Data.Should().BeOfType<MusicNotificationData>().Which.TrackFiles.Should().HaveCount(2);
        }

        [Test]
        public void should_report_an_import_that_replaced_files_as_an_upgrade()
        {
            DownloadMessage sent = null;
            _dispatcher.Setup(d => d.Download(It.IsAny<DownloadMessage>())).Callback<DownloadMessage>(m => sent = m);

            var old = new List<TrackFile> { MusicGoldenData.TrackFile(_artist, _album, 49, "01.mp3", AudioQuality.MP3_320) };

            _handler.Handle(new AlbumImportedEvent(_artist, _album, MusicGoldenData.Release(), _files, old, true, null));

            sent.IsUpgrade.Should().BeTrue();
            sent.OldFiles.Should().ContainSingle().Which.Id.Should().Be(49);
        }

        [Test]
        public void should_send_one_delete_message_per_artist()
        {
            var other = MusicGoldenData.Artist();
            other.Id = 2;

            _handler.Handle(new ArtistsDeletedEvent(new List<Artist> { _artist, other }, true, false));

            _dispatcher.Verify(d => d.MediaDelete(It.IsAny<MediaDeleteMessage>()), Times.Exactly(2));
        }

        [Test]
        public void should_notify_a_rename_with_the_previous_path_relative_to_the_artist_folder()
        {
            RenameMessage sent = null;
            _dispatcher.Setup(d => d.Rename(It.IsAny<RenameMessage>())).Callback<RenameMessage>(m => sent = m);

            _handler.Handle(new ArtistRenamedEvent(_artist, new List<RenamedTrackFile> { new RenamedTrackFile { TrackFile = _files[0], PreviousPath = "/music/The Black Keys/Brothers/old.flac" } }));

            var file = sent.Files.Should().ContainSingle().Subject;

            file.File.Id.Should().Be(51);
            file.PreviousPath.Should().Be("/music/The Black Keys/Brothers/old.flac");
            file.PreviousRelativePath.Should().Be("Brothers/old.flac");
        }

        [Test]
        public void should_not_notify_manual_interaction_published_by_another_media_type()
        {
            var tracked = MusicGoldenData.Tracked(MusicGoldenData.Remote(_artist, new List<Album> { _album }));

            _handler.Handle(new ManualInteractionRequiredEvent(tracked, new NzbDrone.Core.Movies.Notifications.MovieNotificationData()));

            _dispatcher.Verify(d => d.ManualInteractionRequired(It.IsAny<ManualInteractionRequiredMessage>()), Times.Never());
        }

        [Test]
        public void should_notify_manual_interaction_with_the_lidarr_message()
        {
            ManualInteractionRequiredMessage sent = null;
            _dispatcher.Setup(d => d.ManualInteractionRequired(It.IsAny<ManualInteractionRequiredMessage>())).Callback<ManualInteractionRequiredMessage>(m => sent = m);

            var remote = MusicGoldenData.Remote(_artist, new List<Album> { _album });

            _handler.Handle(new ManualInteractionRequiredEvent(MusicGoldenData.Tracked(remote), new MusicNotificationData { Artist = _artist, RemoteAlbum = remote }));

            sent.Message.Should().Be("The Black Keys - Brothers - [FLAC Proper]");
            sent.QualityName.Should().Be("FLAC");
            sent.ReleaseGroup.Should().Be("GRP");
            sent.DownloadId.Should().Be("HASH123");
            sent.Subject.ItemLabel.Should().Be("Brothers");
        }

        [Test]
        public void should_use_the_download_title_for_manual_interaction_on_an_unmatched_download()
        {
            ManualInteractionRequiredMessage sent = null;
            _dispatcher.Setup(d => d.ManualInteractionRequired(It.IsAny<ManualInteractionRequiredMessage>())).Callback<ManualInteractionRequiredMessage>(m => sent = m);

            _handler.Handle(new ManualInteractionRequiredEvent(MusicGoldenData.Tracked(null), new MusicNotificationData()));

            sent.Message.Should().Be("The.Black.Keys-Brothers-2010-FLAC-GRP");
            sent.Subject.Should().BeNull();
            sent.Release.Should().BeNull();
        }

        [TestCase(1, "The Black Keys - Brothers - [FLAC]")]
        [TestCase(2, "The Black Keys - Brothers - [FLAC Proper]")]
        [TestCase(3, "The Black Keys - Brothers - [FLAC Proper]")]
        public void should_format_the_message_by_revision(int version, string expected)
        {
            MusicNotificationHandler.GetMessage(_artist, new List<Album> { _album }, new AudioQualityModel(AudioQuality.FLAC, new Revision(version))).Should().Be(expected);
        }

        [Test]
        public void should_join_the_titles_of_several_albums()
        {
            var albums = new List<Album> { _album, MusicGoldenData.Album(22, "El Camino") };

            MusicNotificationHandler.GetMessage(_artist, albums, new AudioQualityModel(AudioQuality.MP3_320))
                .Should().Be("The Black Keys - Brothers + El Camino - [MP3-320]");
        }

        [Test]
        public void should_notify_a_deleted_album_with_lidarrs_text()
        {
            ItemDeleteMessage sent = null;
            _dispatcher.Setup(d => d.ItemDelete(It.IsAny<ItemDeleteMessage>())).Callback<ItemDeleteMessage>(m => sent = m);
            _album.Artist = new NzbDrone.Core.Datastore.LazyLoaded<Artist>(_artist);

            _handler.Handle(new AlbumDeletedEvent(_album, true, false));

            sent.Message.Should().Be("Brothers - Album removed and all files were deleted");
            sent.DeletedFiles.Should().BeTrue();
            sent.DeletedFilesMessage.Should().Be("Album removed and all files were deleted");
            sent.Subject.Tags.Should().BeEquivalentTo(_artist.Tags);
            ((MusicNotificationData)sent.Data).Album.Should().BeSameAs(_album);
            ((MusicNotificationData)sent.Data).Artist.Should().BeSameAs(_artist);
        }

        [Test]
        public void should_say_when_the_files_of_a_deleted_album_were_kept()
        {
            ItemDeleteMessage sent = null;
            _dispatcher.Setup(d => d.ItemDelete(It.IsAny<ItemDeleteMessage>())).Callback<ItemDeleteMessage>(m => sent = m);
            _album.Artist = new NzbDrone.Core.Datastore.LazyLoaded<Artist>(_artist);

            _handler.Handle(new AlbumDeletedEvent(_album, false, false));

            sent.Message.Should().Be("Brothers - Album removed, files were not deleted");
            sent.DeletedFiles.Should().BeFalse();
        }

        [Test]
        public void should_not_notify_a_deleted_album_whose_artist_is_gone()
        {
            _album.Artist = new NzbDrone.Core.Datastore.LazyLoaded<Artist>(null);

            _handler.Handle(new AlbumDeletedEvent(_album, true, false));

            _dispatcher.Verify(d => d.ItemDelete(It.IsAny<ItemDeleteMessage>()), Times.Never());
        }

        [Test]
        public void should_build_nothing_for_a_deleted_album_when_no_provider_wants_it()
        {
            _dispatcher.Setup(d => d.AnyEnabled(NotificationEvent.ItemDelete)).Returns(false);
            _album.Artist = new NzbDrone.Core.Datastore.LazyLoaded<Artist>(_artist);

            _handler.Handle(new AlbumDeletedEvent(_album, true, false));

            _dispatcher.Verify(d => d.ItemDelete(It.IsAny<ItemDeleteMessage>()), Times.Never());
        }

        private DownloadFailedEvent NewFailure(MediaKind kind)
        {
            return new DownloadFailedEvent
            {
                Kind = kind,
                ParentId = _artist.Id,
                ItemIds = new List<int> { _album.Id },
                Quality = new AudioQualityModel(AudioQuality.FLAC, new Revision(2)).ToDisplayModel(),
                SourceTitle = "The.Black.Keys-Brothers-2010-FLAC-GRP",
                DownloadClient = "qb-main",
                DownloadId = "HASH123",
                Message = "Failed download detected",
                TrackedDownload = MusicGoldenData.Tracked(MusicGoldenData.Remote(_artist, new List<Album> { _album }))
            };
        }

        [Test]
        public void should_notify_a_failed_download_with_the_release_and_the_client()
        {
            DownloadFailureMessage sent = null;
            _dispatcher.Setup(d => d.DownloadFailure(It.IsAny<DownloadFailureMessage>())).Callback<DownloadFailureMessage>(m => sent = m);

            _handler.Handle(NewFailure(MediaKind.Music));

            sent.Message.Should().Be("Failed download detected");
            sent.SourceTitle.Should().Be("The.Black.Keys-Brothers-2010-FLAC-GRP");
            sent.QualityName.Should().Be("FLAC");
            sent.QualityVersion.Should().Be(2);
            sent.DownloadClient.Should().Be("qb-main");
            sent.DownloadId.Should().Be("HASH123");
            sent.Subject.Tags.Should().BeEquivalentTo(_artist.Tags);
            ((MusicNotificationData)sent.Data).Artist.Should().BeSameAs(_artist);
        }

        [TestCase(MediaKind.Movies)]
        [TestCase(MediaKind.Tv)]
        public void should_not_notify_a_failed_download_of_another_media_type(MediaKind kind)
        {
            _handler.Handle(NewFailure(kind));

            _dispatcher.Verify(d => d.DownloadFailure(It.IsAny<DownloadFailureMessage>()), Times.Never());
        }

        [Test]
        public void should_still_notify_a_failed_download_that_is_no_longer_tracked()
        {
            var failure = NewFailure(MediaKind.Music);
            failure.TrackedDownload = null;

            _handler.Handle(failure);

            _dispatcher.Verify(d => d.DownloadFailure(It.Is<DownloadFailureMessage>(m => m.DownloadId == "HASH123")), Times.Once());
        }

        [Test]
        public void should_build_nothing_for_a_failed_download_when_no_provider_wants_it()
        {
            _dispatcher.Setup(d => d.AnyEnabled(NotificationEvent.DownloadFailure)).Returns(false);

            _handler.Handle(NewFailure(MediaKind.Music));

            _dispatcher.Verify(d => d.DownloadFailure(It.IsAny<DownloadFailureMessage>()), Times.Never());
        }

        [Test]
        public void should_notify_a_retag_with_the_tag_changes_listed()
        {
            RetagMessage sent = null;
            _dispatcher.Setup(d => d.Retag(It.IsAny<RetagMessage>())).Callback<RetagMessage>(m => sent = m);
            var diff = new Dictionary<string, Tuple<string, string>>
            {
                { "Title", Tuple.Create("Everlasting Lite", "Everlasting Light") },
                { "Disc", Tuple.Create<string, string>(null, "1") }
            };

            _handler.Handle(new TrackFileRetaggedEvent(_artist, _files[0], diff, true));

            sent.Message.Should().Be(_files[0].Path + ":\nTitle: Everlasting Lite -> Everlasting Light\nDisc: <missing> -> 1");
            sent.Scrubbed.Should().BeTrue();
            sent.Diff.Should().BeSameAs(diff);
            sent.File.Id.Should().Be(51);
            ((MusicNotificationData)sent.Data).TrackFiles.Should().ContainSingle().Which.Should().BeSameAs(_files[0]);
            ((MusicNotificationData)sent.Data).AlbumRelease.Id.Should().Be(31);
        }

        [Test]
        public void should_build_nothing_for_a_retag_when_no_provider_wants_it()
        {
            _dispatcher.Setup(d => d.AnyEnabled(NotificationEvent.Retag)).Returns(false);

            _handler.Handle(new TrackFileRetaggedEvent(_artist, _files[0], new Dictionary<string, Tuple<string, string>>(), false));

            _dispatcher.Verify(d => d.Retag(It.IsAny<RetagMessage>()), Times.Never());
        }
    }
}
