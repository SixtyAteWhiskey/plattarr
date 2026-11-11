using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.History;
using NzbDrone.Core.Music.Search;
using NzbDrone.Core.Queue;
using Radarr.Http;
using Radarr.Http.REST;

namespace Radarr.Api.V3.ProactiveSearch
{
    public class ProactiveMusicItemResource
    {
        public int AlbumId { get; set; }
        public string Title { get; set; }
        public DateTime? LastSearchTime { get; set; }
        public string Reason { get; set; }

        public ProactiveMusicItemResource(MusicProactiveEligibility item)
        {
            AlbumId = item.Album.Id;
            Title = item.Album.Title ?? string.Empty;
            LastSearchTime = item.Album.LastSearchTime;
            Reason = item.Reason;
        }
    }

    public class ProactiveMusicPreviewResource
    {
        public DateTime GeneratedAt { get; set; }
        public int MissingAlbums { get; set; }
        public Dictionary<string, int> Reasons { get; set; }
        public List<ProactiveMusicItemResource> NextAlbums { get; set; }
        public List<ProactiveMusicItemResource> RecentAlbums { get; set; }
        public List<ProactiveMusicItemResource> SkippedAlbums { get; set; }
        public int SkippedAlbumsTotal { get; set; }
        public int ActiveDownloads { get; set; }
        public string PauseReason { get; set; }
    }

    [V3ApiController("proactive-search/music-preview")]
    public class ProactiveMusicPreviewController : Controller
    {
        private readonly IAlbumService _albums;
        private readonly IQueueService _queue;
        private readonly IHistoryService _history;
        private readonly IConfigService _config;

        public ProactiveMusicPreviewController(IAlbumService albums, IQueueService queue, IHistoryService history, IConfigService config)
        {
            _albums = albums;
            _queue = queue;
            _history = history;
            _config = config;
        }

        [HttpGet]
        public Ok<ProactiveMusicPreviewResource> Get([FromQuery] int offset = 0, [FromQuery] int limit = 50, [FromQuery] string title = null)
        {
            var now = DateTime.UtcNow;
            var paging = new PagingSpec<Album>
            {
                Page = 1,
                PageSize = 100000,
                SortDirection = SortDirection.Ascending,
                SortKey = "Id"
            };
            var albums = _albums.AlbumsWithoutFiles(paging).Records;
            var queueItems = _queue.GetQueue();
            var queued = queueItems.Select(item => item.GetRemoteAlbum())
                .Where(remote => remote?.Albums.Any() == true)
                .SelectMany(remote => remote.Albums.Select(album => album.Id));
            var failures = _history.Since(now - DownloadRetryPolicy.FailureWindow, AlbumHistoryEventType.DownloadFailed);
            var grabs = _history.Since(now.AddHours(-_config.ProactiveMusicRecentGrabHours), AlbumHistoryEventType.Grabbed);
            var assessments = MusicProactiveEligibility.Evaluate(albums, queued, failures, now, grabs, _config.ProactiveMusicRecentGrabHours);
            var activeDownloads = ProactiveSearchPacing.ActiveDownloadCount(queueItems);
            var skipped = assessments.Where(item => !item.Eligible &&
                (string.IsNullOrWhiteSpace(title) || item.Album.Title?.Contains(title, StringComparison.OrdinalIgnoreCase) == true))
                .OrderBy(item => item.Album.Title).ThenBy(item => item.Album.Id).ToList();

            return TypedResults.Ok(new ProactiveMusicPreviewResource
            {
                GeneratedAt = now,
                ActiveDownloads = activeDownloads,
                PauseReason = ProactiveSearchPacing.PauseReason(_config.ProactiveMusicSearchEnabled, activeDownloads, _config.ProactiveMusicActiveDownloadLimit),
                MissingAlbums = assessments.Count,
                Reasons = assessments.GroupBy(item => item.Reason).ToDictionary(group => group.Key, group => group.Count()),
                NextAlbums = assessments.Where(item => item.Eligible)
                    .OrderBy(item => item.Album.LastSearchTime ?? DateTime.MinValue).ThenBy(item => item.Album.Id)
                    .Take(10).Select(item => new ProactiveMusicItemResource(item)).ToList(),
                RecentAlbums = assessments.Where(item => item.Album.LastSearchTime.HasValue)
                    .OrderByDescending(item => item.Album.LastSearchTime).Take(10)
                    .Select(item => new ProactiveMusicItemResource(item)).ToList(),
                SkippedAlbumsTotal = skipped.Count,
                SkippedAlbums = skipped.Skip(Math.Max(0, offset)).Take(Math.Clamp(limit, 1, 100))
                    .Select(item => new ProactiveMusicItemResource(item)).ToList()
            });
        }
    }

