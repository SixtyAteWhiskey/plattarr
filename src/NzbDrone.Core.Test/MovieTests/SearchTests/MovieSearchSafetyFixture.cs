using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Movies.History;
using NzbDrone.Core.Movies.Search;

namespace NzbDrone.Core.Test.MovieTests.SearchTests
{
    [TestFixture]
    public class MovieSearchSafetyFixture
    {
        [Test]
        public void three_distinct_recent_failures_hold_automatic_search()
        {
            var now = DateTime.UtcNow;
            var history = new List<MovieHistory>
            {
                new() { EventType = MovieHistoryEventType.DownloadFailed, DownloadId = "one", Date = now.AddDays(-1) },
                new() { EventType = MovieHistoryEventType.DownloadFailed, DownloadId = "one", Date = now.AddHours(-1) },
                new() { EventType = MovieHistoryEventType.DownloadFailed, DownloadId = "two", Date = now.AddDays(-2) },
                new() { EventType = MovieHistoryEventType.DownloadFailed, DownloadId = "old", Date = now.AddDays(-8) }
            };

            MovieSearchSafety.TooManyRecentFailures(history, now).Should().BeFalse();
            history.Add(new MovieHistory { EventType = MovieHistoryEventType.DownloadFailed, DownloadId = "three", Date = now });
            MovieSearchSafety.TooManyRecentFailures(history, now).Should().BeTrue();
        }
    }
}
