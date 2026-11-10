using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Movies.History;

namespace NzbDrone.Core.Movies.Search
{
    public class MovieProactiveEligibility
    {
        public Movie Movie { get; set; }
        public string Reason { get; set; }
        public bool Eligible => Reason == "eligible";

        public static List<MovieProactiveEligibility> Evaluate(IEnumerable<Movie> movies,
                                                                IEnumerable<int> queuedMovieIds,
                                                                IEnumerable<MovieHistory> failures,
                                                                DateTime now,
                                                                IEnumerable<MovieHistory> grabs = null,
                                                                int recentGrabHours = 24)
        {
            var queued = queuedMovieIds.ToHashSet();
            var failed = failures.GroupBy(row => row.ParentId)
                .Where(group => MovieSearchSafety.TooManyRecentFailures(group, now))
                .Select(group => group.Key)
                .ToHashSet();
            var recentlyGrabbed = (grabs ?? Enumerable.Empty<MovieHistory>())
                .Where(row => row.Date >= now.AddHours(-recentGrabHours))
                .Select(row => row.ParentId).ToHashSet();

            return movies.Select(movie => new MovieProactiveEligibility
            {
                Movie = movie,
                Reason = !movie.Monitored ? "unmonitored" :
                    !movie.IsAvailable() ? "unavailable" :
                    queued.Contains(movie.Id) ? "in-queue" :
                    recentlyGrabbed.Contains(movie.Id) ? "grabbed-recently" :
                    failed.Contains(movie.Id) ? "retry-limit" :
                    movie.LastSearchTime.HasValue && movie.LastSearchTime >= now.AddDays(-1) ? "searched-recently" :
                    "eligible"
            }).ToList();
        }
    }
}
