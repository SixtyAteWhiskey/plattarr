using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Movies.Search;
using Radarr.Http;
using Radarr.Http.REST;

namespace Radarr.Api.V3.ProactiveSearch
{
    public class ProactiveMovieSearchConfigResource : RestResource
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

    public class ProactiveMovieSearchConfigUpdateResource
    {
        public bool Enabled { get; set; }
        public int? IntervalMinutes { get; set; }
        public int? ActiveDownloadLimit { get; set; }
        public int? RecentGrabHours { get; set; }
    }

    [V3ApiController("proactive-search/movie-config")]
    public class ProactiveMovieSearchConfigController : Controller
    {
        private readonly IConfigService _config;

        public ProactiveMovieSearchConfigController(IConfigService config)
        {
            _config = config;
        }

        [HttpGet]
        public Ok<ProactiveMovieSearchConfigResource> Get()
        {
            return TypedResults.Ok(ToResource());
        }

        [HttpPut("{id:int}")]
        public Ok<ProactiveMovieSearchConfigResource> Update(int id, [FromBody] ProactiveMovieSearchConfigUpdateResource resource)
        {
            if (id != 1 || resource == null)
            {
                throw new BadRequestException("A Movie proactive search configuration with id 1 is required.");
            }

            var interval = resource.IntervalMinutes ?? _config.ProactiveMovieSearchIntervalMinutes;
            var limit = resource.ActiveDownloadLimit ?? _config.ProactiveMovieActiveDownloadLimit;
            var window = resource.RecentGrabHours ?? _config.ProactiveMovieRecentGrabHours;

            if (!ProactiveSearchPacing.Valid(interval, limit, window))
            {
                throw new BadRequestException("Interval must be 5–1440 minutes, active download limit 1–100, and recent grab window 1–168 hours.");
            }

            _config.ProactiveMovieSearchEnabled = resource.Enabled;
            _config.ProactiveMovieSearchIntervalMinutes = interval;
            _config.ProactiveMovieActiveDownloadLimit = limit;
            _config.ProactiveMovieRecentGrabHours = window;
            return TypedResults.Ok(ToResource());
        }

        private ProactiveMovieSearchConfigResource ToResource()
        {
            return new ProactiveMovieSearchConfigResource
            {
                Id = 1,
                Enabled = _config.ProactiveMovieSearchEnabled,
                IntervalMinutes = _config.ProactiveMovieSearchIntervalMinutes,
                SearchesPerRun = 1,
                CooldownHours = 24,
                FailureLimit = MovieSearchSafety.FailureLimit,
                FailureWindowDays = (int)MovieSearchSafety.FailureWindow.TotalDays,
                ActiveDownloadLimit = _config.ProactiveMovieActiveDownloadLimit,
                RecentGrabHours = _config.ProactiveMovieRecentGrabHours
            };
        }
    }

    [V3ApiController("proactive-search/movie-activity")]
    public class ProactiveMovieSearchActivityController : Controller
    {
        private readonly IProactiveMovieSearchActivityRepository _activity;

        public ProactiveMovieSearchActivityController(IProactiveMovieSearchActivityRepository activity)
        {
            _activity = activity;
        }

        [HttpGet]
        public Ok<List<ProactiveMovieSearchActivity>> Get()
        {
            return TypedResults.Ok(_activity.Recent(20));
        }
    }
}
