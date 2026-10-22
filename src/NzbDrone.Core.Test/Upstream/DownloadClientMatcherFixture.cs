using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    [TestFixture]
    public class DownloadClientMatcherFixture : UpstreamFixtureTest
    {
        private static SourceRow Client(string implementation, string settings)
        {
            return Row(("Implementation", implementation), ("Settings", settings));
        }

        private static string Settings(string host = "qbit.invalid", int port = 8080, bool useSsl = false, string urlBase = null)
        {
            var urlBaseJson = urlBase == null ? string.Empty : $",\"urlBase\":\"{urlBase}\"";

            return $"{{\"host\":\"{host}\",\"port\":{port},\"useSsl\":{(useSsl ? "true" : "false")}{urlBaseJson}}}";
        }

        [Test]
        public void should_match_clients_that_differ_only_in_what_does_not_identify_them()
        {
            // Radarr's qBittorrent client carries a category; Prowlarr's carries none. Neither identifies the client.
            var radarr = Client("QBittorrent", "{\"host\":\"qbit.invalid\",\"port\":8080,\"useSsl\":false,\"movieCategory\":\"radarr\",\"username\":\"a\"}");
            var prowlarr = Client("QBittorrent", "{\"host\":\"qbit.invalid\",\"port\":8080,\"useSsl\":false,\"category\":\"prowlarr\",\"username\":\"b\"}");

            DownloadClientMatcher.AreSame(radarr, prowlarr).Should().BeTrue();
        }

        [Test]
        public void should_ignore_case_in_the_implementation_and_the_host()
        {
            DownloadClientMatcher.AreSame(
                Client("QBittorrent", Settings(host: "QBit.Invalid")),
                Client("qbittorrent", Settings(host: "qbit.invalid"))).Should().BeTrue();
        }

        [Test]
        public void should_not_match_another_implementation()
        {
            DownloadClientMatcher.AreSame(Client("QBittorrent", Settings()), Client("Sabnzbd", Settings())).Should().BeFalse();
        }

        [Test]
        public void should_not_match_another_host()
        {
            DownloadClientMatcher.AreSame(Client("QBittorrent", Settings()), Client("QBittorrent", Settings(host: "other.invalid"))).Should().BeFalse();
        }

        [Test]
        public void should_not_match_another_port()
        {
            DownloadClientMatcher.AreSame(Client("QBittorrent", Settings(port: 8080)), Client("QBittorrent", Settings(port: 8081))).Should().BeFalse();
        }

        [Test]
        public void should_not_match_when_only_one_uses_ssl()
        {
            DownloadClientMatcher.AreSame(Client("QBittorrent", Settings(useSsl: true)), Client("QBittorrent", Settings(useSsl: false))).Should().BeFalse();
        }

        [TestCase("/qbt", "qbt/", true)]
        [TestCase("/qbt/", "/qbt", true)]
        [TestCase("", null, true)]
        [TestCase("/", null, true)]
        [TestCase("/qbt", null, false)]
        [TestCase("/qbt", "/other", false)]
        public void should_compare_the_url_base_without_its_slashes(string left, string right, bool expected)
        {
            DownloadClientMatcher.AreSame(
                Client("QBittorrent", Settings(urlBase: left)),
                Client("QBittorrent", Settings(urlBase: right))).Should().Be(expected);
        }

        [Test]
        public void should_treat_an_unset_ssl_flag_as_off()
        {
            DownloadClientMatcher.AreSame(
                Client("QBittorrent", "{\"host\":\"qbit.invalid\",\"port\":8080}"),
                Client("QBittorrent", Settings(useSsl: false))).Should().BeTrue();
        }

        [Test]
        public void should_not_match_a_client_without_settings()
        {
            DownloadClientMatcher.AreSame(Client("QBittorrent", null), Client("QBittorrent", Settings())).Should().BeFalse();
            DownloadClientMatcher.AreSame(Client("QBittorrent", "[]"), Client("QBittorrent", Settings())).Should().BeFalse();
        }

        [Test]
        public void should_match_the_client_read_from_a_real_radarr_row_with_itself()
        {
            var client = ReadRadarr().Rows("DownloadClients")[0];

            DownloadClientMatcher.AreSame(client, client).Should().BeTrue();
        }
    }
}
