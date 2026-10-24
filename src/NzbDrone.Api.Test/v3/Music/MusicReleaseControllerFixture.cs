using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Cache;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.DecisionEngine;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Music.Search;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Music.Releases;

namespace NzbDrone.Api.Test.v3.Music
{
    [TestFixture]
    public class MusicReleaseControllerFixture : TestBase<MusicReleaseController>
    {
        private Cached<RemoteAlbum> _cache;
        private Artist _artist;
        private Album _album;
        private DownloadDecision _decision;

        [SetUp]
        public void Setup()
        {
            _cache = new Cached<RemoteAlbum>();
            Mocker.GetMock<ICacheManager>().Setup(c => c.GetCache<RemoteAlbum>(It.IsAny<Type>(), It.IsAny<string>())).Returns(_cache);

            Mocker.GetMock<IAudioQualityProfileService>()
                  .Setup(s => s.GetDefaultProfile(It.IsAny<string>(), It.IsAny<AudioQuality>(), It.IsAny<AudioQuality[]>()))
                  .Returns(new AudioQualityProfile { Items = AudioQualityProfileItems(), Cutoff = AudioQuality.FLAC.Id });

            _artist = new Artist { Id = 12, Metadata = new ArtistMetadata { Name = "The Black Keys" } };
            _album = new Album { Id = 101, ArtistId = 12, Title = "Brothers" };

            _decision = new DownloadDecision(NewRemoteAlbum(_artist, _album));

            Mocker.GetMock<IPrioritizeDownloadDecision>()
                  .Setup(p => p.PrioritizeDecisions(It.IsAny<List<DownloadDecision>>()))
                  .Returns<List<DownloadDecision>>(d => d);
        }

        private static List<AudioQualityProfileItem> AudioQualityProfileItems()
        {
            return AudioQuality.All
                               .Select(q => new AudioQualityProfileItem { Quality = q, Allowed = true })
                               .ToList();
        }

        private static RemoteAlbum NewRemoteAlbum(Artist artist, Album album)
        {
            return new RemoteAlbum
            {
                Artist = artist,
                Albums = album == null ? new List<Album>() : new List<Album> { album },
                ParsedAlbumInfo = new ParsedAlbumInfo
                {
                    ArtistName = "The Black Keys",
                    AlbumTitle = "Brothers",
                    Quality = new AudioQualityModel(AudioQuality.FLAC)
                },
                Release = new ReleaseInfo { Guid = "guid-1", IndexerId = 3, Title = "The Black Keys - Brothers [FLAC]", DownloadProtocol = DownloadProtocol.Usenet }
            };
        }

