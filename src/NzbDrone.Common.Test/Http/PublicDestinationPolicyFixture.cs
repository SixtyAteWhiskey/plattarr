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
        [Test]
        public async Task should_reject_a_private_release_destination_before_sending()
        {
            var request = new HttpRequest("http://127.0.0.1/private") { PublicDestinationOnly = true };

            await FluentActions.Invoking(() => Subject.GetResponseAsync(request, new CookieContainer()))
                               .Should().ThrowAsync<WebException>();
        }
    }
}
