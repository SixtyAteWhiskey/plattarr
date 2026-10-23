using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.AutoTagging;
using NzbDrone.Core.Tv.AutoTagging.Specifications;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Tv.AutoTagging;
using Radarr.Http.ClientSchema;

namespace NzbDrone.Api.Test.v3.Tv
{
    [TestFixture]
    public class TvAutoTaggingResourceFixture : TestBase
    {
        [SetUp]
        public void Setup()
        {
            SchemaBuilder.Initialize(Mocker.Container);
        }

        private static List<IAutoTaggingSpecification> AllSpecifications()
        {
            return new List<IAutoTaggingSpecification>
            {
                new GenreSpecification(),
                new MonitoredSpecification(),
                new NetworkSpecification(),
                new OriginalCountrySpecification(),
                new OriginalLanguageSpecification(),
                new QualityProfileSpecification(),
                new RootFolderSpecification(),
                new SeriesTypeSpecification(),
                new StatusSpecification(),
                new TagSpecification(),
                new YearSpecification()
            };
        }

        private static AutoTag NewAutoTag()
        {
            return new AutoTag
            {
                Id = 3,
                Name = "AMC anime",
                RemoveTagsAutomatically = true,
                Tags = new HashSet<int> { 4, 5 },
                Specifications = new List<IAutoTaggingSpecification>
                {
                    new NetworkSpecification { Name = "Network", Value = new[] { "AMC" }, Required = true },
                    new SeriesTypeSpecification { Name = "Type", Value = (int)SeriesTypes.Anime, Negate = true },
                    new YearSpecification { Name = "Years", Min = 2000, Max = 2010 }
                }
            };
        }

        [Test]
        public void should_round_trip_an_auto_tag_through_the_resource()
        {
            // The API reads the resource from JSON, so the field values arrive as JSON elements.
            var resource = STJson.Deserialize<TvAutoTaggingResource>(STJson.ToJson(NewAutoTag().ToResource()));

            resource.Id.Should().Be(3);
            resource.Specifications.Select(s => s.Implementation).Should().Equal("NetworkSpecification", "SeriesTypeSpecification", "YearSpecification");
            resource.Specifications[0].ImplementationName.Should().Be("Network");
            resource.Specifications[1].Negate.Should().BeTrue();

            var model = resource.ToModel(AllSpecifications());

            model.Id.Should().Be(3);
            model.Name.Should().Be("AMC anime");
            model.RemoveTagsAutomatically.Should().BeTrue();
            model.Tags.Should().BeEquivalentTo(new[] { 4, 5 });

            var network = model.Specifications[0].Should().BeOfType<NetworkSpecification>().Subject;
            network.Value.Should().Equal("AMC");
            network.Required.Should().BeTrue();
            network.Name.Should().Be("Network");

            var type = model.Specifications[1].Should().BeOfType<SeriesTypeSpecification>().Subject;
            type.Value.Should().Be((int)SeriesTypes.Anime);
            type.Negate.Should().BeTrue();

            var years = model.Specifications[2].Should().BeOfType<YearSpecification>().Subject;
            years.Min.Should().Be(2000);
            years.Max.Should().Be(2010);
        }

        [Test]
        public void should_refuse_an_unknown_specification_implementation()
        {
            var resource = NewAutoTag().ToResource();
            resource.Specifications[0].Implementation = "KeywordSpecification";

            var action = () => resource.ToModel(AllSpecifications());

            action.Should().Throw<ArgumentException>().WithMessage("*KeywordSpecification*");
        }

        [Test]
        public void should_list_the_schema_ordered_by_specification_order()
        {
            var service = new Mock<IAutoTaggingService>();
            var controller = new TvAutoTaggingController(service.Object, AllSpecifications());

            var schema = controller.GetTemplates().Should().BeOfType<Ok<List<TvAutoTaggingSpecificationSchema>>>().Subject.Value;

            schema.Should().HaveCount(11);
            schema.Select(s => s.Implementation).Should().Contain(new[] { "NetworkSpecification", "OriginalCountrySpecification", "SeriesTypeSpecification", "TagSpecification" });
            schema.Select(s => s.Implementation).Should().NotContain("KeywordSpecification");
            schema.Should().OnlyContain(s => s.Fields != null);
        }

        [Test]
        public void should_list_every_auto_tag_as_a_resource()
        {
            var service = new Mock<IAutoTaggingService>();
            service.Setup(s => s.All()).Returns(new List<AutoTag> { NewAutoTag() });
            var controller = new TvAutoTaggingController(service.Object, AllSpecifications());

            var all = controller.GetAll().Should().BeOfType<Ok<List<TvAutoTaggingResource>>>().Subject.Value;

            all.Should().ContainSingle().Which.Name.Should().Be("AMC anime");
        }
    }
}
