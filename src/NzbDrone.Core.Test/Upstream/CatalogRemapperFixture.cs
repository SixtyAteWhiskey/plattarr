using System;
using System.Collections.Generic;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Converters;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    // Inputs are the shapes found in the owner's History, MovieFiles and Blocklist
    // columns: {"quality": <int>, "revision": {...}} and [<int>, ...].
    [TestFixture]
    public class CatalogRemapperFixture : CoreTest
    {
        private readonly RadarrCatalog _catalog = new RadarrCatalog();

        [Test]
        public void should_translate_the_quality_id_and_keep_the_revision()
        {
            var stored = JToken.Parse("{\"quality\":7,\"revision\":{\"version\":2,\"real\":1,\"isRepack\":true}}");

            var result = (JObject)CatalogRemapper.RemapQuality(stored, _catalog);

            Quality.FindById(result["quality"].Value<int>()).Name.Should().Be("Bluray-1080p");
            result["revision"].ToString().Should().Be(stored["revision"].ToString());
        }

        [Test]
        public void should_not_change_the_input()
        {
            var stored = JToken.Parse("{\"quality\":7,\"revision\":{\"version\":1,\"real\":0,\"isRepack\":false}}");
            var before = stored.ToString();

            CatalogRemapper.RemapQuality(stored, _catalog);

            stored.ToString().Should().Be(before);
        }

        [Test]
        public void should_find_the_quality_key_in_any_case()
        {
            var result = (JObject)CatalogRemapper.RemapQuality(JToken.Parse("{\"Quality\":9,\"revision\":{}}"), _catalog);

            Quality.FindById(result["Quality"].Value<int>()).Name.Should().Be("HDTV-1080p");
        }

        [TestCase(0, "Unknown")]
        [TestCase(30, "Remux-1080p")]
        [TestCase(22, "BR-DISK")]
        public void should_translate_every_kind_of_quality_the_owner_may_hold(int radarrId, string name)
        {
            var result = (JObject)CatalogRemapper.RemapQuality(JToken.Parse($"{{\"quality\":{radarrId},\"revision\":{{}}}}"), _catalog);

            Quality.FindById(result["quality"].Value<int>()).Name.Should().Be(name);
        }

        [Test]
        public void should_write_quality_the_application_can_read_back()
        {
            var stored = JToken.Parse("{\"quality\":7,\"revision\":{\"version\":2,\"real\":1,\"isRepack\":true}}");

            var json = CatalogRemapper.RemapQuality(stored, _catalog).ToString();

            var model = new EmbeddedDocumentConverter<QualityModel>(new QualityIntConverter()).Parse(json);

            model.Quality.Should().Be(Quality.Bluray1080p);
            model.Revision.Version.Should().Be(2);
            model.Revision.Real.Should().Be(1);
            model.Revision.IsRepack.Should().BeTrue();
        }

        [TestCase("{\"quality\":11,\"revision\":{}}")]
        [TestCase("{\"quality\":999,\"revision\":{}}")]
        public void should_refuse_a_quality_id_radarr_never_had(string json)
        {
            Action remap = () => CatalogRemapper.RemapQuality(JToken.Parse(json), _catalog);

            remap.Should().Throw<CorruptSourceDataException>().WithMessage("*quality ID*Radarr*");
        }

        [TestCase("[]")]
        [TestCase("7")]
        [TestCase("{\"revision\":{}}")]
        [TestCase("{\"quality\":\"7\",\"revision\":{}}")]
        [TestCase("{\"quality\":null,\"revision\":{}}")]
        public void should_refuse_a_quality_column_of_the_wrong_shape(string json)
        {
            Action remap = () => CatalogRemapper.RemapQuality(JToken.Parse(json), _catalog);

            remap.Should().Throw<CorruptSourceDataException>();
        }

        [Test]
        public void should_translate_language_ids()
        {
            var result = (JArray)CatalogRemapper.RemapLanguages(JToken.Parse("[1,5,2]"), _catalog);

            var names = new List<string>();

            foreach (var id in result)
            {
                names.Add(Language.FindById(id.Value<int>()).Name);
            }

            names.Should().Equal("English", "Italian", "French");
        }

        [Test]
        public void should_translate_the_language_ids_radarr_numbers_differently_from_sonarr()
        {
            // Radarr 26 is Hindi and 31 is Arabic; Sonarr numbers them 27 and 26.
            var result = (JArray)CatalogRemapper.RemapLanguages(JToken.Parse("[26,31]"), _catalog);

            Language.FindById(result[0].Value<int>()).Name.Should().Be("Hindi");
            Language.FindById(result[1].Value<int>()).Name.Should().Be("Arabic");
        }

        [Test]
        public void should_write_languages_the_application_can_read_back()
        {
            var json = CatalogRemapper.RemapLanguages(JToken.Parse("[1,-2]"), _catalog).ToString();

            var languages = new EmbeddedDocumentConverter<List<Language>>(new LanguageIntConverter()).Parse(json);

            languages.Should().Equal(Language.English, Language.Original);
        }

        [Test]
        public void should_pass_an_empty_language_list_through()
        {
            CatalogRemapper.RemapLanguages(JToken.Parse("[]"), _catalog).Should().BeOfType<JArray>().Which.Should().BeEmpty();
        }

        [TestCase(null)]
        [TestCase("null")]
        public void should_pass_a_missing_language_column_through_as_null(string json)
        {
            CatalogRemapper.RemapLanguages(json == null ? null : JToken.Parse(json), _catalog).Should().BeNull();
        }

        [TestCase("[999]")]
        [TestCase("[1,58]")]
        public void should_refuse_a_language_id_radarr_never_had(string json)
        {
            Action remap = () => CatalogRemapper.RemapLanguages(JToken.Parse(json), _catalog);

            remap.Should().Throw<CorruptSourceDataException>().WithMessage("*language ID*Radarr*");
        }

        [TestCase("{}")]
        [TestCase("\"1\"")]
        [TestCase("[\"English\"]")]
        [TestCase("[1.5]")]
        public void should_refuse_a_language_column_of_the_wrong_shape(string json)
        {
            Action remap = () => CatalogRemapper.RemapLanguages(JToken.Parse(json), _catalog);

            remap.Should().Throw<CorruptSourceDataException>();
        }

        [Test]
        public void should_not_put_the_stored_value_in_a_message()
        {
            Action remap = () => CatalogRemapper.RemapQuality(JToken.Parse("{\"quality\":424242,\"revision\":{}}"), _catalog);

            remap.Should().Throw<CorruptSourceDataException>().Which.Message.Should().NotContain("424242");
        }
    }
}
