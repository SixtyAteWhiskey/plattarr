using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Serializer;

namespace NzbDrone.Common.Http
{
    public class HttpResponse
    {
        private static readonly Regex RegexSetCookie = new("^(.*?)=(.*?)(?:;|$)", RegexOptions.Compiled);
        private static readonly Regex RegexRefresh = new("^(.*?url)=(.*?)(?:;|$)", RegexOptions.Compiled);

        // Radarr and Sonarr signature. Cookies are seeded from the response's own Set-Cookie headers.
        public HttpResponse(HttpRequest request, HttpHeader headers, byte[] binaryData, HttpStatusCode statusCode = HttpStatusCode.OK, Version version = null)
            : this(request, headers, null, binaryData, 0, statusCode, version)
        {
        }

        public HttpResponse(HttpRequest request, HttpHeader headers, string content, HttpStatusCode statusCode = HttpStatusCode.OK, Version version = null)
            : this(request, headers, null, content, 0, statusCode, version)
        {
        }

        // Prowlarr signature. Cookies is the request's cookie jar after the response was applied.
        public HttpResponse(HttpRequest request, HttpHeader headers, CookieCollection cookies, byte[] binaryData, long elapsedTime = 0, HttpStatusCode statusCode = HttpStatusCode.OK, Version version = null)
        {
            Request = request;
            Headers = headers;
            Cookies = cookies ?? GetCookiesFromHeaders(headers);
            ResponseData = binaryData;
            ElapsedTime = elapsedTime;
            StatusCode = statusCode;
            Version = version;
        }

        public HttpResponse(HttpRequest request, HttpHeader headers, CookieCollection cookies, string content, long elapsedTime = 0, HttpStatusCode statusCode = HttpStatusCode.OK, Version version = null)
        {
            Request = request;
            Headers = headers;
            Cookies = cookies ?? GetCookiesFromHeaders(headers);
            ResponseData = Headers.GetEncodingFromContentType().GetBytes(content);
            _content = content;
            ElapsedTime = elapsedTime;
            StatusCode = statusCode;
            Version = version;
        }

        public HttpRequest Request { get; private set; }
        public HttpHeader Headers { get; private set; }
        public CookieCollection Cookies { get; private set; }
        public HttpStatusCode StatusCode { get; private set; }
        public long ElapsedTime { get; private set; }
        public Version Version { get; private set; }
        public byte[] ResponseData { get; private set; }

        private string _content;

        public string Content
        {
            get
            {
                if (_content == null)
                {
                    var encoding = Request?.Encoding ?? Headers.GetEncodingFromContentType();

                    _content = encoding.GetString(ResponseData);
                }

                return _content;
            }
        }

        public bool HasHttpError => (int)StatusCode >= 400;

        public bool HasHttpServerError => (int)StatusCode >= 500;

        // 301 Moved, 302 Found, 303 SeeOther, 307 TemporaryRedirect, and 308 from Radarr;
        // 300 MultipleChoices and the Refresh header from Prowlarr (sites redirecting to a login page).
        public bool HasHttpRedirect => StatusCode == HttpStatusCode.Moved ||
                                       StatusCode == HttpStatusCode.Found ||
                                       StatusCode == HttpStatusCode.SeeOther ||
                                       StatusCode == HttpStatusCode.TemporaryRedirect ||
                                       StatusCode == HttpStatusCode.MultipleChoices ||
                                       StatusCode == HttpStatusCode.PermanentRedirect ||
                                       Headers.ContainsKey("Refresh");

        public string RedirectUrl
        {
            get
            {
                var newUrl = Headers["Location"];

                if (newUrl == null)
                {
                    newUrl = Headers["Refresh"];

                    if (newUrl == null)
                    {
                        return string.Empty;
                    }

                    var match = RegexRefresh.Match(newUrl);

                    if (match.Success)
                    {
                        return (Request.Url + new HttpUri(match.Groups[2].Value)).FullUri;
                    }

                    return string.Empty;
                }

                return (Request.Url + new HttpUri(newUrl)).FullUri;
            }
        }

        public string[] GetCookieHeaders()
        {
            return Headers.GetValues("Set-Cookie") ?? Array.Empty<string>();
        }

        // The request's cookie jar after this response: cookies sent with the request, persistent
        // cookies, and cookies set anywhere in an auto-followed redirect chain.
        public Dictionary<string, string> GetCookies()
        {
            var result = new Dictionary<string, string>();

            foreach (Cookie cookie in Cookies)
            {
                result[cookie.Name] = cookie.Value;
            }

            return result;
        }

        // Cookies from earlier responses in an auto-followed redirect chain. Later values win.
        internal void AddEarlierCookies(CookieCollection earlierCookies)
        {
            var merged = new CookieCollection();

            merged.Add(earlierCookies);
            merged.Add(Cookies);

            Cookies = merged;
        }

        private static CookieCollection GetCookiesFromHeaders(HttpHeader headers)
        {
            var result = new CookieCollection();

            foreach (var header in headers?.GetValues("Set-Cookie") ?? Array.Empty<string>())
            {
                var match = RegexSetCookie.Match(header);

                if (match.Success && match.Groups[1].Value.IsNotNullOrWhiteSpace())
                {
                    try
                    {
                        result.Add(new Cookie(match.Groups[1].Value, match.Groups[2].Value));
                    }
                    catch (CookieException)
                    {
                        // Ignore cookies .NET won't represent, as the regex parser did before
                    }
                }
            }

            return result;
        }

        public override string ToString()
        {
            var result = $"Res: HTTP/{Version} [{Request.Method}] {Request.Url}: {(int)StatusCode}.{StatusCode} ({ResponseData?.Length ?? 0} bytes)";

            if (HasHttpError && Headers.ContentType.IsNotNullOrWhiteSpace() && !Headers.ContentType.Equals("text/html", StringComparison.InvariantCultureIgnoreCase))
            {
                result += Environment.NewLine + Content;
            }

            return result;
        }
    }

    public class HttpResponse<T> : HttpResponse
        where T : new()
    {
        private readonly Lazy<T> _resource;

        public HttpResponse(HttpResponse response)
            : base(response.Request, response.Headers, response.Cookies, response.ResponseData, response.ElapsedTime, response.StatusCode, response.Version)
        {
            _resource = new Lazy<T>(() => Json.Deserialize<T>(response.Content));
        }

        public T Resource => _resource.Value;
    }
}
