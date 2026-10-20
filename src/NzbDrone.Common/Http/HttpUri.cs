using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Common.Http
{
    public partial class HttpUri : IEquatable<HttpUri>
    {
        private readonly string _uri;
        public string FullUri => _uri;

        [GeneratedRegex(@"^(?:(?<scheme>[a-z]+):)?(?://(?<host>[-_A-Z0-9.]+|\[[[A-F0-9:]+\])(?::(?<port>[0-9]{1,5}))?)?(?<path>(?:(?:(?<=^)|/+)[^/?#\r\n]+)+/*|/+)?(?:\?(?<query>[^#\r\n]*))?(?:\#(?<fragment>.*))?$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex UriRegex();

        public HttpUri(string uri)
        {
            _uri = uri ?? string.Empty;

            Parse();
        }

        public HttpUri(string scheme, string host, int? port, string path, string query, string fragment)
        {
            var builder = new StringBuilder();

            if (scheme.IsNotNullOrWhiteSpace())
            {
                builder.Append(scheme);
                builder.Append(':');
            }

            if (host.IsNotNullOrWhiteSpace())
            {
                builder.Append("//");
                builder.Append(host);
                if (port.HasValue)
                {
                    builder.Append(':');
                    builder.Append(port);
                }
            }

            if (path.IsNotNullOrWhiteSpace())
            {
                if (host.IsNotNullOrWhiteSpace() || path.StartsWith("/"))
                {
                    builder.Append('/');
                }

                builder.Append(path.TrimStart('/'));
            }

            if (query.IsNotNullOrWhiteSpace())
            {
                builder.Append('?');
                builder.Append(query);
            }

            if (fragment.IsNotNullOrWhiteSpace())
            {
                builder.Append('#');
                builder.Append(fragment);
            }

            _uri = builder.ToString();

            Parse();
        }

        private void Parse()
        {
            var parseSuccess = Uri.TryCreate(_uri, UriKind.RelativeOrAbsolute, out _);

            var match = UriRegex().Match(_uri);

            var scheme = match.Groups["scheme"];
            var host = match.Groups["host"];
            var port = match.Groups["port"];
            var path = match.Groups["path"];
            var query = match.Groups["query"];
            var fragment = match.Groups["fragment"];

            if (!parseSuccess || (scheme.Success && !host.Success && path.Success))
            {
                throw new ArgumentException("Uri didn't match expected pattern: " + _uri);
            }

            Scheme = scheme.Value;
            Host = host.Value;
            Port = port.Success ? (int?)int.Parse(port.Value) : null;
            Path = path.Value;
            Query = query.Value;
            Fragment = fragment.Value;
        }

        public string Scheme { get; private set; }
        public string Host { get; private set; }
        public int? Port { get; private set; }
        public string Path { get; private set; }
        public string Query { get; private set; }
        public string Fragment { get; private set; }

        private IList<KeyValuePair<string, string>> _queryParams;
        private IList<KeyValuePair<string, string>> QueryParams
        {
            get
            {
                if (_queryParams == null)
                {
                    var dict = new List<KeyValuePair<string, string>>();

                    if (Query.IsNotNullOrWhiteSpace())
                    {
                        foreach (var pair in Query.Split('&'))
                        {
                            var split = pair.Split(new[] { '=' }, 2);

                            if (split.Length == 1)
                            {
                                dict.Add(new KeyValuePair<string, string>(Uri.UnescapeDataString(split[0]), null));
                            }
                            else
                            {
                                dict.Add(new KeyValuePair<string, string>(Uri.UnescapeDataString(split[0]), Uri.UnescapeDataString(split[1])));
                            }
                        }
                    }

                    _queryParams = dict.AsReadOnly();
                }

                return _queryParams;
            }
        }

        public HttpUri CombinePath(string path)
        {
            return new HttpUri(Scheme, Host, Port, CombinePath(Path, path), Query, Fragment);
        }

        public static string CombinePath(string basePath, string relativePath)
        {
            if (relativePath.IsNullOrWhiteSpace())
            {
                return basePath;
            }

            if (basePath.IsNullOrWhiteSpace())
            {
                return relativePath;
            }

            return basePath.TrimEnd('/') + "/" + relativePath.TrimStart('/');
        }

        private static string CombineRelativePath(string basePath, string relativePath)
        {
            if (relativePath.IsNullOrWhiteSpace())
            {
                return basePath;
            }

            if (relativePath.StartsWith("/"))
            {
                return relativePath;
            }

            var baseSlashIndex = basePath.LastIndexOf('/');

            var merged = baseSlashIndex >= 0 ? $"{basePath.AsSpan(0, baseSlashIndex)}/{relativePath}" : relativePath;

            return HasDotSegment(merged) ? RemoveDotSegments(merged) : merged;
        }

        private static bool HasDotSegment(string path)
        {
            return path == "." || path == ".." ||
                   path.StartsWith("./") || path.StartsWith("../") ||
                   path.Contains("/./") || path.Contains("/../") ||
                   path.EndsWith("/.") || path.EndsWith("/..");
        }

        // RFC 3986 section 5.2.4
        private static string RemoveDotSegments(string path)
        {
            var input = path;
            var output = new StringBuilder(path.Length);

            while (input.Length > 0)
            {
                if (input.StartsWith("../"))
                {
                    input = input.Substring(3);
                }
                else if (input.StartsWith("./"))
                {
                    input = input.Substring(2);
                }
                else if (input.StartsWith("/./"))
                {
                    input = input.Substring(2);
                }
                else if (input == "/.")
                {
                    input = "/";
                }
                else if (input.StartsWith("/../") || input == "/..")
                {
                    input = input == "/.." ? "/" : input.Substring(3);

                    var lastSlash = output.ToString().LastIndexOf('/');
                    output.Length = lastSlash >= 0 ? lastSlash : 0;
                }
                else if (input == "." || input == "..")
                {
                    input = string.Empty;
                }
                else
                {
                    var nextSlash = input.IndexOf('/', input.StartsWith("/") ? 1 : 0);
                    var segment = nextSlash >= 0 ? input.Substring(0, nextSlash) : input;

                    output.Append(segment);
                    input = input.Substring(segment.Length);
                }
            }

            var result = output.ToString();

            // A relative base path stays relative
            return path.StartsWith("/") ? result : result.TrimStart('/');
        }

        public HttpUri SetQuery(string query)
        {
            return new HttpUri(Scheme, Host, Port, Path, query, Fragment);
        }

        public HttpUri AddQueryParam(string key, object value)
        {
            var newQuery = string.Concat(Uri.EscapeDataString(key), "=", Uri.EscapeDataString(value.ToString()));

            if (Query.IsNotNullOrWhiteSpace())
            {
                newQuery = string.Concat(Query, "&", newQuery);
            }

            return SetQuery(newQuery);
        }

        public HttpUri AddQueryParams(IEnumerable<KeyValuePair<string, string>> queryParams)
        {
            var builder = new StringBuilder();
            builder.Append(Query);

            foreach (var pair in queryParams)
            {
                if (builder.Length != 0)
                {
                    builder.Append('&');
                }

                builder.Append(Uri.EscapeDataString(pair.Key));
                builder.Append('=');
                builder.Append(Uri.EscapeDataString(pair.Value));
            }

            return SetQuery(builder.ToString());
        }

        public override int GetHashCode()
        {
            return _uri.GetHashCode();
        }

        public override string ToString()
        {
            return _uri;
        }

        public override bool Equals(object obj)
        {
            if (obj is string)
            {
                return _uri.Equals((string)obj);
            }
            else if (obj is Uri)
            {
                return _uri.Equals(((Uri)obj).OriginalString);
            }
            else
            {
                return Equals(obj as HttpUri);
            }
        }

        public bool Equals(HttpUri other)
        {
            if (object.ReferenceEquals(other, null))
            {
                return false;
            }

            return _uri.Equals(other._uri);
        }

        public static explicit operator Uri(HttpUri url)
        {
            return new Uri(url.FullUri);
        }

        public static HttpUri operator +(HttpUri baseUrl, HttpUri relativeUrl)
        {
            if (relativeUrl.Scheme.IsNotNullOrWhiteSpace())
            {
                return relativeUrl;
            }

            if (relativeUrl.Host.IsNotNullOrWhiteSpace())
            {
                return new HttpUri(baseUrl.Scheme, relativeUrl.Host, relativeUrl.Port, relativeUrl.Path, relativeUrl.Query, relativeUrl.Fragment);
            }

            if (relativeUrl.Path.IsNotNullOrWhiteSpace())
            {
                return new HttpUri(baseUrl.Scheme, baseUrl.Host, baseUrl.Port, CombineRelativePath(baseUrl.Path, relativeUrl.Path), relativeUrl.Query, relativeUrl.Fragment);
            }

            return new HttpUri(baseUrl.Scheme, baseUrl.Host, baseUrl.Port, baseUrl.Path, relativeUrl.Query, relativeUrl.Fragment);
        }
    }
}
