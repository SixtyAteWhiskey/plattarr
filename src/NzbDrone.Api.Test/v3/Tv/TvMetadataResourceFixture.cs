using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Tv.Extras.Metadata;
using NzbDrone.Core.Tv.Extras.Metadata.Consumers.Xbmc;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Tv.Metadata;
using Radarr.Http.ClientSchema;

namespace NzbDrone.Api.Test.v3.Tv
{
    [TestFixture]
    public class TvMetadataResourceFixture : TestBase
    {
        private readonly TvMetadataResourceMapper _mapper = new();

        [SetUp]
        public void Setup()
        {
            SchemaBuilder.Initialize(Mocker.Container);
        }

        private static MetadataDefinition NewDefinition(bool enable)
        {
            return new MetadataDefinition
            {
                Id = 3,
                Name = "Kodi (XBMC) / Emby",
                Implementation = nameof(XbmcMetadata),
                ConfigContract = nameof(XbmcMetadataSettings),
                Enable = enable,
                Settings = new XbmcMetadataSettings { SeriesMetadata = true, EpisodeMetadata = false }
            };
        }

        [TestCase(true)]
        [TestCase(false)]
        public void should_carry_the_enable_flag_to_the_resource(bool enable)
        {
            var resource = _mapper.ToResource(NewDefinition(enable));

            resource.Id.Should().Be(3);
            resource.Enable.Should().Be(enable);
        }

        [Test]
        public void should_expose_the_series_fields_with_their_values()
        {
            var resource = _mapper.ToResource(NewDefinition(true));

            resource.Fields.Single(f => f.Name == "seriesMetadata").Value.Should().Be(true);
            resource.Fields.Single(f => f.Name == "episodeMetadata").Value.Should().Be(false);
        }

        [Test]
        public void should_carry_the_enable_flag_back_to_the_model()
        {
            var resource = _mapper.ToResource(NewDefinition(false));
            resource.Enable = true;

            var model = _mapper.ToModel(resource, NewDefinition(false));

            model.Enable.Should().BeTrue();
        }

        [Test]
        public void should_map_null_to_null()
        {
            _mapper.ToResource(null).Should().BeNull();
            _mapper.ToModel(null, null).Should().BeNull();
        }
    }
}
