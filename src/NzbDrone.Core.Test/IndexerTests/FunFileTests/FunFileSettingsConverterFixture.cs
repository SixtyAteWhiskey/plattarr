using System;
using System.Linq;
using System.Text.Json.Nodes;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Converters;
using NzbDrone.Core.Indexers.Definitions;
using NzbDrone.Core.Indexers.Definitions.Cardigann;
using NzbDrone.Core.Indexers.Settings;

namespace NzbDrone.Core.Test.IndexerTests.FunFileTests
{
    [TestFixture]
    public class FunFileSettingsConverterFixture
    {
        private const string NativeSettings = """
            {
              "baseUrl": "https://www.funfile.org/",
              "username": "sample-user",
              "password": "FAKE-PASSWORD",
              "baseSettings": { "failDownloads": [1] },
              "torrentBaseSettings": { "minimumSeeders": 4, "seedRatio": 1.5, "preferMagnetUrl": true }
            }
            """;

        [Test]
        public void should_preserve_native_settings_in_a_cardigann_stored_row()
        {
            var serializer = new ProviderSettingConverter();
            var native = (UserPassTorrentBaseSettings)serializer.Parse(NativeSettings, typeof(UserPassTorrentBaseSettings));
            var converted = FunFileSettingsConverter.Convert(NativeSettings);
            var stored = serializer.Serialize((CardigannSettings)serializer.Parse(converted, typeof(CardigannSettings)));
            var replacement = (CardigannSettings)serializer.Parse(stored, typeof(CardigannSettings));

            replacement.DefinitionFile.Should().Be("funfile");
            replacement.BaseUrl.Should().Be(native.BaseUrl);
            replacement.BaseSettings.FailDownloads.Should().Equal(native.BaseSettings.FailDownloads);
            replacement.TorrentBaseSettings.MinimumSeeders.Should().Be(native.TorrentBaseSettings.MinimumSeeders);
            replacement.TorrentBaseSettings.SeedRatio.Should().Be(native.TorrentBaseSettings.SeedRatio);
            replacement.TorrentBaseSettings.PreferMagnetUrl.Should().Be(native.TorrentBaseSettings.PreferMagnetUrl);
            replacement.ExtraFieldData["username"].Should().Be(native.Username);
            replacement.ExtraFieldData["password"].Should().Be(native.Password);

            var document = JsonNode.Parse(stored).AsObject();
            document.Any(p => string.Equals(p.Key, "username", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(p.Key, "password", StringComparison.OrdinalIgnoreCase)).Should().BeFalse();
        }

        [Test]
        public void should_accept_upstream_settings_with_different_key_case()
        {
            const string upstream = """
                { "BaseUrl": "https://www.funfile.org/", "Username": "sample-user", "PASSWORD": "FAKE-PASSWORD" }
                """;

            var replacement = (CardigannSettings)new ProviderSettingConverter().Parse(
                FunFileSettingsConverter.Convert(upstream), typeof(CardigannSettings));

            replacement.BaseUrl.Should().Be("https://www.funfile.org/");
            replacement.ExtraFieldData["username"].Should().Be("sample-user");
            replacement.ExtraFieldData["password"].Should().Be("FAKE-PASSWORD");
        }

        [TestCase("{}")]
        [TestCase("[]")]
        [TestCase("{\"username\":\"user\",\"password\":\"pass\",\"extraFieldData\":{\"freeleech\":true}}")]
        public void should_reject_a_conversion_that_would_lose_settings(string settings)
        {
            Action convert = () => FunFileSettingsConverter.Convert(settings);

            convert.Should().Throw<ArgumentException>();
        }
    }
}
