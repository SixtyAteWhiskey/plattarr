using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Music.History;

namespace NzbDrone.Core.Music.Search
{
    public class MusicProactiveEligibility
    {
        public Album Album { get; set; }
        public string Reason { get; set; }
        public bool Eligible => Reason == "eligible";

        public static List<MusicProactiveEligibility> Evaluate(IEnumerable<Album> albums,
                                                                IEnumerable<int> queuedAlbumIds,
                                                                IEnumerable<AlbumHistory> failures,
                                                                DateTime now,
                                                                IEnumerable<AlbumHistory> grabs = null,
                                                                int recentGrabHours = 24)
        {
            var queued = queuedAlbumIds.ToHashSet();
            var limited = failures.Where(row => row.EventType == AlbumHistoryEventType.DownloadFailed &&
                                               row.Date >= now - DownloadRetryPolicy.FailureWindow)
                .GroupBy(row => row.ItemId)
                .Where(group => group.Select(row => row.DownloadId ?? row.Id.ToString())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(DownloadRetryPolicy.FailureLimit).Count() >= DownloadRetryPolicy.FailureLimit)
                .Select(group => group.Key).ToHashSet();
            var recentlyGrabbed = (grabs ?? Enumerable.Empty<AlbumHistory>())
                .Where(row => row.Date >= now.AddHours(-recentGrabHours))
                .Select(row => row.ItemId).ToHashSet();

            return albums.Select(album => new MusicProactiveEligibility
            {
                Album = album,
                Reason = !album.Monitored || album.Artist?.Value?.Monitored != true ? "unmonitored" :
                    !album.ReleaseDate.HasValue || album.ReleaseDate > now ? "unavailable" :
                    queued.Contains(album.Id) ? "in-queue" :
                    recentlyGrabbed.Contains(album.Id) ? "grabbed-recently" :
                    limited.Contains(album.Id) ? "retry-limit" :
                    album.LastSearchTime.HasValue && album.LastSearchTime >= now.AddDays(-1) ? "searched-recently" :
                    "eligible"
            }).ToList();
        }
    }
}
