using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Converters;
using NzbDrone.Core.Indexers.Definitions.Cardigann;

namespace NzbDrone.Core.Test.IndexerTests.CardigannTests
{
    [TestFixture]
    public class CardigannSettingsFixture
    {
        [Test]
        public void should_restore_typed_extra_fields_from_stored_settings()
        {
            const string json = """
                {
                  "definitionFile": "example",
                  "extraFieldData": {
                    "enabled": true,
                    "sort": 2,
                    "cookie": "sample=value"
                  }
                }
                """;

            var converter = new ProviderSettingConverter();
            var settings = (CardigannSettings)converter.Parse(json, typeof(CardigannSettings));

            settings.ExtraFieldData["enabled"].Should().BeOfType<bool>().Which.Should().BeTrue();
            settings.ExtraFieldData["sort"].Should().BeOfType<long>().Which.Should().Be(2);
            settings.ExtraFieldData["cookie"].Should().BeOfType<string>().Which.Should().Be("sample=value");
        }
    }
}