    public class ProactiveMusicSearchConfigResource : RestResource
    {
        public bool Enabled { get; set; }
        public int IntervalMinutes { get; set; }
        public int SearchesPerRun { get; set; }
        public int CooldownHours { get; set; }
        public int FailureLimit { get; set; }
        public int FailureWindowDays { get; set; }
        public int ActiveDownloadLimit { get; set; }
        public int RecentGrabHours { get; set; }
    }

    public class ProactiveMusicSearchConfigUpdateResource
    {
        public bool Enabled { get; set; }
        public int? IntervalMinutes { get; set; }
        public int? ActiveDownloadLimit { get; set; }
        public int? RecentGrabHours { get; set; }
    }

    [V3ApiController("proactive-search/music-config")]
    public class ProactiveMusicSearchConfigController : Controller
    {
        private readonly IConfigService _config;

        public ProactiveMusicSearchConfigController(IConfigService config)
        {
            _config = config;
        }

        [HttpGet]
        public Ok<ProactiveMusicSearchConfigResource> Get()
        {
            return TypedResults.Ok(ToResource());
        }

        [HttpPut("{id:int}")]
        public Ok<ProactiveMusicSearchConfigResource> Update(int id, [FromBody] ProactiveMusicSearchConfigUpdateResource resource)
        {
            if (id != 1 || resource == null)
            {
                throw new BadRequestException("A Music proactive search configuration with id 1 is required.");
            }

            var interval = resource.IntervalMinutes ?? _config.ProactiveMusicSearchIntervalMinutes;
            var limit = resource.ActiveDownloadLimit ?? _config.ProactiveMusicActiveDownloadLimit;
            var window = resource.RecentGrabHours ?? _config.ProactiveMusicRecentGrabHours;
            if (!ProactiveSearchPacing.Valid(interval, limit, window))
            {
                throw new BadRequestException("Interval must be 5–1440 minutes, active download limit 1–100, and recent grab window 1–168 hours.");
            }

            _config.ProactiveMusicSearchEnabled = resource.Enabled;
            _config.ProactiveMusicSearchIntervalMinutes = interval;
            _config.ProactiveMusicActiveDownloadLimit = limit;
            _config.ProactiveMusicRecentGrabHours = window;
            return TypedResults.Ok(ToResource());
        }

        private ProactiveMusicSearchConfigResource ToResource()
        {
            return new ProactiveMusicSearchConfigResource
            {
                Id = 1,
                Enabled = _config.ProactiveMusicSearchEnabled,
                IntervalMinutes = _config.ProactiveMusicSearchIntervalMinutes,
                SearchesPerRun = 1,
                CooldownHours = 24,
                FailureLimit = DownloadRetryPolicy.FailureLimit,
                FailureWindowDays = (int)DownloadRetryPolicy.FailureWindow.TotalDays,
                ActiveDownloadLimit = _config.ProactiveMusicActiveDownloadLimit,
                RecentGrabHours = _config.ProactiveMusicRecentGrabHours
            };
        }
    }

    [V3ApiController("proactive-search/music-activity")]
    public class ProactiveMusicSearchActivityController : Controller
    {
        private readonly IProactiveMusicSearchActivityRepository _activity;

        public ProactiveMusicSearchActivityController(IProactiveMusicSearchActivityRepository activity)
        {
            _activity = activity;
        }

        [HttpGet]
        public Ok<List<ProactiveMusicSearchActivity>> Get()
        {
            return TypedResults.Ok(_activity.Recent(20));
        }
    }
}
