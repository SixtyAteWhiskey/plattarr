using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.MediaFiles.TrackImport;
using NzbDrone.Core.Music.MediaFiles.TrackImport.Manual;
using NzbDrone.Core.Qualities;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Music.ManualImport;
using Radarr.Http.REST;

namespace NzbDrone.Api.Test.v3.Music
{
    [TestFixture]
    public class MusicManualImportControllerFixture : TestBase<MusicManualImportController>
    {
        private Artist _artist;
        private Album _album;
        private AlbumRelease _release;

        [SetUp]
        public void Setup()
        {
            _artist = new Artist
            {
                Id = 12,
                Metadata = new ArtistMetadata { Name = "The Black Keys", Images = new List<NzbDrone.Core.MediaCover.MediaCover>() },
                Tags = new HashSet<int>()
            };

            _album = new Album
            {
                Id = 101,
                Title = "Brothers",
                Images = new List<NzbDrone.Core.MediaCover.MediaCover>(),
                SecondaryTypes = new List<SecondaryAlbumType>(),
                Genres = new List<string>()
            };

            _release = new AlbumRelease { Id = 201 };

            Mocker.GetMock<IArtistService>().Setup(s => s.GetArtist(12)).Returns(_artist);
            Mocker.GetMock<IAlbumService>().Setup(s => s.GetAlbum(101)).Returns(_album);
            Mocker.GetMock<IReleaseService>().Setup(s => s.GetRelease(201)).Returns(_release);
        }

        private ManualImportItem NewItem(AudioQualityModel quality = null)
        {
            return new ManualImportItem
            {
                Id = 7,
                Path = "/downloads/01.flac",
                Name = "01",
                Size = 5,
                Artist = _artist,
                Album = _album,
                Release = _release,
                Quality = quality ?? new AudioQualityModel(AudioQuality.FLAC),
                ReleaseGroup = "GRP",
                DownloadId = "dl-1",
                Tracks = new List<Track> { new Track { Id = 301, Title = "Everlasting Light", Ratings = new Ratings(), Artist = new NzbDrone.Core.Datastore.LazyLoaded<Artist>(_artist) } },
                Rejections = new List<ImportRejection> { new ImportRejection(ImportRejectionReason.UnmatchedTrack, "Unmatched", RejectionType.Temporary) },
                ReplaceExistingFiles = true
            };
        }

        [TestCase(true, FilterFilesType.Matched)]
        [TestCase(false, FilterFilesType.None)]
        public void should_filter_existing_files_as_asked(bool filterExistingFiles, FilterFilesType expected)
        {
            Mocker.GetMock<IManualImportService>()
                  .Setup(s => s.GetMediaFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Artist>(), It.IsAny<FilterFilesType>(), It.IsAny<bool>()))
                  .Returns(new List<ManualImportItem>());

            Subject.GetMediaFiles("/downloads", null, null, filterExistingFiles, false);

            Mocker.GetMock<IManualImportService>().Verify(s => s.GetMediaFiles("/downloads", null, null, expected, false), Times.Once());
        }

