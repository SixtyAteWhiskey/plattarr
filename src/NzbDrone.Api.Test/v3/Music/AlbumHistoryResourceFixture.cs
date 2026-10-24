using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.CustomFormats;
using NzbDrone.Core.Music.DecisionEngine.Specifications;
using NzbDrone.Core.Music.History;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Qualities;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Music.History;
using Radarr.Http;

namespace NzbDrone.Api.Test.v3.Music
{
    [TestFixture]
    public class AlbumHistoryControllerFixture : TestBase<AlbumHistoryController>
    {
        private Artist _artist;
        private Album _album;
        private PagingSpec<AlbumHistory> _spec;
        private int[] _qualities;

        [SetUp]
        public void Setup()
        {
            _artist = new Artist
            {
                Id = 12,
                Metadata = new ArtistMetadata { Name = "The Black Keys", Images = new List<NzbDrone.Core.MediaCover.MediaCover>() },
                Tags = new HashSet<int>(),
                QualityProfile = new AudioQualityProfile { Name = "Lossless" }
            };

            _album = new Album
            {
                Id = 101,
                Title = "Brothers",
                Images = new List<NzbDrone.Core.MediaCover.MediaCover>(),
                SecondaryTypes = new List<SecondaryAlbumType>(),
                Genres = new List<string>()
            };

            _spec = null;
            _qualities = null;

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.Paged(It.IsAny<PagingSpec<AlbumHistory>>(), It.IsAny<int[]>()))
                  .Callback<PagingSpec<AlbumHistory>, int[]>((spec, qualities) =>
                  {
                      _spec = spec;
                      _qualities = qualities;
                  })
                  .Returns<PagingSpec<AlbumHistory>, int[]>((spec, qualities) =>
                  {
                      spec.Records = new List<AlbumHistory> { NewHistory() };
                      spec.TotalRecords = 1;

                      return spec;
                  });

            Mocker.GetMock<IArtistService>().Setup(s => s.GetArtist(12)).Returns(_artist);
            Mocker.GetMock<IAlbumService>().Setup(s => s.GetAlbum(101)).Returns(_album);

            Mocker.GetMock<IMusicCustomFormatCalculationService>()
                  .Setup(c => c.ParseCustomFormat(It.IsAny<AlbumHistory>(), It.IsAny<Artist>()))
                  .Returns(new List<CustomFormat>());

            Mocker.GetMock<IUpgradableSpecification>()
                  .Setup(s => s.QualityCutoffNotMet(It.IsAny<AudioQualityProfile>(), It.IsAny<AudioQualityModel>(), null))
                  .Returns(true);
        }

        private AlbumHistory NewHistory(int trackId = 0)
        {
            var history = new AlbumHistory
            {
                Id = 3,
                MediaKind = MediaKind.Music,
                ParentId = 12,
                ItemId = 101,
                SourceTitle = "The Black Keys - Brothers [FLAC]",
                Quality = new AudioQualityModel(AudioQuality.FLAC),
                Date = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                EventType = AlbumHistoryEventType.TrackFileImported,
                DownloadId = "dl-1"
            };

            if (trackId > 0)
            {
                history.Data[AlbumHistory.TRACK_ID] = trackId.ToString();
            }

            return history;
        }

        private static bool Matches(PagingSpec<AlbumHistory> spec, AlbumHistory row)
        {
            return spec.FilterExpressions.All(e => e.Compile()(row));
        }

        [Test]
        public void should_sort_newest_first_by_default_and_ignore_a_sort_key_that_is_not_allowed()
        {
            Subject.GetHistory(new PagingRequestResource { SortKey = "quality" }, false, false, false, null, null, null);

            _spec.SortKey.Should().Be("date");
            _spec.SortDirection.Should().Be(SortDirection.Descending);
        }

        [TestCase("artists.sortName")]
        [TestCase("albums.title")]
        public void should_allow_sorting_by_the_joined_artist_and_album(string sortKey)
        {
            Subject.GetHistory(new PagingRequestResource { SortKey = sortKey, SortDirection = SortDirection.Ascending }, false, false, false, null, null, null);

            _spec.SortKey.Should().Be(sortKey);
            _spec.SortDirection.Should().Be(SortDirection.Ascending);
        }

