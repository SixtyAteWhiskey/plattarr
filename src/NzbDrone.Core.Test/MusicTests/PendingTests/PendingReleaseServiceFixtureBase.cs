using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Parser;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Pending;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.PendingTests
{
    // The Music pending release service keeps its releases in a static cache, so every fixture loads
    // the cache from the mocked repository through ApplicationStartedEvent.
    public abstract class PendingReleaseServiceFixtureBase : CoreTest<PendingReleaseService>
    {
        protected Artist _artist;
        protected Album _album;
        protected AudioQualityProfile _profile;
        protected ReleaseInfo _release;
        protected ParsedAlbumInfo _parsedAlbumInfo;
        protected RemoteAlbum _remoteAlbum;
        protected DownloadDecision _temporarilyRejected;
        protected List<PendingRelease> _heldReleases;

        private int _nextId = 1;

        [SetUp]
        public void SetUpBase()
        {
            _nextId = 1;

            _artist = Builder<Artist>.CreateNew()
                                     .With(a => a.Id = 10)
                                     .With(a => a.Tags = new HashSet<int>())
                                     .Build();

            _album = new Album { Id = 100, ArtistId = _artist.Id, Title = "Album" };

            _profile = new AudioQualityProfile
            {
                Name = "Test",
                Cutoff = AudioQuality.MP3_320.Id,
                Items = new List<AudioQualityProfileItem>
                {
                    new AudioQualityProfileItem { Allowed = true, Quality = AudioQuality.MP3_192 },
                    new AudioQualityProfileItem { Allowed = true, Quality = AudioQuality.MP3_256 },
                    new AudioQualityProfileItem { Allowed = true, Quality = AudioQuality.MP3_320 },
                    new AudioQualityProfileItem { Allowed = true, Quality = AudioQuality.FLAC }
                },
            };

            _artist.QualityProfile = new LazyLoaded<AudioQualityProfile>(_profile);

            _release = Builder<ReleaseInfo>.CreateNew().Build();
            _release.PublishDate = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

            _parsedAlbumInfo = new ParsedAlbumInfo
            {
                ArtistName = "Artist",
                AlbumTitle = "Album",
                Quality = new AudioQualityModel(AudioQuality.MP3_320)
            };

            _remoteAlbum = new RemoteAlbum
            {
                Artist = _artist,
                Albums = new List<Album> { _album },
                ParsedAlbumInfo = _parsedAlbumInfo,
                Release = _release
            };

            _temporarilyRejected = new DownloadDecision(_remoteAlbum, new DownloadRejection(DownloadRejectionReason.MinimumAgeDelay, "Temp Rejected", RejectionType.Temporary));

            _heldReleases = new List<PendingRelease>();

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Setup(s => s.All())
                  .Returns(_heldReleases);

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Setup(s => s.AllByArtistId(It.IsAny<int>()))
                  .Returns<int>(i => _heldReleases.Where(v => v.ParentId == i).ToList());

            Mocker.GetMock<IArtistService>()
                  .Setup(s => s.GetArtists(It.IsAny<IEnumerable<int>>()))
                  .Returns<IEnumerable<int>>(ids => ids.Contains(_artist.Id) ? new List<Artist> { _artist } : new List<Artist>());

            // Every held release maps to the album the decision is for, unless a test says otherwise.
            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetAlbums(It.IsAny<ParsedAlbumInfo>(), It.IsAny<Artist>(), null))
                  .Returns(() => new List<Album> { _album });

            Mocker.GetMock<IDelayProfileService>()
                  .Setup(s => s.AllForTags(It.IsAny<HashSet<int>>()))
                  .Returns(new List<DelayProfile> { new DelayProfile() });

            Mocker.GetMock<IDelayProfileService>()
                  .Setup(s => s.BestForTags(It.IsAny<HashSet<int>>()))
                  .Returns(new DelayProfile());

            // The cache is static, empty it so no test sees another test's releases.
            LoadCache();
        }

        protected PendingRelease GivenHeldRelease(string title, string indexer, DateTime publishDate, PendingReleaseReason reason = PendingReleaseReason.Delay, AudioQualityModel quality = null, int[] albumIds = null, string albumTitle = null)
        {
            var release = Builder<ReleaseInfo>.CreateNew().Build();
            release.Title = title;
            release.Indexer = indexer;
            release.PublishDate = publishDate;

            var info = new ParsedAlbumInfo
            {
                ArtistName = _parsedAlbumInfo.ArtistName,
                AlbumTitle = albumTitle ?? _parsedAlbumInfo.AlbumTitle,
                Quality = quality ?? _parsedAlbumInfo.Quality
            };

            var held = new PendingRelease
            {
                Id = _nextId++,
                MediaKind = MediaKind.Music,
                ParentId = _artist.Id,
                Title = title,
                Added = DateTime.UtcNow,
                Release = release,
                Reason = reason,
                ParsedInfo = info
            };

            if (albumIds != null)
            {
                var albums = albumIds.Select(id => new Album { Id = id, ArtistId = _artist.Id, Title = $"Album {id}" }).ToList();

                Mocker.GetMock<IParsingService>()
                      .Setup(s => s.GetAlbums(It.Is<ParsedAlbumInfo>(p => p == info), It.IsAny<Artist>(), null))
                      .Returns(albums);
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
