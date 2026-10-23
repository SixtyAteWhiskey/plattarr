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
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Downloads;
using NzbDrone.Core.Tv.Events;
using NzbDrone.Core.Tv.MediaFiles;
using NzbDrone.Core.Tv.MediaFiles.Events;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Test.Download.TrackedDownloads
{
    [TestFixture]
    public class TvTrackedDownloadRefresherFixture : CoreTest<TvTrackedDownloadRefresher>
    {
        private TrackedDownload _matched;
        private TrackedDownload _unmatched;
        private TrackedDownload _otherSeries;
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

        private static RemoteEpisode Remote(int seriesId, int tvdbId, params int[] episodeIds)
        {
            return new RemoteEpisode
            {
                Series = new Series { Id = seriesId, TvdbId = tvdbId },
                Episodes = episodeIds.Select(i => new Episode { Id = i }).ToList()
            };
        }

        [SetUp]
        public void Setup()
        {
            _matched = Tracked("matched", MediaKind.Tv, Remote(3, 30, 301, 302));
            _unmatched = Tracked("unmatched", MediaKind.Tv, null);
            _otherSeries = Tracked("other", MediaKind.Tv, Remote(4, 40, 401));
            _movie = Tracked("movie", MediaKind.Movies, new RemoteMovie { Movie = new Movie { Id = 3, TmdbId = 30 } });

            Mocker.GetMock<ITrackedDownloadService>()
                  .Setup(s => s.GetTrackedDownloads())
                  .Returns(new List<TrackedDownload> { _matched, _unmatched, _otherSeries, _movie });
        }

        private void VerifyRematched(params TrackedDownload[] expected)
        {
            Mocker.GetMock<ITrackedDownloadService>()
                  .Verify(s => s.Rematch(It.Is<List<TrackedDownload>>(l => l.OrderBy(t => t.DownloadItem.DownloadId).SequenceEqual(expected.OrderBy(t => t.DownloadItem.DownloadId)))), Times.Once());
        }

        [Test]
        public void series_deleted_should_rematch_only_the_downloads_matched_to_it()
        {
            Subject.Handle(new SeriesDeletedEvent(new List<Series> { new Series { Id = 3, TvdbId = 30 } }, false, false));

            VerifyRematched(_matched);
        }

        [Test]
        public void series_edited_should_rematch_only_the_downloads_matched_to_it()
        {
            Subject.Handle(new SeriesEditedEvent(new Series { Id = 4, TvdbId = 40 }, new Series { Id = 4, TvdbId = 40 }));

            VerifyRematched(_otherSeries);
        }

        [Test]
        public void series_bulk_edited_should_rematch_the_downloads_matched_to_any_of_them()
        {
            Subject.Handle(new SeriesBulkEditedEvent(new List<Series> { new Series { Id = 3, TvdbId = 30 }, new Series { Id = 4, TvdbId = 40 } }));

            VerifyRematched(_matched, _otherSeries);
        }

        [Test]
        public void series_added_should_rematch_unmatched_downloads_and_those_of_the_same_tvdb_id()
        {
            Subject.Handle(new SeriesAddedEvent(new Series { Id = 9, TvdbId = 30 }));

            VerifyRematched(_matched, _unmatched);
        }

        [Test]
        public void removed_episodes_should_rematch_only_the_downloads_that_held_them()
        {
            var series = new Series { Id = 3, TvdbId = 30 };

            Subject.Handle(new EpisodeInfoRefreshedEvent(series, new List<Episode>(), new List<Episode>(), new List<Episode> { new Episode { Id = 302 } }));

            VerifyRematched(_matched);
        }

        [Test]
        public void refreshed_episodes_without_removals_should_not_rematch_anything()
        {
            var series = new Series { Id = 3, TvdbId = 30 };

            Subject.Handle(new EpisodeInfoRefreshedEvent(series, new List<Episode> { new Episode { Id = 303 } }, new List<Episode> { new Episode { Id = 301 } }, new List<Episode>()));

            Mocker.GetMock<ITrackedDownloadService>()
                  .Verify(s => s.Rematch(It.Is<List<TrackedDownload>>(l => l.Any())), Times.Never());
        }

        [Test]
        public void episode_imported_should_request_a_download_refresh()
        {
            Subject.Handle(new EpisodeImportedEvent(new LocalEpisode(), new EpisodeFile(), new List<DeletedEpisodeFile>(), true, null));

            Mocker.GetMock<IEventAggregator>()
                  .Verify(e => e.PublishEvent(It.IsAny<RefreshDownloadsRequestedEvent>()), Times.Once());
        }

        [Test]
        public void should_never_rematch_a_download_of_another_media_type()
        {
            Subject.Handle(new SeriesAddedEvent(new Series { Id = 9, TvdbId = 99 }));

            Mocker.GetMock<ITrackedDownloadService>()
                  .Verify(s => s.Rematch(It.Is<List<TrackedDownload>>(l => l.Contains(_movie))), Times.Never());
        }
    }
}