        [Test]
        public async Task should_search_the_album_when_an_album_id_is_given()
        {
            Mocker.GetMock<ISearchForReleases>().Setup(s => s.AlbumSearch(101, true, true, true)).ReturnsAsync(new List<DownloadDecision> { _decision });

            var result = await Subject.GetReleases(101, null);

            result.Should().ContainSingle().Which.AlbumTitle.Should().Be("Brothers");
            Mocker.GetMock<ISearchForReleases>().Verify(s => s.ArtistSearch(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public async Task should_search_the_artist_when_only_an_artist_id_is_given()
        {
            Mocker.GetMock<ISearchForReleases>().Setup(s => s.ArtistSearch(12, false, true, true)).ReturnsAsync(new List<DownloadDecision> { _decision });

            var result = await Subject.GetReleases(null, 12);

            result.Should().ContainSingle().Which.ArtistName.Should().Be("The Black Keys");
        }

        [Test]
        public async Task should_read_the_music_rss_and_keep_only_music_releases_when_nothing_is_given()
        {
            var music = new ReleaseInfo { Guid = "music" };
            var other = new ReleaseInfo { Guid = "other" };

            Mocker.GetMock<IIndexerReleaseFetcher>()
                  .Setup(f => f.FetchRss(It.Is<IReadOnlyCollection<MediaKind>>(k => k.Single() == MediaKind.Music)))
                  .ReturnsAsync(new List<ReleaseInfo> { music, other });
            Mocker.GetMock<IIndexerCategoryService>().Setup(s => s.BelongsTo(music, MediaKind.Music)).Returns(true);
            Mocker.GetMock<IIndexerCategoryService>().Setup(s => s.BelongsTo(other, MediaKind.Music)).Returns(false);
            Mocker.GetMock<IMakeDownloadDecision>()
                  .Setup(m => m.GetRssDecision(It.Is<List<ReleaseInfo>>(r => r.Count == 1 && r[0] == music), false))
                  .Returns(new List<DownloadDecision> { _decision });

            var result = await Subject.GetReleases(null, null);

            result.Should().ContainSingle();
        }

        [Test]
        public void should_report_a_failed_search_as_a_bad_request()
        {
            Mocker.GetMock<ISearchForReleases>().Setup(s => s.AlbumSearch(101, true, true, true)).ThrowsAsync(new SearchFailedException("all indexers unavailable"));

            Func<Task> action = () => Subject.GetReleases(101, null);

            action.Should().ThrowAsync<NzbDroneClientException>().Result.Which.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        }

        [Test]
        public void should_report_any_other_search_error_as_a_server_error()
        {
            Mocker.GetMock<ISearchForReleases>().Setup(s => s.ArtistSearch(12, false, true, true)).ThrowsAsync(new InvalidOperationException("boom"));

            Func<Task> action = () => Subject.GetReleases(null, 12);

            action.Should().ThrowAsync<NzbDroneClientException>().Result.Which.StatusCode.Should().Be(System.Net.HttpStatusCode.InternalServerError);
        }

        [Test]
        public async Task should_weight_a_release_by_its_position_in_the_default_profile()
        {
            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.AlbumSearch(101, true, true, true))
                  .ReturnsAsync(new List<DownloadDecision> { _decision, new DownloadDecision(NewRemoteAlbum(_artist, _album)) });

            var result = await Subject.GetReleases(101, null);

            result.Select(r => r.ReleaseWeight).Should().Equal(0, 1);
            result[0].QualityWeight.Should().Be((AudioQualityProfileItems().FindIndex(i => i.Quality == AudioQuality.FLAC) * 100) + 1);
        }

        private MusicReleaseResource Cached(RemoteAlbum remote)
        {
            var resource = new MusicReleaseResource { IndexerId = 3, Guid = "guid-1" };
            _cache.Set("3_guid-1", remote);

            return resource;
        }

        [Test]
        public void should_not_download_a_release_that_is_not_cached()
        {
            Func<Task> action = () => Subject.DownloadRelease(new MusicReleaseResource { IndexerId = 3, Guid = "gone" });

            action.Should().ThrowAsync<NzbDroneClientException>().Result.Which.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
            Mocker.GetMock<IDownloadService>().Verify(d => d.DownloadReport(It.IsAny<RemoteRelease>(), It.IsAny<int?>()), Times.Never());
        }

        [Test]
        public async Task should_download_a_cached_release_with_the_requested_client()
        {
            var remote = NewRemoteAlbum(_artist, _album);
            var resource = Cached(remote);
            resource.DownloadClientId = 9;

            await Subject.DownloadRelease(resource);

            Mocker.GetMock<IDownloadService>().Verify(d => d.DownloadReport(remote, 9), Times.Once());
        }

        [Test]
        public async Task should_take_the_artist_and_album_from_the_album_id_for_an_unmatched_release()
        {
            var remote = NewRemoteAlbum(null, null);
            var resource = Cached(remote);
            resource.AlbumId = 101;

            Mocker.GetMock<IAlbumService>().Setup(s => s.GetAlbum(101)).Returns(_album);
            Mocker.GetMock<IArtistService>().Setup(s => s.GetArtist(12)).Returns(_artist);

            await Subject.DownloadRelease(resource);

            remote.Artist.Should().BeSameAs(_artist);
            remote.Albums.Should().ContainSingle().Which.Should().BeSameAs(_album);
            Mocker.GetMock<IDownloadService>().Verify(d => d.DownloadReport(remote, null), Times.Once());
        }

        [Test]
        public void should_not_download_an_unmatched_release_without_an_artist_or_album()
        {
            var resource = Cached(NewRemoteAlbum(null, null));

            Func<Task> action = () => Subject.DownloadRelease(resource);

            action.Should().ThrowAsync<NzbDroneClientException>().Result.Which.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        }

        [Test]
        public void should_report_a_failed_grab_as_a_conflict()
        {
            var resource = Cached(NewRemoteAlbum(_artist, _album));

            Mocker.GetMock<IDownloadService>()
                  .Setup(d => d.DownloadReport(It.IsAny<RemoteRelease>(), It.IsAny<int?>()))
                  .ThrowsAsync(new ReleaseDownloadException(new ReleaseInfo(), "indexer said no"));

            Func<Task> action = () => Subject.DownloadRelease(resource);

            action.Should().ThrowAsync<NzbDroneClientException>().Result.Which.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
        }
    }
}
