using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Parser;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Tv.Pending;

namespace NzbDrone.Core.Test.TvTests.PendingTests
{
    // The TV pending release service keeps its releases in a static cache, so every fixture loads
    // the cache from the mocked repository through ApplicationStartedEvent.
    public abstract class PendingReleaseServiceFixtureBase : CoreTest<PendingReleaseService>
    {
        protected Series _series;
        protected Episode _episode;
        protected QualityProfile _profile;
        protected ReleaseInfo _release;
        protected ParsedEpisodeInfo _parsedEpisodeInfo;
        protected RemoteEpisode _remoteEpisode;
        protected DownloadDecision _temporarilyRejected;
        protected List<PendingRelease> _heldReleases;

        private int _nextId = 1;

        [SetUp]
        public void SetUpBase()
        {
            _nextId = 1;

            _series = Builder<Series>.CreateNew()
                                     .With(s => s.Id = 10)
                                     .With(s => s.Tags = new HashSet<int>())
                                     .Build();

            _episode = new Episode { Id = 100, SeriesId = _series.Id, SeasonNumber = 1, EpisodeNumber = 1 };

            _profile = new QualityProfile
            {
                Name = "Test",
                Cutoff = Quality.HDTV720p.Id,
                Items = new List<QualityProfileQualityItem>
                {
                    new QualityProfileQualityItem { Allowed = true, Quality = Quality.SDTV },
                    new QualityProfileQualityItem { Allowed = true, Quality = Quality.HDTV720p },
                    new QualityProfileQualityItem { Allowed = true, Quality = Quality.WEBDL720p },
                    new QualityProfileQualityItem { Allowed = true, Quality = Quality.Bluray720p }
                },
            };

            _series.QualityProfile = _profile;

            _release = Builder<ReleaseInfo>.CreateNew().Build();
            _release.PublishDate = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

            _parsedEpisodeInfo = new ParsedEpisodeInfo
            {
                SeriesTitle = "Series",
                SeasonNumbers = new List<int> { 1 },
                EpisodeNumbers = new[] { 1 },
                Quality = new QualityModel(Quality.HDTV720p)
            };

            _remoteEpisode = new RemoteEpisode
            {
                Series = _series,
                Episodes = new List<Episode> { _episode },
                ParsedEpisodeInfo = _parsedEpisodeInfo,
                Release = _release
            };

            _temporarilyRejected = new DownloadDecision(_remoteEpisode, new DownloadRejection(DownloadRejectionReason.MinimumAgeDelay, "Temp Rejected", RejectionType.Temporary));

            _heldReleases = new List<PendingRelease>();

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Setup(s => s.All())
                  .Returns(_heldReleases);

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Setup(s => s.AllBySeriesId(It.IsAny<int>()))
                  .Returns<int>(i => _heldReleases.Where(v => v.ParentId == i).ToList());

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetSeries(It.IsAny<IEnumerable<int>>()))
                  .Returns<IEnumerable<int>>(ids => ids.Contains(_series.Id) ? new List<Series> { _series } : new List<Series>());

            // Every held release maps to the episode the decision is for, unless a test says otherwise.
            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<Series>()))
                  .Returns(() => new RemoteEpisode { Series = _series, Episodes = new List<Episode> { _episode }, MappedSeasonNumber = 1 });

            Mocker.GetMock<IDelayProfileService>()
                  .Setup(s => s.AllForTags(It.IsAny<HashSet<int>>()))
                  .Returns(new List<DelayProfile> { new DelayProfile() });

            Mocker.GetMock<IDelayProfileService>()
                  .Setup(s => s.BestForTags(It.IsAny<HashSet<int>>()))
                  .Returns(new DelayProfile());

            // The cache is static, empty it so no test sees another test's releases.
            LoadCache();
        }

        protected PendingRelease GivenHeldRelease(string title, string indexer, DateTime publishDate, PendingReleaseReason reason = PendingReleaseReason.Delay, QualityModel quality = null, int[] episodeIds = null)
        {
            var release = Builder<ReleaseInfo>.CreateNew().Build();
            release.Title = title;
            release.Indexer = indexer;
            release.PublishDate = publishDate;

            var info = new ParsedEpisodeInfo
            {
                SeriesTitle = _parsedEpisodeInfo.SeriesTitle,
                SeasonNumbers = new List<int> { 1 },
                EpisodeNumbers = new[] { 1 },
                Quality = quality ?? _parsedEpisodeInfo.Quality
            };

            var held = new PendingRelease
            {
                Id = _nextId++,
                MediaKind = MediaKind.Tv,
                ParentId = _series.Id,
                Title = title,
                Added = DateTime.UtcNow,
                Release = release,
                Reason = reason,
                ParsedInfo = info
            };

            if (episodeIds != null)
            {
                var episodes = episodeIds.Select(id => new Episode { Id = id, SeriesId = _series.Id, SeasonNumber = 1, EpisodeNumber = id }).ToList();

                Mocker.GetMock<IParsingService>()
                      .Setup(s => s.Map(It.Is<ParsedEpisodeInfo>(p => p == info), It.IsAny<Series>()))
                      .Returns(new RemoteEpisode { Series = _series, Episodes = episodes, MappedSeasonNumber = 1 });
            }

            _heldReleases.Add(held);

            LoadCache();

            return held;
        }

        protected void LoadCache()
        {
            Subject.Handle(new ApplicationStartedEvent());
        }
    }
}
