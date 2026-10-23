using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Blocklisting;
using NzbDrone.Core.Tv.Parser.Model;
using MovieBlocklist = NzbDrone.Core.Movies.Blocklisting.Blocklist;
using MovieBlocklistRepository = NzbDrone.Core.Movies.Blocklisting.BlocklistRepository;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class BlocklistRepositoryFixture : DbTest<BlocklistRepository, Blocklist>
    {
        private Series _series;
        private Series _other;

        [SetUp]
        public void Setup()
        {
            var seriesRepository = Mocker.Resolve<SeriesRepository>();

            _series = TvTestData.NewSeries(81189, "Breaking Bad");
            _other = TvTestData.NewSeries(121361, "Game Of Thrones");
            seriesRepository.Insert(_series);
            seriesRepository.Insert(_other);
        }

        private static Blocklist NewBlocklist(Series series, string title = "Show.S01.720p-GRP", string hash = null)
        {
            return new Blocklist
            {
                MediaKind = MediaKind.Tv,
                ParentId = series.Id,
                ItemIds = new List<int> { 11, 12, 13 },
                SourceTitle = title,
                Quality = new QualityModel(Quality.HDTV720p),
                Date = DateTime.UtcNow,
                PublishedDate = DateTime.UtcNow.AddDays(-1),
                Size = 987654321,
                Protocol = DownloadProtocol.Torrent,
                Indexer = "idx",
                IndexerFlags = IndexerFlags.G_Freeleech,
                ReleaseType = ReleaseType.SeasonPack,
                Message = "failed",
                TorrentInfoHash = hash,
                Languages = new List<Language> { Language.English, Language.French }
            };
        }

        [Test]
        public void should_round_trip_item_ids_release_type_and_the_shared_columns()
        {
            var blocklist = NewBlocklist(_series, hash: "ABC123");

            Subject.Insert(blocklist);

            var stored = Subject.Get(blocklist.Id);

            stored.Should().BeEquivalentTo(
                blocklist,
                o => o.Excluding(b => b.Series)
                      .Using<DateTime?>(c => c.Subject.Should().BeCloseTo(c.Expectation.Value, TimeSpan.FromSeconds(1)))
                      .WhenTypeIs<DateTime?>()
                      .Using<DateTime>(c => c.Subject.Should().BeCloseTo(c.Expectation, TimeSpan.FromSeconds(1)))
                      .WhenTypeIs<DateTime>());
            stored.ItemIds.Should().Equal(11, 12, 13);
            stored.ReleaseType.Should().Be(ReleaseType.SeasonPack);
        }

        [Test]
        public void should_refuse_a_row_of_another_media_kind()
        {
            var blocklist = NewBlocklist(_series);
            blocklist.MediaKind = MediaKind.Movies;

            var action = () => Subject.Insert(blocklist);

            action.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void should_query_by_series_title_and_hash_case_insensitively()
        {
            Subject.Insert(NewBlocklist(_series, "Show.S01.720p-GRP", "ABC123"));
            Subject.Insert(NewBlocklist(_other, "Show.S01.720p-GRP", "ABC123"));

            Subject.BlocklistedByTitle(_series.Id, "SHOW.S01").Should().ContainSingle().Which.ParentId.Should().Be(_series.Id);
            Subject.BlocklistedByTitle(_series.Id, "other").Should().BeEmpty();
            Subject.BlocklistedByTorrentInfoHash(_series.Id, "abc123").Should().ContainSingle().Which.ParentId.Should().Be(_series.Id);
            Subject.BlocklistedBySeries(_other.Id).Should().ContainSingle();
        }

        [Test]
        public void should_not_see_or_delete_movie_rows_that_share_a_parent_id()
        {
            var movieRepository = Mocker.Resolve<MovieBlocklistRepository>();

            movieRepository.Insert(new MovieBlocklist
            {
                MediaKind = MediaKind.Movies,
                ParentId = _series.Id,
                SourceTitle = "Show.S01.720p-GRP",
                Quality = new QualityModel(),
                Date = DateTime.UtcNow,
                Languages = new List<Language>()
            });
            Subject.Insert(NewBlocklist(_series));

            Subject.All().Should().ContainSingle();
            Subject.Count().Should().Be(1);
            Subject.BlocklistedByTitle(_series.Id, "Show.S01").Should().ContainSingle();
            Subject.BlocklistedBySeries(_series.Id).Should().ContainSingle();

            Subject.DeleteForSeriesIds(new List<int> { _series.Id });

            Subject.All().Should().BeEmpty();
            movieRepository.All().Should().ContainSingle();
        }

        [Test]
        public void should_purge_tv_rows_and_leave_movie_rows()
        {
            var movieRepository = Mocker.Resolve<MovieBlocklistRepository>();

            movieRepository.Insert(new MovieBlocklist
            {
                MediaKind = MediaKind.Movies,
                ParentId = _series.Id,
                SourceTitle = "Show.S01.720p-GRP",
                Quality = new QualityModel(),
                Date = DateTime.UtcNow,
                Languages = new List<Language>()
            });
            Subject.Insert(NewBlocklist(_series));
            Subject.Insert(NewBlocklist(_other));

            Subject.Purge();

            Subject.All().Should().BeEmpty();
            movieRepository.All().Should().ContainSingle();
        }

        [Test]
        public void should_page_only_tv_rows_and_join_the_series()
        {
            var movieRepository = Mocker.Resolve<MovieBlocklistRepository>();

            movieRepository.Insert(new MovieBlocklist
            {
                MediaKind = MediaKind.Movies,
                ParentId = _series.Id,
                SourceTitle = "Movie",
                Quality = new QualityModel(),
                Date = DateTime.UtcNow,
                Languages = new List<Language>()
            });
            Subject.Insert(NewBlocklist(_series));
            Subject.Insert(NewBlocklist(_other));

            var page = Subject.GetPaged(new PagingSpec<Blocklist> { Page = 1, PageSize = 10, SortKey = "date", SortDirection = SortDirection.Descending });

            page.TotalRecords.Should().Be(2);
            page.Records.Select(b => b.Series.Title).Should().BeEquivalentTo(new[] { "Breaking Bad", "Game Of Thrones" });
        }
    }
}
