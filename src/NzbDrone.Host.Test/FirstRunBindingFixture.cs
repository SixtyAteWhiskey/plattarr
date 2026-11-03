using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using NzbDrone.Common.Exceptions;
using NzbDrone.Host;

namespace NzbDrone.App.Test
{
    [TestFixture]
    public class FirstRunBindingFixture
    {
        private static IConfiguration GetConfig(string bindAddress = null, string method = null, string enabled = null)
        {
            var settings = new System.Collections.Generic.Dictionary<string, string>();

            if (bindAddress != null)
            {
                settings["Radarr:Server:BindAddress"] = bindAddress;
            }

            if (method != null)
            {
                settings["Radarr:Auth:Method"] = method;
            }

            if (enabled != null)
            {
                settings["Radarr:Auth:Enabled"] = enabled;
            }

            return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        }

        [Test]
        public void should_bind_new_instance_to_loopback_by_default()
        {
            Bootstrap.ResolveBindAddress(GetConfig(), false).Should().Be("127.0.0.1");
        }

        [TestCase("*")]
        [TestCase("0.0.0.0")]
        [TestCase("192.168.1.5")]
        public void should_reject_new_unauthenticated_network_binding(string address)
        {
            var action = () => Bootstrap.ResolveBindAddress(GetConfig(address), false);

            action.Should().Throw<RadarrStartupException>();
        }

        [TestCase("127.0.0.1")]
        [TestCase("::1")]
        [TestCase("localhost")]
        public void should_allow_new_unauthenticated_loopback_binding(string address)
        {
            Bootstrap.ResolveBindAddress(GetConfig(address), false).Should().Be(address);
        }

        [TestCase("Forms")]
        [TestCase("External")]
        [TestCase("Oidc")]
        public void should_allow_authenticated_new_network_binding(string method)
        {
            Bootstrap.ResolveBindAddress(GetConfig("*", method), false).Should().Be("*");
        }

        [Test]
        public void should_allow_legacy_forms_enabled_network_binding()
        {
            Bootstrap.ResolveBindAddress(GetConfig("*", enabled: "true"), false).Should().Be("*");
        }

        [Test]
        public void should_preserve_existing_instance_binding()
        {
            Bootstrap.ResolveBindAddress(GetConfig(), true).Should().Be("*");
            Bootstrap.ResolveBindAddress(GetConfig("192.168.1.5"), true).Should().Be("192.168.1.5");
        }
    }
}
