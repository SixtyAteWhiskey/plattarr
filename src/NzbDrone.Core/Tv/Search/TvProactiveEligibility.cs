using System;
using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Tv.Search
{
    public class TvProactiveEpisodeEligibility
    {
        public Episode Episode { get; set; }
        public string Reason { get; set; }
    }

    public class TvProactiveSeriesEligibility
    {
        public Series Series { get; set; }
        public List<TvProactiveEpisodeEligibility> Episodes { get; set; }
        public bool Eligible => Episodes.Any(item => item.Reason == "eligible");
        public int EligibleEpisodeCount => Episodes.Count(item => item.Reason == "eligible");
        public DateTime? LastSearchTime => Episodes.Max(item => item.Episode.LastSearchTime);
        public DateTime OldestEligibleSearchTime => Episodes.Where(item => item.Reason == "eligible")
            .Min(item => item.Episode.LastSearchTime ?? DateTime.MinValue);
        public Dictionary<string, int> Reasons => Episodes.GroupBy(item => item.Reason)
            .ToDictionary(group => group.Key, group => group.Count());

        public static List<TvProactiveSeriesEligibility> Evaluate(IEnumerable<Episode> episodes,
                                                                   IReadOnlyDictionary<int, Series> seriesById,
                                                                   IEnumerable<int> queuedEpisodeIds,
                                                                   IEnumerable<int> failedSeriesIds,
                                                                   DateTime now)
        {
            var queued = queuedEpisodeIds.ToHashSet();
            var failed = failedSeriesIds.ToHashSet();

            return episodes.GroupBy(episode => episode.SeriesId)
                .Where(group => seriesById.ContainsKey(group.Key))
                .Select(group =>
                {
                    var series = seriesById[group.Key];
                    return new TvProactiveSeriesEligibility
                    {
                        Series = series,
                        Episodes = group.Select(episode => new TvProactiveEpisodeEligibility
                        {
                            Episode = episode,
                            Reason = !series.Monitored || !episode.Monitored ? "unmonitored" :
                                failed.Contains(series.Id) ? "retry-limit" :
                                queued.Contains(episode.Id) ? "in-queue" :
                                episode.LastSearchTime.HasValue && episode.LastSearchTime >= now.AddDays(-1) ? "searched-recently" :
                                "eligible"
                        }).ToList()
                    };
                }).ToList();
        }
    }
}
