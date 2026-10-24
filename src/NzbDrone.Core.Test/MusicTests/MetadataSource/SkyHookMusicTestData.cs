using System.Collections.Generic;
using System.IO;
using System.Net;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Cloud;
using NzbDrone.Common.Http;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.MetadataSource;
using NzbDrone.Core.Music.MetadataSource.SkyHook;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Test.Common.AutoMoq;

namespace NzbDrone.Core.Test.MusicTests.MetadataSource
{
    // Recorded responses of the Lidarr metadata service (api.lidarr.audio/api/v0.4, fetched 2026-10-24
    // from the cloud container and, for the artist and search routes, from SERVER01 with identical
    // bytes), so the metadata tests do not depend on a third party service being up.
    internal static class SkyHookMusicTestData
    {
        public const string BlackKeysId = "d15721d8-56b4-453d-b506-fc915b14cba2";
        public const string ThickfreaknessId = "35bacad8-4327-33f2-9e7a-3a69721ca707";

        public static string BlackKeysJson => Read($"lidarr-artist-{BlackKeysId}.json");

        public static string ThickfreaknessJson => Read($"lidarr-album-{ThickfreaknessId}.json");

        public static string ArtistSearchJson => Read("lidarr-search-artist-the-black-keys.json");

        private static string Read(string name)
        {
            return File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Files", "SkyHook", name));
        }

        public static HttpResponse<T> Response<T>(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
            where T : new()
        {
            return new HttpResponse<T>(new HttpResponse(new HttpRequest("https://metadata.test/"), new HttpHeader(), json, statusCode));
        }

        // Lidarr's default "Standard" metadata profile: studio albums, official releases.
        public static MetadataProfile StandardProfile()
        {
            return new MetadataProfile
            {
                Id = 1,
                Name = "Standard",
                PrimaryAlbumTypes = new List<ProfilePrimaryAlbumTypeItem> { new ProfilePrimaryAlbumTypeItem { PrimaryAlbumType = PrimaryAlbumType.Album, Allowed = true } },
                SecondaryAlbumTypes = new List<ProfileSecondaryAlbumTypeItem> { new ProfileSecondaryAlbumTypeItem { SecondaryAlbumType = SecondaryAlbumType.Studio, Allowed = true } },
                ReleaseStatuses = new List<ProfileReleaseStatusItem> { new ProfileReleaseStatusItem { ReleaseStatus = ReleaseStatus.Official, Allowed = true } }
            };
        }

        public static SkyHookProxy CreateProxy(AutoMoqer mocker, MetadataProfile profile = null)
        {
            profile ??= StandardProfile();

            mocker.GetMock<IMetadataProfileService>().Setup(s => s.Get(It.IsAny<int>())).Returns(profile);
            mocker.GetMock<IMetadataProfileService>().Setup(s => s.Exists(It.IsAny<int>())).Returns(true);
            mocker.GetMock<IMetadataProfileService>().Setup(s => s.All()).Returns(new List<MetadataProfile> { profile });

            // The real request builder, so the tests see the URL the service would be asked for.
            mocker.SetConstant<ILidarrCloudRequestBuilder>(new LidarrCloudRequestBuilder());
            mocker.SetConstant<IMetadataRequestBuilder>(mocker.Resolve<MetadataRequestBuilder>());

            return mocker.Resolve<SkyHookProxy>();
        }
    }
}
