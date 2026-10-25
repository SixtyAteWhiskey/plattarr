using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using Radarr.Http.Middleware;

namespace NzbDrone.App.Test
{
    [TestFixture]
    public class SeerrCompatibilityMiddlewareFixture
    {
        [TestCase(
            "/radarr/api/v3/movie/lookup",
            "/api/v3/movie/lookup",
            SeerrCompatibilityMiddleware.RadarrApplication)]
        [TestCase(
            "/sonarr/api/v3/series/lookup",
            "/api/v3/series/lookup",
            SeerrCompatibilityMiddleware.SonarrApplication)]
        [TestCase(
            "/sonarr/api/v3/rootfolder",
            "/api/v3/tv/rootfolder",
            SeerrCompatibilityMiddleware.SonarrApplication)]
        public async Task should_rewrite_only_the_servarr_api_path(
            string path,
            string expectedPath,
            string expectedApplication)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = path;
            context.Request.QueryString = new QueryString("?apikey=test");

            var middleware = new SeerrCompatibilityMiddleware(nextContext =>
            {
                nextContext.Request.Path.Value.Should().Be(expectedPath);
                nextContext.Request.QueryString.Value.Should().Be("?apikey=test");
                SeerrCompatibilityMiddleware.GetApplication(nextContext).Should().Be(expectedApplication);
                return Task.CompletedTask;
            });

            await middleware.InvokeAsync(context);
        }

        [Test]
        public async Task should_leave_the_native_api_path_unchanged()
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/v3/movie";

            var middleware = new SeerrCompatibilityMiddleware(nextContext =>
            {
                nextContext.Request.Path.Value.Should().Be("/api/v3/movie");
                SeerrCompatibilityMiddleware.GetApplication(nextContext).Should().BeNull();
                return Task.CompletedTask;
            });

            await middleware.InvokeAsync(context);
        }
    }
}
