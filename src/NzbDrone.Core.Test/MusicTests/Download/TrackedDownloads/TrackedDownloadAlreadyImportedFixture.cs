using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.History;
using NzbDrone.Core.Music.Import;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.Download.TrackedDownloads
{
    [TestFixture]
    public class TrackedDownloadAlreadyImportedFixture : CoreTest<TrackedDownloadAlreadyImported>
    {
        private List<Album> _albums;
        private TrackedDownload _trackedDownload;
        private List<AlbumHistory> _historyItems;

        [SetUp]
        public void Setup()
        {
            _albums = new List<Album>();

            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                                                  .With(r => r.Albums = _albums)
                                                  .Build();

            var downloadItem = Builder<DownloadClientItem>.CreateNew().Build();

            _trackedDownload = new TrackedDownload
            {
                Kind = MediaKind.Music,
                RemoteRelease = remoteAlbum,
                DownloadItem = downloadItem
            };

            _historyItems = new List<AlbumHistory>();
        }

        private void GivenAlbums(int count)
        {
            _albums.AddRange(Builder<Album>.CreateListOfSize(count).BuildList());
        }

        private void GivenHistoryForAlbum(Album album, params AlbumHistoryEventType[] eventTypes)
        {
            foreach (var eventType in eventTypes)
            {
                _historyItems.Add(Builder<AlbumHistory>.CreateNew()
                                                       .With(h => h.ItemId = album.Id)
                                                       .With(h => h.EventType = eventType)
                                                       .Build());
            }
        }

        [Test]
        public void should_return_false_if_there_is_no_history()
        {
            GivenAlbums(1);

            Subject.IsImported(_trackedDownload, _historyItems).Should().BeFalse();
        }

        [Test]
        public void should_return_false_if_single_album_download_is_not_imported()
        {
            GivenAlbums(1);

            GivenHistoryForAlbum(_albums[0], AlbumHistoryEventType.Grabbed);

            Subject.IsImported(_trackedDownload, _historyItems).Should().BeFalse();
        }

        [Test]
        public void should_return_false_if_no_album_in_multi_album_download_is_imported()
        {
            GivenAlbums(2);

            GivenHistoryForAlbum(_albums[0], AlbumHistoryEventType.Grabbed);
            GivenHistoryForAlbum(_albums[1], AlbumHistoryEventType.Grabbed);

            Subject.IsImported(_trackedDownload, _historyItems).Should().BeFalse();
        }

        [Test]
        public void should_return_false_if_only_one_album_in_multi_album_download_is_imported()
        {
            GivenAlbums(2);

            GivenHistoryForAlbum(_albums[0], AlbumHistoryEventType.DownloadImported, AlbumHistoryEventType.Grabbed);
            GivenHistoryForAlbum(_albums[1], AlbumHistoryEventType.Grabbed);

            Subject.IsImported(_trackedDownload, _historyItems).Should().BeFalse();
        }

        [Test]
        public void should_return_true_if_single_album_download_is_imported()
        {
            GivenAlbums(1);

            GivenHistoryForAlbum(_albums[0], AlbumHistoryEventType.DownloadImported, AlbumHistoryEventType.Grabbed);

            Subject.IsImported(_trackedDownload, _historyItems).Should().BeTrue();
        }

        [Test]
        public void should_return_true_if_multi_album_download_is_imported()
        {
            GivenAlbums(2);

            GivenHistoryForAlbum(_albums[0], AlbumHistoryEventType.DownloadImported, AlbumHistoryEventType.Grabbed);
            GivenHistoryForAlbum(_albums[1], AlbumHistoryEventType.DownloadImported, AlbumHistoryEventType.Grabbed);

            Subject.IsImported(_trackedDownload, _historyItems).Should().BeTrue();
        }

        [Test]
        public void should_count_a_track_file_import_as_imported()
        {
            GivenAlbums(1);

            GivenHistoryForAlbum(_albums[0], AlbumHistoryEventType.TrackFileImported, AlbumHistoryEventType.Grabbed);

            Subject.IsImported(_trackedDownload, _historyItems).Should().BeTrue();
        }

        [Test]
        public void should_return_true_if_the_download_is_not_matched_to_albums_but_has_history()
        {
            GivenAlbums(1);
            GivenHistoryForAlbum(_albums[0], AlbumHistoryEventType.Grabbed);

            _trackedDownload.RemoteRelease = null;

            Subject.IsImported(_trackedDownload, _historyItems).Should().BeTrue();
        }
    }
}
