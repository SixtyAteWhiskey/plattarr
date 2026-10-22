using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.History;
using MovieHistory = NzbDrone.Core.Movies.History.MovieHistory;
using MovieHistoryEventType = NzbDrone.Core.Movies.History.MovieHistoryEventType;
using MovieHistoryRepository = NzbDrone.Core.Movies.History.HistoryRepository;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class HistoryRepositoryFixture : DbTest<HistoryRepository, EpisodeHistory>
    {
        private Series _series;
        private Series _other;
        private Episode _episode1;
        private Episode _episode2;
        private Episode _otherEpisode;

        [SetUp]
        public void Setup()
        {
            var seriesRepository = Mocker.Resolve<SeriesRepository>();
            var episodeRepository = Mocker.Resolve<EpisodeRepository>();

            _series = TvTestData.NewSeries(81189, "Breaking Bad");
            _other = TvTestData.NewSeries(121361, "Game Of Thrones");
            seriesRepository.Insert(_series);
            seriesRepository.Insert(_other);

            _episode1 = TvTestData.NewEpisode(_series.Id, 1, 1);
            _episode2 = TvTestData.NewEpisode(_series.Id, 2, 1);
            _otherEpisode = TvTestData.NewEpisode(_other.Id, 1, 1);
            episodeRepository.Insert(_episode1);
            episodeRepository.Insert(_episode2);
            episodeRepository.Insert(_otherEpisode);
        }

        private EpisodeHistory NewHistory(Series series, Episode episode, EpisodeHistoryEventType type = EpisodeHistoryEventType.Grabbed, DateTime? date = null, string downloadId = null, Quality quality = null, params Language[] languages)
        {
            return new EpisodeHistory
            {
                MediaKind = MediaKind.Tv,
                ParentId = series.Id,
                ItemId = episode.Id,
                SourceTitle = "Show.S01E01.720p",
                Quality = new QualityModel(quality ?? Quality.HDTV720p),
                Date = date ?? DateTime.UtcNow,
                EventType = type,
                DownloadId = downloadId,
                Languages = languages.Length == 0 ? new List<Language> { Language.English } : languages.ToList(),
                Data = new Dictionary<string, string> { { "indexer", "idx" }, { "releaseGroup", "GRP" } }
            };
        }

        [Test]
        public void should_round_trip_a_history_row_with_series_and_episode_ids()
        {
            var history = NewHistory(_series, _episode1, EpisodeHistoryEventType.DownloadFolderImported, downloadId: "dl-1");

            Subject.Insert(history);

            var stored = Subject.Get(history.Id);

            stored.MediaKind.Should().Be(MediaKind.Tv);
            stored.ParentId.Should().Be(_series.Id);
            stored.ItemId.Should().Be(_episode1.Id);
            stored.EventType.Should().Be(EpisodeHistoryEventType.DownloadFolderImported);
            stored.DownloadId.Should().Be("dl-1");
            stored.Data.Should().Contain("indexer", "idx").And.Contain("releaseGroup", "GRP");
            stored.Languages.Should().Equal(Language.English);
            stored.Quality.Quality.Should().Be(Quality.HDTV720p);
        }

        [Test]
        public void should_refuse_a_row_of_another_media_kind()
        {
            var history = NewHistory(_series, _episode1);
            history.MediaKind = MediaKind.Movies;

            var action = () => Subject.Insert(history);

            action.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void should_not_see_movie_rows_that_share_a_parent_id_and_ids_stay_unique()
        {
            var movieRepository = Mocker.Resolve<MovieHistoryRepository>();

            var movieRow = new MovieHistory
            {
                MediaKind = MediaKind.Movies,
                ParentId = _series.Id,
                SourceTitle = "Movie.2024",
                Quality = new QualityModel(Quality.Bluray1080p),
                Date = DateTime.UtcNow,
                EventType = MovieHistoryEventType.Grabbed,
                DownloadId = "shared-download-id",
                Languages = new List<Language> { Language.English }
            };

            movieRepository.Insert(movieRow);

            var tvRow = NewHistory(_series, _episode1, downloadId: "shared-download-id");
            Subject.Insert(tvRow);

            tvRow.Id.Should().NotBe(movieRow.Id);
            Subject.All().Should().ContainSingle().Which.Id.Should().Be(tvRow.Id);
            Subject.Count().Should().Be(1);
            Subject.FindByDownloadId("shared-download-id").Should().ContainSingle().Which.Id.Should().Be(tvRow.Id);
            Subject.MostRecentForDownloadId("shared-download-id").Id.Should().Be(tvRow.Id);
            Subject.GetBySeries(_series.Id, null).Should().ContainSingle();
            Subject.Since(DateTime.UtcNow.AddDays(-1), null).Should().ContainSingle();
            Subject.GetPaged(new PagingSpec<EpisodeHistory> { Page = 1, PageSize = 10, SortKey = "date", SortDirection = SortDirection.Descending }, null, null)
                   .TotalRecords.Should().Be(1);
            Subject.FindDownloadHistory(_series.Id, new QualityModel(Quality.Bluray1080p)).Should().BeEmpty();

            movieRepository.All().Should().ContainSingle().Which.Id.Should().Be(movieRow.Id);
        }

        [Test]
        public void should_not_delete_movie_rows_when_deleting_for_a_series()
        {
            var movieRepository = Mocker.Resolve<MovieHistoryRepository>();

            movieRepository.Insert(new MovieHistory
            {
                MediaKind = MediaKind.Movies,
                ParentId = _series.Id,
                SourceTitle = "Movie.2024",
                Quality = new QualityModel(),
                Date = DateTime.UtcNow,
                Languages = new List<Language>()
            });
            Subject.Insert(NewHistory(_series, _episode1));
            Subject.Insert(NewHistory(_other, _otherEpisode));

            Subject.DeleteForSeries(new List<int> { _series.Id });

            Subject.All().Single().ParentId.Should().Be(_other.Id);
            movieRepository.All().Should().HaveCount(1);
        }

        [Test]
        public void should_find_history_by_episode_newest_first()
        {
            var old = NewHistory(_series, _episode1, date: DateTime.UtcNow.AddDays(-2));
            var recent = NewHistory(_series, _episode1, date: DateTime.UtcNow.AddDays(-1));
            Subject.Insert(old);
            Subject.Insert(recent);
            Subject.Insert(NewHistory(_series, _episode2));

            Subject.FindByEpisodeId(_episode1.Id).Select(h => h.Id).Should().Equal(recent.Id, old.Id);
            Subject.MostRecentForEpisode(_episode1.Id).Id.Should().Be(recent.Id);
            Subject.MostRecentForEpisode(99999).Should().BeNull();
        }

        [Test]
        public void should_join_the_series_and_episode_for_series_season_and_episode_queries()
        {
            Subject.Insert(NewHistory(_series, _episode1, EpisodeHistoryEventType.Grabbed));
            Subject.Insert(NewHistory(_series, _episode2, EpisodeHistoryEventType.DownloadFailed));
            Subject.Insert(NewHistory(_other, _otherEpisode));

            var bySeries = Subject.GetBySeries(_series.Id, null);
            bySeries.Should().HaveCount(2);
            bySeries.Should().OnlyContain(h => h.Series.Id == _series.Id && h.Episode.SeriesId == _series.Id);

            Subject.GetBySeries(_series.Id, EpisodeHistoryEventType.DownloadFailed).Single().Episode.Id.Should().Be(_episode2.Id);
            Subject.GetBySeason(_series.Id, 2, null).Single().ItemId.Should().Be(_episode2.Id);
            Subject.GetBySeason(_series.Id, 2, EpisodeHistoryEventType.Grabbed).Should().BeEmpty();
            Subject.GetByEpisode(_episode1.Id, null).Single().Series.Title.Should().Be("Breaking Bad");
            Subject.GetByEpisode(_episode1.Id, EpisodeHistoryEventType.DownloadFailed).Should().BeEmpty();
        }

        [Test]
        public void should_return_history_since_a_date_oldest_first()
        {
            var old = NewHistory(_series, _episode1, date: DateTime.UtcNow.AddDays(-10));
            var middle = NewHistory(_series, _episode1, EpisodeHistoryEventType.DownloadFailed, DateTime.UtcNow.AddDays(-2));
            var recent = NewHistory(_series, _episode2, date: DateTime.UtcNow.AddDays(-1));
            Subject.Insert(old);
            Subject.Insert(recent);
            Subject.Insert(middle);

            Subject.Since(DateTime.UtcNow.AddDays(-5), null).Select(h => h.Id).Should().Equal(middle.Id, recent.Id);
            Subject.Since(DateTime.UtcNow.AddDays(-5), EpisodeHistoryEventType.DownloadFailed).Select(h => h.Id).Should().Equal(middle.Id);
            Subject.Since(DateTime.UtcNow.AddDays(-5), null).First().Series.Should().NotBeNull();
        }

        [Test]
        public void should_find_download_history_for_a_series_and_quality()
        {
            var grabbed = NewHistory(_series, _episode1, EpisodeHistoryEventType.Grabbed, quality: Quality.HDTV720p);
            var failed = NewHistory(_series, _episode1, EpisodeHistoryEventType.DownloadFailed, quality: Quality.HDTV720p);
            var renamed = NewHistory(_series, _episode1, EpisodeHistoryEventType.EpisodeFileRenamed, quality: Quality.HDTV720p);
            var otherQuality = NewHistory(_series, _episode1, EpisodeHistoryEventType.Grabbed, quality: Quality.DVD);
            var otherSeries = NewHistory(_other, _otherEpisode, EpisodeHistoryEventType.Grabbed, quality: Quality.HDTV720p);
            Subject.Insert(grabbed);
            Subject.Insert(failed);
            Subject.Insert(renamed);
            Subject.Insert(otherQuality);
            Subject.Insert(otherSeries);

            Subject.FindDownloadHistory(_series.Id, new QualityModel(Quality.HDTV720p)).Select(h => h.Id)
                   .Should().BeEquivalentTo(new[] { grabbed.Id, failed.Id });
        }

        [Test]
        public void should_match_a_language_stored_in_the_legacy_single_value_form()
        {
            var legacy = NewHistory(_series, _episode1);
            var modern = NewHistory(_series, _episode2);
            Subject.Insert(legacy);
            Subject.Insert(modern);

            using (var conn = Mocker.Resolve<IMainDatabase>().OpenConnection())
            {
                conn.Execute($"UPDATE \"History\" SET \"Languages\" = '[{Language.French.Id}]' WHERE \"Id\" = @id", new { id = legacy.Id });
            }

            var page = Subject.GetPaged(
                new PagingSpec<EpisodeHistory> { Page = 1, PageSize = 10, SortKey = "date", SortDirection = SortDirection.Descending },
                new[] { Language.French.Id },
                null);

            page.Records.Select(h => h.Id).Should().Equal(legacy.Id);
        }

        [Test]
        public void should_filter_a_page_by_language_and_quality()
        {
            var english720 = NewHistory(_series, _episode1, quality: Quality.HDTV720p, languages: Language.English);
            var french720 = NewHistory(_series, _episode1, quality: Quality.HDTV720p, languages: Language.French);
            var englishDvd = NewHistory(_series, _episode2, quality: Quality.DVD, languages: new[] { Language.English, Language.French });
            Subject.Insert(english720);
            Subject.Insert(french720);
            Subject.Insert(englishDvd);

            PagingSpec<EpisodeHistory> Spec() => new PagingSpec<EpisodeHistory> { Page = 1, PageSize = 10, SortKey = "date", SortDirection = SortDirection.Descending };

            var all = Subject.GetPaged(Spec(), null, null);
            all.TotalRecords.Should().Be(3);
            all.Records.Should().OnlyContain(h => h.Series != null && h.Episode != null);

            Subject.GetPaged(Spec(), new[] { Language.French.Id }, null).Records.Select(h => h.Id)
                   .Should().BeEquivalentTo(new[] { french720.Id, englishDvd.Id });
            Subject.GetPaged(Spec(), null, new[] { Quality.DVD.Id }).Records.Select(h => h.Id).Should().Equal(englishDvd.Id);
            Subject.GetPaged(Spec(), new[] { Language.French.Id }, new[] { Quality.HDTV720p.Id }).Records.Select(h => h.Id).Should().Equal(french720.Id);
            Subject.GetPaged(Spec(), new[] { Language.French.Id }, new[] { Quality.HDTV720p.Id }).TotalRecords.Should().Be(1);
        }
    }
}
