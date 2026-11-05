using System;
using System.IO;
using System.Text;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.Hygiene
{
    public interface IHygieneListSource
    {
        string Load(string inline, string source);
    }

    public class HygieneListSource : IHygieneListSource
    {
        public const int MaximumBytes = 100000;
        private readonly IHttpClient _httpClient;

        public HygieneListSource(IHttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public static void Validate(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return;
            }

            if (source.Length > 2048 || (!IsHttpUrl(source) && !Path.IsPathFullyQualified(source)))
            {
                throw new ArgumentException("List source must be an absolute file path or HTTP/HTTPS URL.");
            }
        }

        public string Load(string inline, string source)
        {
            Validate(source);
            if (string.IsNullOrWhiteSpace(source))
            {
                return inline ?? string.Empty;
            }

            if (IsHttpUrl(source))
            {
                var request = new HttpRequest(source)
                {
                    MaximumResponseBytes = MaximumBytes,
                    PublicDestinationOnly = true,
                    RequestTimeout = TimeSpan.FromSeconds(15),
                    LogHttpError = false
                };

                return RequireContent(_httpClient.Get(request).Content);
            }

            using var stream = File.OpenRead(source);
            if (stream.Length > MaximumBytes)
            {
                throw new InvalidDataException("List source exceeds the size limit.");
            }

            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            return RequireContent(reader.ReadToEnd());
        }

        private static bool IsHttpUrl(string source)
        {
            return Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static string RequireContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content) || Encoding.UTF8.GetByteCount(content) > MaximumBytes)
            {
                throw new InvalidDataException("List source is empty or exceeds the size limit.");
            }

            return content;
        }
    }
}
