using System;
using Newtonsoft.Json.Linq;

namespace NzbDrone.Core.Upstream
{
    // Two download clients, one from Radarr and one from Prowlarr, are the same
    // client when implementation, host, port, SSL flag and URL base are equal
    // (docs/importer.md, "Download clients"). The Prowlarr copy of a matching pair
    // is dropped as a duplicate.
    public static class DownloadClientMatcher
    {
        public static bool AreSame(SourceRow a, SourceRow b)
        {
            if (!string.Equals(a.GetString("Implementation"), b.GetString("Implementation"), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var left = a.GetJson("Settings") as JObject;
            var right = b.GetJson("Settings") as JObject;

            if (left == null || right == null)
            {
                return false;
            }

            return string.Equals(Text(left, "host"), Text(right, "host"), StringComparison.OrdinalIgnoreCase)
                && Number(left, "port") == Number(right, "port")
                && Flag(left, "useSsl") == Flag(right, "useSsl")
                && string.Equals(UrlBase(left), UrlBase(right), StringComparison.Ordinal);
        }

        private static string Text(JObject settings, string key)
        {
            return settings.GetValue(key, StringComparison.OrdinalIgnoreCase)?.ToString().Trim() ?? string.Empty;
        }

        private static long Number(JObject settings, string key)
        {
            var token = settings.GetValue(key, StringComparison.OrdinalIgnoreCase);

            return token != null && token.Type == JTokenType.Integer ? token.Value<long>() : 0;
        }

        private static bool Flag(JObject settings, string key)
        {
            var token = settings.GetValue(key, StringComparison.OrdinalIgnoreCase);

            return token != null && token.Type == JTokenType.Boolean && token.Value<bool>();
        }

        // /qbt, qbt/ and /qbt/ are the same base; an empty one is the root.
        private static string UrlBase(JObject settings)
        {
            return Text(settings, "urlBase").Trim('/');
        }
    }
}
