using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Notifications.Emby
{
    public class MediaBrowserProxy
    {
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public MediaBrowserProxy(IHttpClient httpClient, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public void TestConnection(MediaBrowserSettings settings)
        {
            var path = "/System/Configuration";
            var request = BuildRequest(path, settings).Build();

            var response = _httpClient.Get(request);
            _logger.Trace("Response: {0}", response.Content);
        }

        public void Notify(MediaBrowserSettings settings, string title, string message)
        {
            var path = "/Notifications/Admin";
            var request = BuildRequest(path, settings).Build();
            request.Headers.ContentType = "application/json";
            request.LogHttpError = false;

            request.SetContent(new
            {
                Name = title,
                Description = message,
                ImageUrl = "https://raw.github.com/Radarr/Radarr/develop/Logo/64.png"
            }.ToJson());

            ProcessRequest(request);
        }

        public HashSet<string> GetPaths(MediaBrowserSettings settings, ActivitySubject subject)
        {
            var path = "/Items";
            var tmdbId = int.TryParse(subject.GetExternalId(ExternalIdTypes.Tmdb), out var parsedTmdbId) ? parsedTmdbId : 0;
            var imdbId = subject.GetExternalId(ExternalIdTypes.Imdb);
            var tvdbId = int.TryParse(subject.GetExternalId(ExternalIdTypes.Tvdb), out var parsedTvdbId) ? parsedTvdbId : 0;
            var tvMazeId = int.TryParse(subject.GetExternalId(ExternalIdTypes.TvMaze), out var parsedTvMazeId) ? parsedTvMazeId : 0;

            var builder = BuildRequest(path, settings)
                .AddQueryParam("recursive", "true")
                .AddQueryParam("includeItemTypes", GetItemType(subject.Kind))
                .AddQueryParam("fields", "Path,ProviderIds");

            // An artist has no production year on the server, so a year filter would hide it.
            if (subject.Year > 0)
            {
                builder.AddQueryParam("years", subject.Year);
            }

            var request = builder.Build();

            try
            {
                var paths = ProcessGetRequest<MediaBrowserItems>(request).Items.GroupBy(item =>
                {
                    if (item is { ProviderIds.Tmdb: int tmdbid } && tmdbid != 0 && tmdbid == tmdbId)
                    {
                        return MediaBrowserMatchQuality.Id;
                    }

                    if (item is { ProviderIds.Imdb: string imdbid } && imdbid == imdbId)
                    {
                        return MediaBrowserMatchQuality.Id;
                    }

                    if (item is { ProviderIds.Tvdb: int tvdbid } && tvdbid != 0 && tvdbid == tvdbId)
                    {
                        return MediaBrowserMatchQuality.Id;
                    }

                    if (item is { ProviderIds.TvMaze: int tvmazeid } && tvmazeid != 0 && tvmazeid == tvMazeId)
                    {
                        return MediaBrowserMatchQuality.Id;
                    }

                    if (item is { Name: var name } && name == subject.ParentTitle)
                    {
                        return MediaBrowserMatchQuality.Name;
                    }

                    return MediaBrowserMatchQuality.None;
                },
                    item => item.Path).OrderBy(group => (int)group.Key).First();

                if (paths.Key == MediaBrowserMatchQuality.None)
                {
                    _logger.Trace("Could not find {0} by name", subject.KindLabel);

                    return new HashSet<string>();
                }

                _logger.Trace("Found {0} by name/id: {1}", subject.KindLabel, string.Join(" ", paths));

                return paths.ToHashSet();
            }
            catch (InvalidOperationException)
            {
                _logger.Trace("Could not find {0} by name.", subject.KindLabel);

                return new HashSet<string>();
            }
        }

        private static string GetItemType(MediaKind kind)
        {
            return kind switch
            {
                MediaKind.Movies => "Movie",
                MediaKind.Tv => "Series",
                MediaKind.Music => "MusicArtist",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
        }

        public void Update(MediaBrowserSettings settings, string moviePath, string updateType)
        {
            var path = "/Library/Media/Updated";
            var request = BuildRequest(path, settings).Build();
            request.Headers.ContentType = "application/json";

            request.SetContent(new
            {
                Updates = new[]
                {
                    new
                    {
                        Path = moviePath,
                        UpdateType = updateType
                    }
                }
            }.ToJson());

            ProcessRequest(request);
        }

        private T ProcessGetRequest<T>(HttpRequest request)
            where T : new()
        {
            var response = _httpClient.Get<T>(request);
            _logger.Trace("Response: {0}", response.Content);

            CheckForError(response);

            return response.Resource;
        }

        private string ProcessRequest(HttpRequest request)
        {
            var response = _httpClient.Post(request);
            _logger.Trace("Response: {0}", response.Content);

            CheckForError(response);

            return response.Content;
        }

        private string GetUrl(MediaBrowserSettings settings)
        {
            var scheme = settings.UseSsl ? "https" : "http";
            return $@"{scheme}://{settings.Address}";
        }

        private HttpRequestBuilder BuildRequest(string path, MediaBrowserSettings settings)
        {
            var url = GetUrl(settings);
            var request = new HttpRequestBuilder(url).Resource(path);

            request.Headers.Add("X-MediaBrowser-Token", settings.ApiKey);
            request.Headers.Add("Authorization", $"MediaBrowser Token=\"{settings.ApiKey}\"");

            return request;
        }

        private void CheckForError(HttpResponse response)
        {
            _logger.Debug("Looking for error in response: {0}", response);

            // TODO: actually check for the error
        }
    }
}
