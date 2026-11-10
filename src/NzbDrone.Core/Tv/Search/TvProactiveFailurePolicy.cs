using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Tv.History;

namespace NzbDrone.Core.Tv.Search
{
    public static class TvProactiveFailurePolicy
    {
        public static HashSet<int> LimitedSeries(IEnumerable<EpisodeHistory> failures, DateTime now)
        {
            return failures.Where(row => row.EventType == EpisodeHistoryEventType.DownloadFailed &&
                                         row.Date >= now - DownloadRetryPolicy.FailureWindow)
                .GroupBy(row => row.ParentId)
                .Where(group => group.Select(row => row.DownloadId ?? row.Id.ToString())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(DownloadRetryPolicy.FailureLimit)
                    .Count() >= DownloadRetryPolicy.FailureLimit)
                .Select(group => group.Key).ToHashSet();
        }
    }
}
