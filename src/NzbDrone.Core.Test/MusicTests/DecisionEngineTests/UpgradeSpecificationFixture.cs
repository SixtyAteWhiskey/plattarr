using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Music.DecisionEngine.Specifications;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.DecisionEngineTests
{
    [TestFixture]

    public class UpgradeSpecificationFixture : CoreTest<UpgradableSpecification>
    {
        public static object[] IsUpgradeTestCases =
        {
            new object[] { AudioQuality.MP3_192, 1, AudioQuality.MP3_192, 2, AudioQuality.MP3_192, true },
            new object[] { AudioQuality.MP3_320, 1, AudioQuality.MP3_320, 2, AudioQuality.MP3_320, true },
            new object[] { AudioQuality.MP3_192, 1, AudioQuality.MP3_192, 1, AudioQuality.MP3_192, false },
            new object[] { AudioQuality.MP3_320, 1, AudioQuality.MP3_256, 2, AudioQuality.MP3_320, false },
            new object[] { AudioQuality.MP3_320, 1, AudioQuality.MP3_256, 2, AudioQuality.MP3_320, false },
            new object[] { AudioQuality.MP3_320, 1, AudioQuality.MP3_320, 1, AudioQuality.MP3_320, false }
        };

        private void GivenAutoDownloadPropers(ProperDownloadTypes type)
        {
            Mocker.GetMock<IConfigService>()
                  .SetupGet(s => s.DownloadPropersAndRepacks)
                  .Returns(type);
        }

        [Test]
        [TestCaseSource(nameof(IsUpgradeTestCases))]
        public void IsUpgradeTest(AudioQuality current, int currentVersion, AudioQuality newQuality, int newVersion, AudioQuality cutoff, bool expected)
        {
            GivenAutoDownloadPropers(ProperDownloadTypes.PreferAndUpgrade);

            var profile = new AudioQualityProfile
            {
                UpgradeAllowed = true,
                Items = MusicTestData.DefaultQualities()
            };

            Subject.IsUpgradable(
                        profile,
                        new List<AudioQualityModel> { new AudioQualityModel(current, new Revision(version: currentVersion)) },
                        new List<CustomFormat>(),
                        new AudioQualityModel(newQuality, new Revision(version: newVersion)),
                        new List<CustomFormat>())
                   .Should().Be(expected);
        }

        [Test]
        public void should_return_true_if_proper_and_download_propers_is_do_not_download()
        {
            GivenAutoDownloadPropers(ProperDownloadTypes.DoNotUpgrade);

            var profile = new AudioQualityProfile
            {
                Items = MusicTestData.DefaultQualities(),
            };

            Subject.IsUpgradable(
                        profile,
                        new List<AudioQualityModel> { new AudioQualityModel(AudioQuality.MP3_256, new Revision(version: 1)) },
                        new List<CustomFormat>(),
                        new AudioQualityModel(AudioQuality.MP3_256, new Revision(version: 2)),
                        new List<CustomFormat>())
                    .Should().BeTrue();
        }

        [Test]
        public void should_return_false_if_proper_and_autoDownloadPropers_is_do_not_prefer()
        {
            GivenAutoDownloadPropers(ProperDownloadTypes.DoNotPrefer);

            var profile = new AudioQualityProfile
            {
                Items = MusicTestData.DefaultQualities(),
            };

            Subject.IsUpgradable(
                        profile,
                        new List<AudioQualityModel> { new AudioQualityModel(AudioQuality.MP3_256, new Revision(version: 1)) },
                        new List<CustomFormat>(),
                        new AudioQualityModel(AudioQuality.MP3_256, new Revision(version: 2)),
                        new List<CustomFormat>())
                    .Should().BeFalse();
        }
    }
}
