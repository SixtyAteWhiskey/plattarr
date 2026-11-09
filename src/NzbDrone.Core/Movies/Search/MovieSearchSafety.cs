using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Movies.History;

namespace NzbDrone.Core.Movies.Search
{
    public static class MovieSearchSafety
    {
        public const int FailureLimit = 3;
        public static readonly TimeSpan FailureWindow = TimeSpan.FromDays(7);

        public static bool TooManyRecentFailures(IEnumerable<MovieHistory> history, DateTime now)
        {
            return RecentFailureCount(history, now) >= FailureLimit;
        }

        public static int RecentFailureCount(IEnumerable<MovieHistory> history, DateTime now)
        {
            return history.Where(row => row.EventType == MovieHistoryEventType.DownloadFailed &&
                                        row.Date >= now - FailureWindow)
                          .Select(row => row.DownloadId ?? row.Id.ToString())
                          .Distinct(StringComparer.OrdinalIgnoreCase)
                          .Take(FailureLimit)
                          .Count();
        }
    }
}
