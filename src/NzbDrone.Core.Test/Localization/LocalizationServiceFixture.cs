using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Localization
{
    [TestFixture]
    public class LocalizationServiceFixture : CoreTest<LocalizationService>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IConfigService>().Setup(m => m.UILanguage).Returns((int)Language.English);

            Mocker.GetMock<IAppFolderInfo>().Setup(m => m.StartUpFolder).Returns(TestContext.CurrentContext.TestDirectory);
        }

        [Test]
        public void should_get_string_in_dictionary_if_lang_exists_and_string_exists()
        {
            var localizedString = Subject.GetLocalizedString("UiLanguage");

            localizedString.Should().Be("UI Language");
        }

        [Test]
        public void should_get_string_in_french()
        {
            Mocker.GetMock<IConfigService>().Setup(m => m.UILanguage).Returns((int)Language.French);

            var localizedString = Subject.GetLocalizedString("UiLanguage");

            localizedString.Should().Be("Langue de l'IU");

            ExceptionVerification.ExpectedErrors(0);
        }

        [Test]
        public void should_use_safe_indexer_guidance_for_untranslated_language()
        {
            Mocker.GetMock<IConfigService>().Setup(m => m.UILanguage).Returns((int)Language.French);

            var message = Subject.GetLocalizedString("IndexerLongTermStatusCheckPartialBlockedMessage");

            message.Should().Contain("remain blocked")
                .And.Contain("review local logs")
                .And.NotContain("{indexerNames}");
        }

        [Test]
        public void should_get_string_in_default_dictionary_if_unknown_language_and_string_exists()
        {
            Mocker.GetMock<IConfigService>().Setup(m => m.UILanguage).Returns(0);
            var localizedString = Subject.GetLocalizedString("UiLanguage");

            localizedString.Should().Be("UI Language");
        }

        [Test]
        public void should_return_argument_if_string_doesnt_exists()
        {
            var localizedString = Subject.GetLocalizedString("badString");

            localizedString.Should().Be("badString");
        }

        [Test]
        public void should_return_argument_if_string_doesnt_exists_default_lang()
        {
            var localizedString = Subject.GetLocalizedString("badString");

            localizedString.Should().Be("badString");
        }

        [TestCase("RSSSync")]
        [TestCase("RssSync")]
        [TestCase("IMDbId")]
        [TestCase("ImdbId")]
        [TestCase("TBA")]
        [TestCase("Tba")]
        public void should_keep_both_keys_of_a_pair_that_differs_only_by_case(string key)
        {
            // A list compares at exact case; the dictionary's own Keys and ContainKey follow its comparer.
            Subject.GetLocalizationDictionary().Keys.ToList().Should().Contain(key);
        }

        [Test]
        public void should_return_the_value_stored_under_the_exact_case()
        {
            Subject.GetLocalizedString("IMDbId").Should().Be("IMDb Id");
            Subject.GetLocalizedString("ImdbId").Should().Be("IMDb ID");
        }

        [Test]
        public void should_serve_every_key_in_the_default_dictionary()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Localization", "Core", "en.json");
            var expected = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)).Keys;

            Subject.GetLocalizationDictionary().Keys.Should().BeEquivalentTo(expected);
        }

        [Test]
        public void should_throw_if_empty_string_passed()
        {
            Assert.Throws<ArgumentNullException>(() => Subject.GetLocalizedString(""));
        }

        [Test]
        public void should_throw_if_null_string_passed()
        {
            Assert.Throws<ArgumentNullException>(() => Subject.GetLocalizedString(null));
        }
    }
}
