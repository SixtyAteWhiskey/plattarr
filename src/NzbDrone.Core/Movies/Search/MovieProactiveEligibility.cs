using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Movies.History;

namespace NzbDrone.Core.Movies.Search
{
    public class MovieProactiveEligibility
    {
        public Movie Movie { get; set; }
        public string CandidateType { get; set; }
        public string Reason { get; set; }
        public bool Eligible => Reason == "eligible";

        public static List<MovieProactiveEligibility> Evaluate(IEnumerable<Movie> movies,
                                                                IEnumerable<int> queuedMovieIds,
                                                                IEnumerable<MovieHistory> failures,
                                                                DateTime now,
                                                                IEnumerable<MovieHistory> grabs = null,
                                                                int recentGrabHours = 24)
        {
            return Evaluate(movies, Enumerable.Empty<Movie>(), queuedMovieIds, failures, now, grabs, recentGrabHours);
        }

        public static List<MovieProactiveEligibility> Evaluate(IEnumerable<Movie> missingMovies,
                                                                IEnumerable<Movie> cutoffUnmetMovies,
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

            // A Movie with no file is a missing candidate even if a caller also
            // returns it from the cutoff query. Search each Movie at most once.
            var candidates = missingMovies.Select(movie => (Movie: movie, Type: "missing"))
                .Concat(cutoffUnmetMovies.Select(movie => (Movie: movie, Type: "cutoff-unmet")))
                .GroupBy(item => item.Movie.Id)
                .Select(group => group.First());

            return candidates.Select(candidate => new MovieProactiveEligibility
            {
                Movie = candidate.Movie,
                CandidateType = candidate.Type,
                Reason = !candidate.Movie.Monitored ? "unmonitored" :
                    !candidate.Movie.IsAvailable() ? "unavailable" :
                    queued.Contains(candidate.Movie.Id) ? "in-queue" :
                    recentlyGrabbed.Contains(candidate.Movie.Id) ? "grabbed-recently" :
                    failed.Contains(candidate.Movie.Id) ? "retry-limit" :
                    candidate.Movie.LastSearchTime.HasValue && candidate.Movie.LastSearchTime >= now.AddDays(-1) ? "searched-recently" :
                    "eligible"
            }).ToList();
        }
    }
}