        [Test]
        public void should_refuse_a_listing_with_neither_a_folder_nor_a_download_id()
        {
            Assert.Throws<BadRequestException>(() => Subject.GetMediaFiles(null, null, null));
            Assert.Throws<BadRequestException>(() => Subject.GetMediaFiles(" ", "", 12));

            Mocker.GetMock<IManualImportService>().Verify(
                s => s.GetMediaFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Artist>(), It.IsAny<FilterFilesType>(), It.IsAny<bool>()),
                Times.Never());
        }

        [Test]
        public void should_list_a_download_without_a_folder()
        {
            Mocker.GetMock<IManualImportService>()
                  .Setup(s => s.GetMediaFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Artist>(), It.IsAny<FilterFilesType>(), It.IsAny<bool>()))
                  .Returns(new List<ManualImportItem>());

            Subject.GetMediaFiles(null, "dl-1", null);

            Mocker.GetMock<IManualImportService>().Verify(s => s.GetMediaFiles(null, "dl-1", null, FilterFilesType.Matched, true), Times.Once());
        }

        [Test]
        public void should_pass_the_artist_when_an_artist_id_is_given()
        {
            Mocker.GetMock<IManualImportService>()
                  .Setup(s => s.GetMediaFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Artist>(), It.IsAny<FilterFilesType>(), It.IsAny<bool>()))
                  .Returns(new List<ManualImportItem>());

            Subject.GetMediaFiles("/downloads", "dl-1", 12);

            Mocker.GetMock<IManualImportService>().Verify(s => s.GetMediaFiles("/downloads", "dl-1", _artist, FilterFilesType.Matched, true), Times.Once());
        }

        [Test]
        public void should_map_the_item_and_weigh_its_audio_quality()
        {
            Mocker.GetMock<IManualImportService>()
                  .Setup(s => s.GetMediaFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Artist>(), It.IsAny<FilterFilesType>(), It.IsAny<bool>()))
                  .Returns(new List<ManualImportItem> { NewItem() });

            var resource = Subject.GetMediaFiles("/downloads", null, null).Single();

            resource.Id.Should().Be(7);
            resource.Artist.ArtistName.Should().Be("The Black Keys");
            resource.Album.Title.Should().Be("Brothers");
            resource.AlbumReleaseId.Should().Be(201);
            resource.Tracks.Should().ContainSingle(t => t.Title == "Everlasting Light");
            resource.Quality.Quality.Should().Be(AudioQuality.FLAC);
            resource.QualityWeight.Should().Be(AudioQuality.DefaultQualityDefinitions.Single(d => d.Quality == AudioQuality.FLAC).Weight + 1);
            resource.DownloadId.Should().Be("dl-1");
            resource.Rejections.Should().ContainSingle(r => r.Reason == "Unmatched" && r.Type == RejectionType.Temporary);
        }

        [Test]
        public void should_weigh_a_real_and_a_version_revision_into_the_quality_weight()
        {
            var item = NewItem(new AudioQualityModel(AudioQuality.FLAC, new Revision(3, 2)));

            Mocker.GetMock<IManualImportService>()
                  .Setup(s => s.GetMediaFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Artist>(), It.IsAny<FilterFilesType>(), It.IsAny<bool>()))
                  .Returns(new List<ManualImportItem> { item });

            var resource = Subject.GetMediaFiles("/downloads", null, null).Single();

            resource.QualityWeight.Should().Be(AudioQuality.DefaultQualityDefinitions.Single(d => d.Quality == AudioQuality.FLAC).Weight + 20 + 3);
        }

        [Test]
        public void should_leave_the_weight_alone_for_an_item_without_a_quality()
        {
            var item = NewItem();
            item.Quality = null;

            Mocker.GetMock<IManualImportService>()
                  .Setup(s => s.GetMediaFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Artist>(), It.IsAny<FilterFilesType>(), It.IsAny<bool>()))
                  .Returns(new List<ManualImportItem> { item });

            Subject.GetMediaFiles("/downloads", null, null).Single().QualityWeight.Should().Be(0);
        }

        [Test]
        public void should_look_up_the_artist_album_and_release_when_updating_items()
        {
            List<ManualImportItem> sent = null;

            Mocker.GetMock<IManualImportService>()
                  .Setup(s => s.UpdateItems(It.IsAny<List<ManualImportItem>>()))
                  .Callback<List<ManualImportItem>>(items => sent = items)
                  .Returns<List<ManualImportItem>>(items => items);

            var result = Subject.UpdateItems(new List<MusicManualImportUpdateResource>
            {
                new MusicManualImportUpdateResource
                {
                    Id = 7,
                    Path = "/downloads/01.flac",
                    ArtistId = 12,
                    AlbumId = 101,
                    AlbumReleaseId = 201,
                    Quality = new AudioQualityModel(AudioQuality.FLAC),
                    DisableReleaseSwitching = true,
                    ReplaceExistingFiles = true
                },
                new MusicManualImportUpdateResource { Id = 8, Path = "/downloads/02.flac" }
            });

            result.Should().BeOfType<AcceptedResult>();
            sent.Should().HaveCount(2);
            sent[0].Artist.Should().BeSameAs(_artist);
            sent[0].Album.Should().BeSameAs(_album);
            sent[0].Release.Should().BeSameAs(_release);
            sent[0].DisableReleaseSwitching.Should().BeTrue();
            sent[0].ReplaceExistingFiles.Should().BeTrue();
            sent[1].Artist.Should().BeNull();
            sent[1].Album.Should().BeNull();
            sent[1].Release.Should().BeNull();
        }
    }
}
