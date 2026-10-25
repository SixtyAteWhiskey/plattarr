using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Radarr.Http.Middleware
{
    public class SeerrCompatibilityMiddleware
    {
        public const string ApplicationItemKey = "SeerrApplication";
        public const string RadarrApplication = "Radarr";
        public const string SonarrApplication = "Sonarr";

        private readonly RequestDelegate _next;

        public SeerrCompatibilityMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments(
                    "/radarr/api/v3",
                    StringComparison.OrdinalIgnoreCase,
                    out var radarrPath))
            {
                context.Items[ApplicationItemKey] = RadarrApplication;
                context.Request.Path = new PathString("/api/v3").Add(radarrPath);
            }
            else if (context.Request.Path.StartsWithSegments(
                         "/sonarr/api/v3",
                         StringComparison.OrdinalIgnoreCase,
                         out var sonarrPath))
            {
                context.Items[ApplicationItemKey] = SonarrApplication;
                context.Request.Path = sonarrPath.Equals(
                    new PathString("/rootfolder"),
                    StringComparison.OrdinalIgnoreCase)
                    ? new PathString("/api/v3/tv/rootfolder")
                    : new PathString("/api/v3").Add(sonarrPath);
            }

            await _next(context);
        }

        public static string GetApplication(HttpContext context)
        {
            return context != null && context.Items.TryGetValue(ApplicationItemKey, out var application)
                ? application as string
                : null;
        }
    }
}
