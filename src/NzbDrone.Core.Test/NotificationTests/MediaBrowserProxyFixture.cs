using System.Collections.Generic;
using System.Linq;
using System.Net;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications.Emby;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.NotificationTests
{
    // How the Emby / Jellyfin library lookup is asked: one query per media kind, with the year only where the
    // parent has one (an artist has none, and a year filter would hide it on the server).
    [TestFixture]
    public class MediaBrowserProxyFixture : CoreTest<MediaBrowserProxy>
    {
        private readonly MediaBrowserSettings _settings = new() { Host = "emby.local", ApiKey = "key" };
        private HttpRequest _request;

        private void ServerHas(string json)
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Get<MediaBrowserItems>(It.IsAny<HttpRequest>()))
                  .Callback<HttpRequest>(r => _request = r)
                  .Returns((HttpRequest r) => new HttpResponse<MediaBrowserItems>(new HttpResponse(r, new HttpHeader(), json, HttpStatusCode.OK)));
        }

        // The server reads the body without regard to case; the serializer writes camelCase.
        private static System.Text.Json.JsonElement Property(System.Text.Json.JsonElement element, string name)
        {
            return element.EnumerateObject().First(p => string.Equals(p.Name, name, System.StringComparison.OrdinalIgnoreCase)).Value;
        }

        private static ActivitySubject Artist() => new()
        {
            Kind = MediaKind.Music,
            KindLabel = "Artist",
            ParentTitle = "The Black Keys",
            ParentPath = "/music/The Black Keys"
        };

        [Test]
        public void should_ask_for_music_artists_without_a_year()
        {
            ServerHas("{\"Items\":[]}");

            Subject.GetPaths(_settings, Artist());

            _request.Url.FullUri.Should().Contain("includeItemTypes=MusicArtist");
            _request.Url.FullUri.Should().Contain("recursive=true");
            _request.Url.FullUri.Should().Contain("fields=Path%2CProviderIds");
            _request.Url.FullUri.Should().NotContain("years=");
        }

        [Test]
        public void should_keep_the_year_for_a_movie()
        {
            ServerHas("{\"Items\":[]}");

            Subject.GetPaths(_settings, new ActivitySubject { Kind = MediaKind.Movies, KindLabel = "Movie", ParentTitle = "Interstellar", Year = 2014 });

            _request.Url.FullUri.Should().Contain("includeItemTypes=Movie");
            _request.Url.FullUri.Should().Contain("years=2014");
        }

        [Test]
        public void should_keep_the_year_for_a_series()
        {
            ServerHas("{\"Items\":[]}");

            Subject.GetPaths(_settings, new ActivitySubject { Kind = MediaKind.Tv, KindLabel = "TV Show", ParentTitle = "Breaking Bad", Year = 2008 });

            _request.Url.FullUri.Should().Contain("includeItemTypes=Series");
            _request.Url.FullUri.Should().Contain("years=2008");
        }

        [Test]
        public void should_find_an_artist_by_name()
        {
            ServerHas("{\"Items\":[{\"Name\":\"The Black Keys\",\"Path\":\"/srv/music/The Black Keys\",\"ProviderIds\":{}},{\"Name\":\"Other\",\"Path\":\"/srv/music/Other\",\"ProviderIds\":{}}]}");

            var paths = Subject.GetPaths(_settings, Artist());

            paths.Should().BeEquivalentTo(new List<string> { "/srv/music/The Black Keys" });
        }

        [Test]
        public void should_find_nothing_when_no_artist_has_the_name()
        {
            ServerHas("{\"Items\":[{\"Name\":\"Other\",\"Path\":\"/srv/music/Other\",\"ProviderIds\":{}}]}");

            Subject.GetPaths(_settings, Artist()).Should().BeEmpty();
        }

        [Test]
        public void should_post_the_path_and_update_type_to_the_library_endpoint()
        {
            HttpRequest posted = null;

            Mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Post(It.IsAny<HttpRequest>()))
                  .Callback<HttpRequest>(r => posted = r)
                  .Returns((HttpRequest r) => new HttpResponse(r, new HttpHeader(), "", HttpStatusCode.NoContent));

            Subject.Update(_settings, "/music/The Black Keys", "Modified");

            posted.Url.FullUri.Should().EndWith("/Library/Media/Updated");
            posted.ContentData.Should().NotBeNull();

            using var body = System.Text.Json.JsonDocument.Parse(posted.ContentData);
            var update = Property(body.RootElement, "Updates")[0];

            Property(update, "Path").GetString().Should().Be("/music/The Black Keys");
            Property(update, "UpdateType").GetString().Should().Be("Modified");
        }
    }
}
