using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.MetadataSource.SkyHook;
using NzbDrone.Core.Music.MetadataSource.SkyHook.Resource;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MusicTests.MetadataSource
{
    // Hermetic tests of the Lidarr metadata client: every response is a recorded payload
    // (Files/SkyHook/lidarr-*.json), no network involved. The filter tests are Lidarr's own
    // (SkyHookProxyFixture) with the live calls replaced by the recordings.
    [TestFixture]
    public class SkyHookProxyFixture : CoreTest
    {
        private SkyHookProxy _proxy;
        private MetadataProfile _profile;
        private HttpRequest _lastRequest;

        [SetUp]
        public void Setup()
        {
            _profile = SkyHookMusicTestData.StandardProfile();
            _proxy = SkyHookMusicTestData.CreateProxy(Mocker, _profile);
        }

        private void GivenResponse<T>(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
            where T : new()
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Get<T>(It.IsAny<HttpRequest>()))
                  .Callback<HttpRequest>(r => _lastRequest = r)
                  .Returns(SkyHookMusicTestData.Response<T>(json, statusCode));
        }

        private void GivenArtistResponse(HttpStatusCode statusCode = HttpStatusCode.OK, string json = null)
        {
            GivenResponse<ArtistResource>(json ?? SkyHookMusicTestData.BlackKeysJson, statusCode);
        }

        private void GivenAlbumResponse(HttpStatusCode statusCode = HttpStatusCode.OK, string json = null)
        {
            GivenResponse<AlbumResource>(json ?? SkyHookMusicTestData.ThickfreaknessJson, statusCode);
        }

        [Test]
        public void should_request_the_artist_route_of_the_lidarr_metadata_service()
        {
            GivenArtistResponse();

            _proxy.GetArtistInfo(SkyHookMusicTestData.BlackKeysId, 1);

            _lastRequest.Url.FullUri.Should().Be("https://api.lidarr.audio/api/v0.4/artist/" + SkyHookMusicTestData.BlackKeysId);
            _lastRequest.AllowAutoRedirect.Should().BeTrue();
            _lastRequest.SuppressHttpError.Should().BeTrue();
        }

        [Test]
        public void should_request_the_configured_metadata_source_instead_of_the_default()
        {
            Mocker.GetMock<IConfigService>()
                  .SetupGet(c => c.MusicMetadataSource)
                  .Returns("https://metadata.example/api/");
            GivenArtistResponse();

            _proxy.GetArtistInfo(SkyHookMusicTestData.BlackKeysId, 1);

            _lastRequest.Url.FullUri.Should().Be("https://metadata.example/api/artist/" + SkyHookMusicTestData.BlackKeysId);
        }

        [Test]
        public void should_map_artist_fields()
        {
            GivenArtistResponse();

            var artist = _proxy.GetArtistInfo(SkyHookMusicTestData.BlackKeysId, 1);

            artist.ForeignArtistId.Should().Be(SkyHookMusicTestData.BlackKeysId);
            artist.Name.Should().Be("The Black Keys");
            artist.CleanName.Should().Be(NzbDrone.Core.Music.Parser.Parser.CleanArtistName("The Black Keys"));
            artist.SortName.Should().Be(NzbDrone.Core.Music.Parser.Parser.NormalizeTitle("The Black Keys"));

            var metadata = artist.Metadata.Value;
            metadata.Type.Should().Be("Group");
            metadata.Status.Should().Be(ArtistStatusType.Continuing);
            metadata.Aliases.Should().Equal("Black Keys");
            metadata.Overview.Should().StartWith("The Black Keys are an American rock duo formed in Akron, Ohio");
            metadata.Genres.Should().Contain("Garage Rock Revival");
            metadata.Ratings.Votes.Should().Be(12);
            metadata.Ratings.Value.Should().Be(9.2m);
            metadata.Images.Should().NotBeEmpty();

            // The service's "Logo" image is Plattarr's Clearlogo.
            metadata.Images.Select(i => i.CoverType).Should().Equal(MediaCoverTypes.Clearlogo, MediaCoverTypes.Poster, MediaCoverTypes.Banner, MediaCoverTypes.Fanart);
            metadata.Links.Should().Contain(l => l.Name == "theblackkeys" && l.Url == "https://www.theblackkeys.com/");
        }

        [Test]
        public void should_only_return_the_albums_the_metadata_profile_allows()
        {
            GivenArtistResponse();

            var albums = _proxy.GetArtistInfo(SkyHookMusicTestData.BlackKeysId, 1).Albums.Value;

            // 98 albums are on record; the Standard profile keeps the 14 official studio albums.
            albums.Should().HaveCount(14);
            albums.Select(a => a.Title).Should().Contain(new[] { "Thickfreakness", "Rubber Factory", "Brothers", "El Camino" });
            albums.Select(a => a.Title).Should().NotContain(new[] { "Fever EP", "BBC Sessions", "Ohio" });
            albums.Should().OnlyContain(a => a.AlbumType == "Album");
            albums.Should().OnlyContain(a => !string.IsNullOrEmpty(a.ForeignAlbumId));
        }

        [Test]
        public void should_return_every_album_that_has_a_release_status_when_the_profile_allows_everything()
        {
            _profile.PrimaryAlbumTypes = PrimaryAlbumType.All.Select(t => new ProfilePrimaryAlbumTypeItem { PrimaryAlbumType = t, Allowed = true }).ToList();
            _profile.SecondaryAlbumTypes = SecondaryAlbumType.All.Select(t => new ProfileSecondaryAlbumTypeItem { SecondaryAlbumType = t, Allowed = true }).ToList();
            _profile.ReleaseStatuses = ReleaseStatus.All.Select(t => new ProfileReleaseStatusItem { ReleaseStatus = t, Allowed = true }).ToList();
            GivenArtistResponse();

            // Two of the 98 albums on record have no release status ("Live in New York 2012" and a
            // KCRW broadcast), and the filter requires one, so even an open profile drops them.
            var albums = _proxy.GetArtistInfo(SkyHookMusicTestData.BlackKeysId, 1).Albums.Value;

            albums.Should().HaveCount(96);
            albums.Select(a => a.Title).Should().NotContain(new[] { "Live in New York 2012", "Live At Morning Becomes Eclectic, KCRW 2010" });
        }

        [Test]
        public void should_throw_artist_not_found_for_a_404()
        {
            GivenArtistResponse(HttpStatusCode.NotFound, "{\"error\":\"Not Found\"}");

            Assert.Throws<ArtistNotFoundException>(() => _proxy.GetArtistInfo("66c66aaa-6e2f-4930-8610-912e24c63ed1", 1));
        }

        [Test]
        public void should_throw_bad_request_for_a_400()
        {
            GivenArtistResponse(HttpStatusCode.BadRequest, "{\"error\":\"Invalid UUID\"}");

            Assert.Throws<BadRequestException>(() => _proxy.GetArtistInfo("66c66aaa-6e2f-4930-aaaaaa", 1));
        }

        [Test]
        public void should_throw_skyhook_exception_for_a_503()
        {
            GivenArtistResponse(HttpStatusCode.ServiceUnavailable, "{}");

            Assert.Throws<SkyHookException>(() => _proxy.GetArtistInfo(SkyHookMusicTestData.BlackKeysId, 1));
        }

        [Test]
        public void should_throw_http_exception_for_any_other_error()
        {
            GivenArtistResponse(HttpStatusCode.InternalServerError, "{}");

            Assert.Throws<HttpException>(() => _proxy.GetArtistInfo(SkyHookMusicTestData.BlackKeysId, 1));
        }

        [Test]
        public void should_request_the_album_route_and_map_the_album()
        {
            GivenAlbumResponse();

            var (artistId, album, artists) = _proxy.GetAlbumInfo(SkyHookMusicTestData.ThickfreaknessId);

            _lastRequest.Url.FullUri.Should().Be("https://api.lidarr.audio/api/v0.4/album/" + SkyHookMusicTestData.ThickfreaknessId);

            artistId.Should().Be(SkyHookMusicTestData.BlackKeysId);
            artists.Should().ContainSingle().Which.Name.Should().Be("The Black Keys");

            album.ForeignAlbumId.Should().Be(SkyHookMusicTestData.ThickfreaknessId);
            album.Title.Should().Be("Thickfreakness");
            album.CleanTitle.Should().Be(NzbDrone.Core.Music.Parser.Parser.CleanArtistName("Thickfreakness"));
            album.AlbumType.Should().Be("Album");
            album.SecondaryTypes.Should().BeEmpty();
            album.ReleaseDate.Should().Be(new DateTime(2003, 4, 8, 0, 0, 0, DateTimeKind.Utc));
            album.ReleaseDate.Value.Kind.Should().Be(DateTimeKind.Utc);
            album.AnyReleaseOk.Should().BeTrue();
            album.Ratings.Votes.Should().Be(3);
            album.Ratings.Value.Should().Be(7.3m);
            album.ArtistMetadata.Value.ForeignArtistId.Should().Be(SkyHookMusicTestData.BlackKeysId);
        }

        [Test]
        public void should_map_releases_and_tracks_and_monitor_the_release_with_most_tracks()
        {
            GivenAlbumResponse();

            var album = _proxy.GetAlbumInfo(SkyHookMusicTestData.ThickfreaknessId).Item2;
            var releases = album.AlbumReleases.Value;

            releases.Select(r => r.TrackCount).Should().Equal(11, 12, 11, 11, 11);
            releases.Should().OnlyContain(r => r.Status == "Official");

            // Only the release with the most tracks is monitored.
            releases.Where(r => r.Monitored).Should().ContainSingle().Which.ForeignReleaseId.Should().Be("e3eb1617-e439-4205-a126-50ee9593eb63");

            var first = releases[0];
            first.ForeignReleaseId.Should().Be("36b58cd2-2a2f-36ad-81b3-7ab044d01d19");
            first.Media.Should().ContainSingle().Which.Format.Should().Be("Vinyl");
            first.Tracks.Value.Should().HaveCount(11);

            var track = first.Tracks.Value[0];
            track.ForeignTrackId.Should().Be("832547f2-bfb4-3a94-ad91-01557fe7822c");
            track.ForeignRecordingId.Should().Be("312f5958-4d44-4745-b563-1044521df45e");
            track.Title.Should().Be("Thickfreakness");
            track.TrackNumber.Should().Be("1");
            track.AbsoluteTrackNumber.Should().Be(1);
            track.MediumNumber.Should().Be(1);
            track.Duration.Should().Be(228400);
            first.Duration.Should().Be(first.Tracks.Value.Sum(t => t.Duration));
        }

        [Test]
        public void should_throw_album_not_found_for_a_404()
        {
            GivenAlbumResponse(HttpStatusCode.NotFound, "{\"error\":\"Not Found\"}");

            Assert.Throws<AlbumNotFoundException>(() => _proxy.GetAlbumInfo("66c66aaa-6e2f-4930-8610-912e24c63ed1"));
        }

        [Test]
        public void should_throw_bad_request_for_an_invalid_album_id()
        {
            GivenAlbumResponse(HttpStatusCode.BadRequest, "{\"error\":\"Invalid UUID\"}");

            Assert.Throws<BadRequestException>(() => _proxy.GetAlbumInfo("66c66aaa-6e2f-4930-aaaaaa"));
        }

        [Test]
        public void should_search_for_artists_with_a_lowercased_trimmed_query()
        {
            GivenResponse<List<ArtistResource>>(SkyHookMusicTestData.ArtistSearchJson);

            var results = _proxy.SearchForNewArtist("  The Black Keys ");

            _lastRequest.Url.FullUri.Should().Be("https://api.lidarr.audio/api/v0.4/search?type=artist&query=the%20black%20keys");

            results.Should().HaveCount(10);
            results[0].ForeignArtistId.Should().Be(SkyHookMusicTestData.BlackKeysId);
            results[0].Name.Should().Be("The Black Keys");
            results[1].Name.Should().Be("Goblin");
        }

        [Test]
        public void should_return_the_known_artist_for_a_search_hit_already_in_the_library()
        {
            GivenResponse<List<ArtistResource>>(SkyHookMusicTestData.ArtistSearchJson);
            var known = new Artist { Id = 7 };
            Mocker.GetMock<IArtistService>().Setup(s => s.FindById(SkyHookMusicTestData.BlackKeysId)).Returns(known);

            _proxy.SearchForNewArtist("the black keys")[0].Should().BeSameAs(known);
        }

        [Test]
        public void should_return_nothing_for_an_invalid_mbid_query()
        {
            _proxy.SearchForNewArtist("lidarr:not-a-guid").Should().BeEmpty();
            _proxy.SearchForNewArtist("mbid: ").Should().BeEmpty();

            Mocker.GetMock<IHttpClient>().Verify(c => c.Get<ArtistResource>(It.IsAny<HttpRequest>()), Times.Never());
        }

        [Test]
        public void should_look_the_artist_up_by_id_for_an_mbid_query()
        {
            GivenArtistResponse();

            var results = _proxy.SearchForNewArtist("lidarr:" + SkyHookMusicTestData.BlackKeysId);

            results.Should().ContainSingle().Which.Name.Should().Be("The Black Keys");
            _lastRequest.Url.FullUri.Should().EndWith("/artist/" + SkyHookMusicTestData.BlackKeysId);
        }

        [Test]
        public void should_return_nothing_when_the_mbid_query_names_an_unknown_artist()
        {
            GivenArtistResponse(HttpStatusCode.NotFound, "{}");

            _proxy.SearchForNewArtist("lidarr:" + SkyHookMusicTestData.BlackKeysId).Should().BeEmpty();
        }

        [Test]
        public void should_wrap_a_failed_search_in_a_skyhook_exception()
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Get<List<ArtistResource>>(It.IsAny<HttpRequest>()))
                  .Throws(new WebException("no route to host"));

            Assert.Throws<SkyHookException>(() => _proxy.SearchForNewArtist("the black keys"));

            ExceptionVerification.ExpectedWarns(1);
        }

        public List<AlbumResource> GivenExampleAlbums()
        {
            var result = new List<AlbumResource>();

            foreach (var primaryType in PrimaryAlbumType.All)
            {
                foreach (var secondaryType in SecondaryAlbumType.All)
                {
                    var secondaryTypes = secondaryType.Name == "Studio" ? new List<string>() : new List<string> { secondaryType.Name };
                    foreach (var releaseStatus in ReleaseStatus.All)
                    {
                        var releaseStatuses = new List<string> { releaseStatus.Name };
                        result.Add(new AlbumResource
                        {
                            Type = primaryType.Name,
                            SecondaryTypes = secondaryTypes,
                            ReleaseStatuses = releaseStatuses
                        });
                    }
                }
            }

            return result;
        }

        [TestCaseSource(typeof(PrimaryAlbumType), "All")]
        public void should_filter_albums_by_primary_release_type(PrimaryAlbumType type)
        {
            _profile.PrimaryAlbumTypes = new List<ProfilePrimaryAlbumTypeItem>
            {
                new ProfilePrimaryAlbumTypeItem { PrimaryAlbumType = type, Allowed = true }
            };

            _proxy.FilterAlbums(GivenExampleAlbums(), 1).Select(x => x.Type).Distinct()
                  .Should().BeEquivalentTo(new List<string> { type.Name });
        }

        [TestCaseSource(typeof(SecondaryAlbumType), "All")]
        public void should_filter_albums_by_secondary_release_type(SecondaryAlbumType type)
        {
            _profile.SecondaryAlbumTypes = new List<ProfileSecondaryAlbumTypeItem>
            {
                new ProfileSecondaryAlbumTypeItem { SecondaryAlbumType = type, Allowed = true }
            };

            _proxy.FilterAlbums(GivenExampleAlbums(), 1)
                  .SelectMany(x => x.SecondaryTypes.Select(SkyHookProxy.MapSecondaryTypes))
                  .Select(x => x.Name)
                  .Distinct()
                  .Should().BeEquivalentTo(type.Name == "Studio" ? new List<string>() : new List<string> { type.Name });
        }

        [TestCaseSource(typeof(ReleaseStatus), "All")]
        public void should_filter_albums_by_release_status(ReleaseStatus type)
        {
            _profile.ReleaseStatuses = new List<ProfileReleaseStatusItem>
            {
                new ProfileReleaseStatusItem { ReleaseStatus = type, Allowed = true }
            };

            _proxy.FilterAlbums(GivenExampleAlbums(), 1).SelectMany(x => x.ReleaseStatuses).Distinct()
                  .Should().BeEquivalentTo(new List<string> { type.Name });
        }

        // An album with no secondary types is a studio album. It must pass only when the profile allows
        // Studio, and an album tagged with a secondary type must pass only when that type is allowed.
        [Test]
        public void should_treat_an_album_without_secondary_types_as_studio()
        {
            var albums = new List<AlbumResource>
            {
                new AlbumResource { Type = "Album", SecondaryTypes = new List<string>(), ReleaseStatuses = new List<string> { "Official" } },
                new AlbumResource { Type = "Album", SecondaryTypes = new List<string> { "Live" }, ReleaseStatuses = new List<string> { "Official" } }
            };

            _profile.SecondaryAlbumTypes = new List<ProfileSecondaryAlbumTypeItem>
            {
                new ProfileSecondaryAlbumTypeItem { SecondaryAlbumType = SecondaryAlbumType.Live, Allowed = true },
                new ProfileSecondaryAlbumTypeItem { SecondaryAlbumType = SecondaryAlbumType.Studio, Allowed = false }
            };

            var withoutStudio = _proxy.FilterAlbums(albums, 1).ToList();

            withoutStudio.Should().ContainSingle().Which.SecondaryTypes.Should().Equal("Live");

            _profile.SecondaryAlbumTypes = new List<ProfileSecondaryAlbumTypeItem>
            {
                new ProfileSecondaryAlbumTypeItem { SecondaryAlbumType = SecondaryAlbumType.Live, Allowed = false },
                new ProfileSecondaryAlbumTypeItem { SecondaryAlbumType = SecondaryAlbumType.Studio, Allowed = true }
            };

            var withStudio = _proxy.FilterAlbums(albums, 1).ToList();

            withStudio.Should().ContainSingle().Which.SecondaryTypes.Should().BeEmpty();
        }
    }
}
