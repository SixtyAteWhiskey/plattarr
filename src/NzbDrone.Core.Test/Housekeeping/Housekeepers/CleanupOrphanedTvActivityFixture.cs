using System.Collections.Generic;
using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Housekeepers;
using TvBlocklist = NzbDrone.Core.Tv.Blocklisting.Blocklist;
using TvHistory = NzbDrone.Core.Tv.History.EpisodeHistory;
using TvHistoryEventType = NzbDrone.Core.Tv.History.EpisodeHistoryEventType;

namespace NzbDrone.Core.Test.Housekeeping.Housekeepers
{
    [TestFixture]
    public class CleanupOrphanedTvActivityFixture : DbTest<CleanupOrphanedTvActivity, TvHistory>
    {
        private Series _series;
        private Episode _episode;

        [SetUp]
        public void Setup()
        {
            _series = Db.Insert(new Series { TvdbId = 81189, Title = "A", CleanTitle = "a", SortTitle = "a", TitleSlug = "a", Path = "/tv/a", Images = new List<MediaCover.MediaCover>() });
            _episode = Db.Insert(new Episode { SeriesId = _series.Id, TvdbId = 1, SeasonNumber = 1, EpisodeNumber = 1, Title = "Pilot" });
        }

        private TvHistory NewHistory(MediaKind kind, int parentId, int itemId)
        {
            return new TvHistory
            {
                MediaKind = kind,
                EventType = TvHistoryEventType.Grabbed,
                ParentId = parentId,
                ItemId = itemId,
                SourceTitle = "A.S01E01",
                Quality = new QualityModel(),
                Languages = new List<Language>(),
                Date = System.DateTime.UtcNow
            };
        }

        private int Count(string table, MediaKind kind)
        {
            using var mapper = Mocker.Resolve<IMainDatabase>().OpenConnection();

            return mapper.ExecuteScalar<int>($@"SELECT COUNT(*) FROM ""{table}"" WHERE ""MediaKind"" = {(int)kind}");
        }

        [Test]
        public void should_delete_history_of_a_series_that_is_gone()
        {
            Db.Insert(NewHistory(MediaKind.Tv, _series.Id + 100, _episode.Id));

            Subject.Clean();

            Count("History", MediaKind.Tv).Should().Be(0);
        }

        [Test]
        public void should_delete_history_of_an_episode_that_is_gone()
        {
            Db.Insert(NewHistory(MediaKind.Tv, _series.Id, _episode.Id + 100));

            Subject.Clean();

            Count("History", MediaKind.Tv).Should().Be(0);
        }

        [Test]
        public void should_keep_history_whose_series_and_episode_exist()
        {
            Db.Insert(NewHistory(MediaKind.Tv, _series.Id, _episode.Id));

            Subject.Clean();

            Count("History", MediaKind.Tv).Should().Be(1);
        }

        [Test]
        public void should_not_touch_rows_of_other_media_types()
        {
            Db.Insert(NewHistory(MediaKind.Movies, 4242, 0));

            Subject.Clean();

            Count("History", MediaKind.Movies).Should().Be(1);
        }

        [Test]
        public void should_delete_blocklist_of_a_series_that_is_gone_and_keep_the_rest()
        {
            var gone = new TvBlocklist
            {
                MediaKind = MediaKind.Tv,
                ParentId = _series.Id + 100,
                ItemIds = new List<int> { 1 },
                SourceTitle = "gone",
                Quality = new QualityModel(),
                Languages = new List<Language>()
            };
            var kept = new TvBlocklist
            {
                MediaKind = MediaKind.Tv,
                ParentId = _series.Id,
                ItemIds = new List<int> { _episode.Id },
                SourceTitle = "kept",
                Quality = new QualityModel(),
                Languages = new List<Language>()
            };

            Db.Insert(gone);
            Db.Insert(kept);

            Subject.Clean();

            Count("Blocklist", MediaKind.Tv).Should().Be(1);
        }
    }
}
