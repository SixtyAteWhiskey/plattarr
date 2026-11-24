using System;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Common.Http.Dispatchers;
using NzbDrone.Test.Common;

namespace NzbDrone.Common.Test.Http
{
    [TestFixture]
    public class PublicDestinationPolicyFixture
    {
        [TestCase("http://127.0.0.1:9696/1/download", "http://127.0.0.1:9696/1/api", true)]
        [TestCase("http://INDEXER.invalid/download", "http://indexer.invalid:80/api", true)]
        [TestCase("http://[::1]:9696/download", "http://[::1]:9696/api", true)]
        [TestCase("http://127.0.0.1:9697/download", "http://127.0.0.1:9696/api", false)]
        [TestCase("http://127.0.0.2:9696/download", "http://127.0.0.1:9696/api", false)]
        [TestCase("http://indexer.invalid/download", "https://indexer.invalid/api", false)]
        [TestCase("http://indexer.invalid.attacker.invalid/download", "http://indexer.invalid/api", false)]
        [TestCase("http://user@indexer.invalid/download", "http://indexer.invalid/api", false)]
        [TestCase("file:///etc/passwd", "file:///etc/config", false)]
        public void should_limit_private_download_permission_to_the_configured_origin(string url, string configured, bool allowed)
        {
            PublicDestinationPolicy.IsConfiguredOrigin(new Uri(url), new Uri(configured)).Should().Be(allowed);
        }

        [Test]
        public void should_require_an_absolute_configured_origin()
        {
            var destination = new Uri("http://127.0.0.1/download");
            PublicDestinationPolicy.IsConfiguredOrigin(destination, null).Should().BeFalse();
            PublicDestinationPolicy.IsConfiguredOrigin(destination, new Uri("/api", UriKind.Relative)).Should().BeFalse();
        }

        [TestCase("http://127.0.0.1/")]
        [TestCase("http://169.254.169.254/")]
        [TestCase("http://10.0.0.1/")]
        [TestCase("http://100.64.0.1/")]
        [TestCase("http://192.168.1.1/")]
        [TestCase("http://[::1]/")]
        [TestCase("http://[::ffff:127.0.0.1]/")]
        [TestCase("file:///etc/passwd")]
        public async Task should_reject_private_or_non_http_release_destinations(string url)
        {
            await FluentActions.Invoking(() => PublicDestinationPolicy.ValidateAsync(new Uri(url)))
                               .Should().ThrowAsync<WebException>();
        }

        [TestCase("https://1.1.1.1/")]
        [TestCase("https://[2606:4700:4700::1111]/")]
        public async Task should_accept_public_ip_destinations(string url)
        {
            await PublicDestinationPolicy.ValidateAsync(new Uri(url));
        }

        [Test]
        public async Task should_allow_trusted_proxy_to_resolve_a_hostname_but_reject_private_literals()
        {
            await PublicDestinationPolicy.ValidateAsync(new Uri("https://proxy-resolved.invalid/"), trustedProxy: true);

            await FluentActions.Invoking(() => PublicDestinationPolicy.ValidateAsync(new Uri("http://127.0.0.1/"), trustedProxy: true))
                               .Should().ThrowAsync<WebException>();
        }
    }

    [TestFixture]
    public class ManagedHttpDispatcherDestinationFixture : TestBase<ManagedHttpDispatcher>
    {
        [TestCase("http://127.0.0.1:9696/", "http://127.0.0.1:9697/private")]
        [TestCase("http://127.0.0.1:9696/", "http://169.254.169.254/private")]
        [TestCase("https://127.0.0.1/", "http://127.0.0.1/private")]
        public async Task should_reject_private_redirects_outside_the_configured_origin(string configured, string redirected)
        {
            var request = new HttpRequest(redirected)
            {
                PublicDestinationOnly = true,
                ConfiguredOrigin = new Uri(configured)
            };

            await FluentActions.Invoking(() => Subject.GetResponseAsync(request, new CookieContainer()))
                               .Should().ThrowAsync<WebException>();
        }

        [Test]
        public async Task should_reject_a_private_release_destination_before_sending()
        {
            var request = new HttpRequest("http://127.0.0.1/private") { PublicDestinationOnly = true };

            await FluentActions.Invoking(() => Subject.GetResponseAsync(request, new CookieContainer()))
                               .Should().ThrowAsync<WebException>();
        }
    }
}
