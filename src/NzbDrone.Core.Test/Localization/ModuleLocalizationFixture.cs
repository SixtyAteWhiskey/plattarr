using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Reflection;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Localization
{
    // The download client schema builds one block of fields per registered module and asks the
    // localization service for each module's help text by key. A missing key is not an error: the
    // UI shows the raw key. This test fails when a module is added without its text.
    [TestFixture]
    public class ModuleLocalizationFixture : CoreTest<LocalizationService>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IConfigService>().Setup(m => m.UILanguage).Returns((int)Language.English);
            Mocker.GetMock<IAppFolderInfo>().Setup(m => m.StartUpFolder).Returns(TestContext.CurrentContext.TestDirectory);
        }

        private static List<IMediaModule> Modules()
        {
            return typeof(IMediaModule).Assembly.ImplementationsOf<IMediaModule>()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Select(t => (IMediaModule)Activator.CreateInstance(t))
                .ToList();
        }

        [Test]
        public void should_find_both_modules()
        {
            Modules().Select(m => m.Kind).Should().BeEquivalentTo(new[] { MediaKind.Movies, MediaKind.Tv });
        }

        [Test]
        public void should_have_priority_help_text_for_every_module()
        {
            foreach (var module in Modules())
            {
                var tag = ModuleCategoryFieldNames.Tag(module.Kind);

                foreach (var key in new[] { $"DownloadClientSettingsRecentPriority{tag}HelpText", $"DownloadClientSettingsOlderPriority{tag}HelpText" })
                {
                    Subject.GetLocalizedString(key).Should().NotBe(key, $"{module.Name} needs the localization key {key}");
                }
            }
        }
    }
}
