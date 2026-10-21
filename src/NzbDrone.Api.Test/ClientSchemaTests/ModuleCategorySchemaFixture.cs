using System;
using System.Collections.Generic;
using System.Linq;
using DryIoc;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.Clients.QBittorrent;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Test.Common;
using Radarr.Http.ClientSchema;

namespace NzbDrone.Api.Test.ClientSchemaTests
{
    [TestFixture]
    public class ModuleCategorySchemaFixture : TestBase
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<ILocalizationService>()
                .Setup(s => s.GetLocalizedString(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .Returns<string, Dictionary<string, object>>((key, tokens) => key == "DownloadClientSettingsMediaTypeLabel"
                    ? $"{tokens["label"]} ({tokens["mediaType"]})"
                    : key);
        }

        private void GivenModules(params IMediaModule[] modules)
        {
            // SchemaBuilder asks the container for every registered module.
            foreach (var module in modules)
            {
                Mocker.Container.RegisterInstance<IMediaModule>(module, ifAlreadyRegistered: IfAlreadyRegistered.AppendNotKeyed);
            }

            SchemaBuilder.Initialize(Mocker.Container);
        }

        private static List<string> Names(IEnumerable<Field> schema) => schema.Select(f => f.Name).ToList();

        [Test]
        public void with_only_movies_should_generate_the_upstream_movie_fields_with_plain_labels()
        {
            GivenModules(new MoviesModule());

            var schema = SchemaBuilder.ToSchema(new QBittorrentSettings());

            Names(schema).Should().Contain(new[] { "movieCategory", "movieImportedCategory", "recentMoviePriority", "olderMoviePriority" });
            Names(schema).Should().NotContain(n => n.StartsWith("tv") || n.Contains("Tv"));

            schema.Single(f => f.Name == "movieCategory").Label.Should().Be("Category");
            schema.Single(f => f.Name == "movieCategory").Value.Should().Be("plattarr-movies");
        }

        [Test]
        public void the_imported_category_should_be_an_advanced_field_and_the_others_not()
        {
            GivenModules(new MoviesModule());

            var schema = SchemaBuilder.ToSchema(new QBittorrentSettings());

            schema.Single(f => f.Name == "movieImportedCategory").Advanced.Should().BeTrue();
            schema.Single(f => f.Name == "movieCategory").Advanced.Should().BeFalse();
        }

        [Test]
        public void with_a_second_module_should_generate_its_fields_and_name_the_media_type_in_every_label()
        {
            GivenModules(new MoviesModule(), new FakeTvModule());

            var schema = SchemaBuilder.ToSchema(new QBittorrentSettings());

            Names(schema).Should().Contain(new[] { "movieCategory", "tvCategory", "tvImportedCategory", "recentTvPriority", "olderTvPriority" });

            schema.Single(f => f.Name == "movieCategory").Label.Should().Be("Category (Movies)");
            schema.Single(f => f.Name == "tvCategory").Label.Should().Be("Category (TV)");
        }

        [Test]
        public void a_media_type_with_no_entry_yet_should_show_its_default_category()
        {
            GivenModules(new MoviesModule(), new FakeTvModule());

            var schema = SchemaBuilder.ToSchema(new QBittorrentSettings());

            schema.Single(f => f.Name == "tvCategory").Value.Should().Be("plattarr-tv");
        }

        [Test]
        public void should_put_the_generated_fields_at_the_order_the_class_declares()
        {
            GivenModules(new MoviesModule());

            var schema = SchemaBuilder.ToSchema(new QBittorrentSettings());

            var order = typeof(QBittorrentSettings).GetCustomAttributes(typeof(ModuleCategoryFieldsAttribute), false).Cast<ModuleCategoryFieldsAttribute>().Single().Order;

            schema.Single(f => f.Name == "movieCategory").Order.Should().Be(order);
            schema.Single(f => f.Name == "password").Order.Should().BeLessThan(order);
        }

        [Test]
        public void should_add_an_entry_for_a_media_type_when_its_field_is_saved()
        {
            GivenModules(new MoviesModule(), new FakeTvModule());

            var schema = SchemaBuilder.ToSchema(new QBittorrentSettings());
            schema.Single(f => f.Name == "tvCategory").Value = "my-tv";
            schema.Single(f => f.Name == "movieCategory").Value = "my-movies";

            var settings = (QBittorrentSettings)SchemaBuilder.ReadFromSchema(schema, typeof(QBittorrentSettings), null);

            settings.ModuleCategories.Select(c => (c.Kind, c.Category)).Should().BeEquivalentTo(new[] { (MediaKind.Movies, "my-movies"), (MediaKind.Tv, "my-tv") });
        }

        [Test]
        public void should_read_back_what_it_wrote()
        {
            GivenModules(new MoviesModule(), new FakeTvModule());

            var original = new QBittorrentSettings();
            original.For(MediaKind.Movies).ImportedCategory = "done";
            original.For(MediaKind.Movies).RecentPriority = 1;
            original.ModuleCategories.Add(new ModuleCategorySettings { Kind = MediaKind.Tv, Category = "tv", OlderPriority = 1 });

            var read = (QBittorrentSettings)SchemaBuilder.ReadFromSchema(SchemaBuilder.ToSchema(original), typeof(QBittorrentSettings), null);

            read.ModuleCategories.Should().BeEquivalentTo(original.ModuleCategories);
        }

        [Test]
        public void a_class_without_the_attribute_should_get_no_generated_fields()
        {
            GivenModules(new MoviesModule(), new FakeTvModule());

            var schema = SchemaBuilder.ToSchema(new TestModel());

            Names(schema).Should().NotContain(n => n.EndsWith("Category"));
        }

        private class FakeTvModule : IMediaModule
        {
            public MediaKind Kind => MediaKind.Tv;
            public string Name => "TV";
            public IReadOnlyList<int> DefaultCategories => new[] { 5000 };
            public IEnumerable<ScheduledTask> ScheduledTasks => Array.Empty<ScheduledTask>();
        }
    }
}
