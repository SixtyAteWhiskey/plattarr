using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.History;
using NzbDrone.Core.Movies.Search;

namespace NzbDrone.Core.Test.MovieTests.SearchTests
{
    [TestFixture]
    public class MovieProactiveEligibilityFixture
    {
        [Test]
        public void preview_reasons_match_scheduled_search_safety_checks()
        {
            var now = DateTime.UtcNow;
            var movies = Enumerable.Range(1, 5).Select(id => new Movie
            {
                Id = id,
                Monitored = id != 1,
                MinimumAvailability = MovieStatusType.Announced,
                LastSearchTime = id == 4 ? now.AddHours(-2) : null
            }).ToList();
            var failures = Enumerable.Range(1, 3).Select(id => new MovieHistory
            {
                ParentId = 3,
                EventType = MovieHistoryEventType.DownloadFailed,
                DownloadId = id.ToString(),
                Date = now.AddHours(-id)
            }).ToList();

            var results = MovieProactiveEligibility.Evaluate(movies, new[] { 2 }, failures, now);

            results.Select(item => item.Reason).Should().Equal(
                "unmonitored", "in-queue", "retry-limit", "searched-recently", "eligible");
            results.Where(item => item.Eligible).Select(item => item.Movie.Id).Should().Equal(5);
        }

        [Test]
        public void failed_downloads_are_counted_by_distinct_download_id()
        {
            var now = DateTime.UtcNow;
            var movie = new Movie { Id = 7, Monitored = true, MinimumAvailability = MovieStatusType.Announced };
            var failures = new List<MovieHistory>
            {
                new() { ParentId = 7, EventType = MovieHistoryEventType.DownloadFailed, DownloadId = "same", Date = now },
                new() { ParentId = 7, EventType = MovieHistoryEventType.DownloadFailed, DownloadId = "same", Date = now }
            };

            MovieProactiveEligibility.Evaluate(new[] { movie }, Array.Empty<int>(), failures, now)
                .Single().Reason.Should().Be("eligible");
        }

        [Test]
        public void recent_grab_excludes_only_its_movie_until_the_window_expires()
        {
            var now = DateTime.UtcNow;
            var movies = new[] { 1, 2 }.Select(id => new Movie
            {
                Id = id,
                Monitored = true,
                MinimumAvailability = MovieStatusType.Announced
            });
            var grabs = new[]
            {
                new MovieHistory { ParentId = 1, Date = now.AddHours(-2) },
                new MovieHistory { ParentId = 2, Date = now.AddHours(-25) }
            };

            var result = MovieProactiveEligibility.Evaluate(
                movies,
                Array.Empty<int>(),
                Array.Empty<MovieHistory>(),
                now,
                grabs,
                24);

            result.Select(item => item.Reason).Should().Equal("grabbed-recently", "eligible");
        }

        [Test]
        public void cutoff_unmet_movies_share_gates_and_duplicate_missing_movies_are_searched_once()
        {
            var now = DateTime.UtcNow;
            var missing = new Movie { Id = 1, Monitored = true, MinimumAvailability = MovieStatusType.Announced };
            var upgrade = new Movie { Id = 2, Monitored = true, MinimumAvailability = MovieStatusType.Announced };
            var assessments = MovieProactiveEligibility.Evaluate(
                new[] { missing },
                new[] { missing, upgrade },
                Array.Empty<int>(),
                Array.Empty<MovieHistory>(),
                now,
                new[] { new MovieHistory { ParentId = 2, Date = now } });

            assessments.Should().HaveCount(2);
            assessments[0].CandidateType.Should().Be("missing");
            assessments[1].CandidateType.Should().Be("cutoff-unmet");
            assessments[1].Reason.Should().Be("grabbed-recently");
        }

        [Test]
        public void cutoff_unmet_movies_report_queue_failure_and_cooldown_reasons()
        {
            var now = DateTime.UtcNow;
            var upgrades = Enumerable.Range(1, 4).Select(id => new Movie
            {
                Id = id,
                Monitored = true,
                MinimumAvailability = MovieStatusType.Announced,
                LastSearchTime = id == 3 ? now.AddHours(-1) : null
            }).ToList();
            var failures = Enumerable.Range(1, 3).Select(id => new MovieHistory
            {
                ParentId = 2,
                EventType = MovieHistoryEventType.DownloadFailed,
                DownloadId = id.ToString(),
                Date = now.AddHours(-id)
            });

            var assessments = MovieProactiveEligibility.Evaluate(
                Array.Empty<Movie>(), upgrades, new[] { 1 }, failures, now);

            assessments.Select(item => item.Reason).Should().Equal(
                "in-queue", "retry-limit", "searched-recently", "eligible");
            assessments.Should().OnlyContain(item => item.CandidateType == "cutoff-unmet");
        }
    }
}
