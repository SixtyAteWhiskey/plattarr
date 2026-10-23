using System.IO;
using System.Net;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Cloud;
using NzbDrone.Common.Http;
using NzbDrone.Core.Tv.MetadataSource.SkyHook;
using NzbDrone.Core.Tv.MetadataSource.SkyHook.Resource;
using NzbDrone.Test.Common.AutoMoq;

namespace NzbDrone.Core.Test.TvTests
{
    // A recorded SkyHook response (shows/eng/121361/official, Game of Thrones, 2026-10-22), so the
    // metadata tests do not depend on a third party service being up.
    internal static class SkyHookTestData
    {
        public const int GameOfThronesTvdbId = 121361;

        public static string GameOfThronesJson => File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Files", "SkyHook", "tvdb-121361-official.json"));

        public static HttpResponse<ShowResource> Response(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            return new HttpResponse<ShowResource>(new HttpResponse(new HttpRequest("https://skyhook.test/"), new HttpHeader(), json, statusCode));
        }

        public static SkyHookProxy CreateProxy(AutoMoqer mocker, HttpResponse<ShowResource> showResponse = null)
        {
            mocker.SetConstant<ISonarrCloudRequestBuilder>(new SonarrCloudRequestBuilder());

            mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Get<ShowResource>(It.IsAny<HttpRequest>()))
                  .Returns(showResponse ?? Response(GameOfThronesJson));

            return mocker.Resolve<SkyHookProxy>();
        }
    }
}
