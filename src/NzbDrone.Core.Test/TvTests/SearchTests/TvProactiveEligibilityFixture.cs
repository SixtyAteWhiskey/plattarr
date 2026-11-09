using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Search;

namespace NzbDrone.Core.Test.TvTests.SearchTests
{
    [TestFixture]
    public class TvProactiveEligibilityFixture
    {
        [Test]
        public void groups_aired_missing_episodes_by_series_and_explains_skips()
        {
            var now = DateTime.UtcNow;
            var series = new[]
            {
                new Series { Id = 1, Title = "One", Monitored = true },
                new Series { Id = 2, Title = "Two", Monitored = false },
                new Series { Id = 3, Title = "Three", Monitored = true }
            }.ToDictionary(item => item.Id);
            var episodes = new[]
            {
                new Episode { Id = 11, SeriesId = 1, Monitored = true },
                new Episode { Id = 12, SeriesId = 1, Monitored = true },
                new Episode { Id = 21, SeriesId = 2, Monitored = true },
                new Episode { Id = 31, SeriesId = 3, Monitored = true }
            };

            var result = TvProactiveSeriesEligibility.Evaluate(episodes, series, new[] { 11 }, new[] { 3 }, now);

            result.Should().HaveCount(3);
            result.Single(item => item.Series.Id == 1).Eligible.Should().BeTrue();
            result.Single(item => item.Series.Id == 1).Reasons.Should().Contain(new KeyValuePair<string, int>("in-queue", 1));
            result.Single(item => item.Series.Id == 2).Reasons["unmonitored"].Should().Be(1);
            result.Single(item => item.Series.Id == 3).Reasons["retry-limit"].Should().Be(1);
        }

        [Test]
        public void recently_searched_episode_does_not_hide_an_unsearched_episode_of_the_same_series()
        {
            var now = DateTime.UtcNow;
            var series = new Series { Id = 4, Monitored = true };
            var episodes = new[]
            {
                new Episode { Id = 41, SeriesId = 4, Monitored = true, LastSearchTime = now.AddHours(-2) },
                new Episode { Id = 42, SeriesId = 4, Monitored = true }
            };

            var result = TvProactiveSeriesEligibility.Evaluate(
                episodes,
                new Dictionary<int, Series> { { 4, series } },
                Array.Empty<int>(),
                Array.Empty<int>(),
                now).Single();

            result.EligibleEpisodeCount.Should().Be(1);
            result.Reasons["searched-recently"].Should().Be(1);
        }
    }
}
