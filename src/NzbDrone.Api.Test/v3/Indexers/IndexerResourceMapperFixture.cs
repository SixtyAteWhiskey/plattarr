using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Newznab;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.IndexerVersions;
using NzbDrone.Core.Modules;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Indexers;
using Radarr.Http.ClientSchema;

namespace NzbDrone.Api.Test.v3.Indexers
{
    [TestFixture]
    public class IndexerResourceMapperFixture : TestBase
    {
        [SetUp]
        public void Setup()
        {
            SchemaBuilder.Initialize(Mocker.Container);
        }

        [Test]
        public void should_report_media_kinds_using_the_categories_selected_for_search()
        {
            var capabilities = new IndexerCapabilities();
            capabilities.Categories.AddCategoryMapping(2000, NewznabStandardCategory.Movies);
            capabilities.Categories.AddCategoryMapping(5000, NewznabStandardCategory.TV);
            capabilities.Categories.AddCategoryMapping(3000, NewznabStandardCategory.Audio);

            var definition = new IndexerDefinition
            {
                Id = 42,
                Name = "Test",
                Implementation = "Newznab",
                ImplementationName = "Newznab",
                Settings = new NewznabSettings(),
                Tags = new HashSet<int>(),
                SupportsSearch = true,
                Capabilities = capabilities
            };

            var categories = new Mock<IIndexerCategoryService>();
            categories.Setup(c => c.GetCategories(42, MediaKind.Movies)).Returns(new[] { 2000 });
            categories.Setup(c => c.GetCategories(42, MediaKind.Tv)).Returns(new[] { 5000 });
            categories.Setup(c => c.GetCategories(42, MediaKind.Music)).Returns(new[] { 7000 });

            var mapper = new IndexerResourceMapper(
                Mock.Of<IIndexerDefinitionUpdateService>(),
                Mock.Of<IIndexerStatusService>(),
                categories.Object);

            mapper.ToResource(definition).SupportedMediaKinds.Should().Equal("movies", "tv");
        }
    }
}