        [Test]
        public void should_filter_by_event_type_album_download_id_and_artist_on_the_shared_columns()
        {
            Subject.GetHistory(new PagingRequestResource(), false, false, false, new[] { (int)AlbumHistoryEventType.TrackFileImported }, 101, "dl-1", new[] { 12 });

            _spec.FilterExpressions.Should().HaveCount(4);
            Matches(_spec, NewHistory()).Should().BeTrue();

            var otherAlbum = NewHistory();
            otherAlbum.ItemId = 102;
            Matches(_spec, otherAlbum).Should().BeFalse();

            var otherArtist = NewHistory();
            otherArtist.ParentId = 13;
            Matches(_spec, otherArtist).Should().BeFalse();

            var otherEvent = NewHistory();
            otherEvent.EventType = AlbumHistoryEventType.Grabbed;
            Matches(_spec, otherEvent).Should().BeFalse();

            var otherDownload = NewHistory();
            otherDownload.DownloadId = "dl-2";
            Matches(_spec, otherDownload).Should().BeFalse();
        }

        [Test]
        public void should_not_filter_when_no_filter_is_given_and_pass_the_quality_filter_to_the_service()
        {
            Subject.GetHistory(new PagingRequestResource(), false, false, false, null, null, null, null, new[] { 205, 206 });

            _spec.FilterExpressions.Should().BeEmpty();
            _qualities.Should().Equal(205, 206);
        }

        [Test]
        public void should_include_the_artist_album_and_track_only_when_asked()
        {
            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.Paged(It.IsAny<PagingSpec<AlbumHistory>>(), It.IsAny<int[]>()))
                  .Returns<PagingSpec<AlbumHistory>, int[]>((spec, qualities) =>
                  {
                      spec.Records = new List<AlbumHistory> { NewHistory(trackId: 55) };

                      return spec;
                  });
            Mocker.GetMock<ITrackService>().Setup(s => s.GetTrack(55)).Returns(new Track { Id = 55, Title = "Everlasting Light", Ratings = new Ratings(), Artist = new LazyLoaded<Artist>(_artist) });

            var without = Subject.GetHistory(new PagingRequestResource(), false, false, false, null, null, null).Records.Single();

            without.Artist.Should().BeNull();
            without.Album.Should().BeNull();
            without.Track.Should().BeNull();
            without.TrackId.Should().Be(55);

            var with = Subject.GetHistory(new PagingRequestResource(), true, true, true, null, null, null).Records.Single();

            with.Artist.ArtistName.Should().Be("The Black Keys");
            with.Album.Title.Should().Be("Brothers");
            with.Track.Title.Should().Be("Everlasting Light");
        }

        [Test]
        public void should_map_the_shared_columns_and_flag_a_quality_cutoff_that_is_not_met()
        {
            var resource = Subject.GetHistory(new PagingRequestResource(), false, false, false, null, null, null).Records.Single();

            resource.Id.Should().Be(3);
            resource.ArtistId.Should().Be(12);
            resource.AlbumId.Should().Be(101);
            resource.TrackId.Should().Be(0);
            resource.EventType.Should().Be(AlbumHistoryEventType.TrackFileImported);
            resource.Quality.Quality.Should().Be(AudioQuality.FLAC);
            resource.DownloadId.Should().Be("dl-1");
            resource.QualityCutoffNotMet.Should().BeTrue();
        }

        [Test]
        public void should_mark_a_music_history_row_as_failed_by_media_kind()
        {
            Subject.MarkAsFailed(3);

            Mocker.GetMock<IFailedDownloadService>().Verify(s => s.MarkAsFailed(MediaKind.Music, 3, false), Times.Once());
        }

        [Test]
        public void should_return_the_history_of_one_album_with_the_requested_artist()
        {
            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.GetByAlbum(101, null))
                  .Returns(new List<AlbumHistory> { NewHistory() });

            var records = Subject.GetArtistHistory(12, 101, null, true, false, false);

            records.Should().ContainSingle().Which.Artist.ArtistName.Should().Be("The Black Keys");
            Mocker.GetMock<IHistoryService>().Verify(s => s.GetByArtist(It.IsAny<int>(), It.IsAny<AlbumHistoryEventType?>()), Times.Never());
        }
    }
}
