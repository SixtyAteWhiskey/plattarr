using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.MediaFiles;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Tv.SeriesStats;

namespace NzbDrone.Core.Test.TvTests.SeriesStatsTests
{
    [TestFixture]
    public class SeriesStatisticsFixture : DbTest<SeriesStatisticsRepository, Series>
    {
        private Series _series;
        private Series _other;

        [SetUp]
        public void Setup()
        {
            _series = TvTestData.NewSeries(81189, "Breaking Bad");
            _other = TvTestData.NewSeries(121361, "Game Of Thrones");

            Db.Insert(_series);
            Db.Insert(_other);
        }

        private EpisodeFile GivenFile(Series series, int season, string path, Quality quality, string group, long size)
        {
            var file = TvTestData.NewEpisodeFile(series.Id, season, path, quality);
            file.ReleaseGroup = group;
            file.Size = size;
            file.ReleaseType = ReleaseType.SingleEpisode;

            return Db.Insert(file);
        }

        private Episode GivenEpisode(Series series, int season, int number, DateTime? airDateUtc, bool monitored = true, int fileId = 0)
        {
            return Db.Insert(TvTestData.NewEpisode(series.Id, season, number, airDateUtc, monitored, fileId));
        }

        [Test]
        public void should_count_episodes_per_season()
        {
            var past = DateTime.UtcNow.AddDays(-10);
            var future = DateTime.UtcNow.AddDays(10);
            var file = GivenFile(_series, 1, "S01E01.mkv", Quality.Bluray1080p, "GRPA", 1000);

            GivenEpisode(_series, 1, 1, past, true, file.Id);
            GivenEpisode(_series, 1, 2, past);
            GivenEpisode(_series, 1, 3, past, false);
            GivenEpisode(_series, 1, 4, future);
            GivenEpisode(_series, 2, 1, past);

            var stats = Subject.SeriesStatistics(_series.Id);
            var season1 = stats.Single(s => s.SeasonNumber == 1);

            stats.Should().HaveCount(2);
            season1.TotalEpisodeCount.Should().Be(4);
            season1.EpisodeFileCount.Should().Be(1);
            season1.MonitoredEpisodeCount.Should().Be(3);

            // Available: aired or has a file. Counted: monitored and aired, or has a file.
            season1.AvailableEpisodeCount.Should().Be(3);
            season1.EpisodeCount.Should().Be(2);
        }

        [Test]
        public void should_sum_size_and_collect_release_groups_and_qualities_per_season()
        {
            var past = DateTime.UtcNow.AddDays(-10);
            var fileA = GivenFile(_series, 1, "S01E01.mkv", Quality.Bluray1080p, "GRPA", 1000);
            var fileB = GivenFile(_series, 1, "S01E02.mkv", Quality.WEBDL720p, "GRPB", 500);

            GivenEpisode(_series, 1, 1, past, true, fileA.Id);
            GivenEpisode(_series, 1, 2, past, true, fileB.Id);

            var season = Subject.SeriesStatistics(_series.Id).Single();

            season.SizeOnDisk.Should().Be(1500);
            season.ReleaseGroups.Should().BeEquivalentTo(new[] { "GRPA", "GRPB" });
            season.EpisodeFileQualities.Select(q => q.Id).Should().BeEquivalentTo(new[] { Quality.Bluray1080p.Id, Quality.WEBDL720p.Id });
            season.ReleaseTypes.Distinct().Should().Equal(ReleaseType.SingleEpisode);
        }

        [Test]
        public void should_only_return_the_requested_series()
        {
            var past = DateTime.UtcNow.AddDays(-10);
            var file = GivenFile(_other, 1, "S01E01.mkv", Quality.HDTV720p, "OTHER", 777);

            GivenEpisode(_series, 1, 1, past);
            GivenEpisode(_other, 1, 1, past, true, file.Id);

            var stats = Subject.SeriesStatistics(_series.Id);

            stats.Should().ContainSingle().Which.SizeOnDisk.Should().Be(0);
            Subject.SeriesStatistics().Select(s => s.SeriesId).Should().BeEquivalentTo(new[] { _series.Id, _other.Id });
        }

        [Test]
        public void should_return_next_and_previous_airing_for_monitored_episodes_only()
        {
            var previous = DateTime.UtcNow.AddDays(-3);
            var next = DateTime.UtcNow.AddDays(3);

            GivenEpisode(_series, 1, 1, previous);
            GivenEpisode(_series, 1, 2, next);

            // Unmonitored episodes closer to now than the monitored ones: they must be ignored.
            GivenEpisode(_series, 1, 3, DateTime.UtcNow.AddDays(1), false);
            GivenEpisode(_series, 1, 4, DateTime.UtcNow.AddDays(-1), false);

            var season = Subject.SeriesStatistics(_series.Id).Single();

            season.NextAiring.Value.ToUniversalTime().Should().BeCloseTo(next, TimeSpan.FromSeconds(1));
            season.PreviousAiring.Value.ToUniversalTime().Should().BeCloseTo(previous, TimeSpan.FromSeconds(1));
        }
    }
}
