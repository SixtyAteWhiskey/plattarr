using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Music.DecisionEngine.Specifications;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.DecisionEngineTests
{
    [TestFixture]
    public class CutoffSpecificationFixture : CoreTest<UpgradableSpecification>
    {
        [Test]
        public void should_return_true_if_current_album_is_less_than_cutoff()
        {
            Subject.CutoffNotMet(
             new AudioQualityProfile
             {
                 Cutoff = AudioQuality.MP3_256.Id,
                 Items = MusicTestData.DefaultQualities(),
                 UpgradeAllowed = true
             },
             new List<AudioQualityModel> { new AudioQualityModel(AudioQuality.MP3_192, new Revision(version: 2)) },
             new List<CustomFormat>()).Should().BeTrue();
        }

        [Test]
        public void should_return_false_if_current_album_is_equal_to_cutoff()
        {
            Subject.CutoffNotMet(
            new AudioQualityProfile
            {
                Cutoff = AudioQuality.MP3_256.Id,
                Items = MusicTestData.DefaultQualities(),
                UpgradeAllowed = true
            },
            new List<AudioQualityModel> { new AudioQualityModel(AudioQuality.MP3_256, new Revision(version: 2)) },
            new List<CustomFormat>()).Should().BeFalse();
        }

        [Test]
        public void should_return_false_if_current_album_is_greater_than_cutoff()
        {
            Subject.CutoffNotMet(
            new AudioQualityProfile
            {
                Cutoff = AudioQuality.MP3_256.Id,
                Items = MusicTestData.DefaultQualities(),
                UpgradeAllowed = true
            },
            new List<AudioQualityModel> { new AudioQualityModel(AudioQuality.MP3_320, new Revision(version: 2)) },
            new List<CustomFormat>()).Should().BeFalse();
        }

        [Test]
        public void should_return_true_when_new_album_is_proper_but_existing_is_not()
        {
            Subject.CutoffNotMet(
            new AudioQualityProfile
            {
                Cutoff = AudioQuality.MP3_320.Id,
                Items = MusicTestData.DefaultQualities(),
                UpgradeAllowed = true
            },
            new List<AudioQualityModel> { new AudioQualityModel(AudioQuality.MP3_320, new Revision(version: 1)) },
            new List<CustomFormat>(),
            new AudioQualityModel(AudioQuality.MP3_320, new Revision(version: 2))).Should().BeTrue();
        }

        [Test]
        public void should_return_false_if_cutoff_is_met_and_quality_is_higher()
        {
            Subject.CutoffNotMet(
            new AudioQualityProfile
            {
                Cutoff = AudioQuality.MP3_320.Id,
                Items = MusicTestData.DefaultQualities(),
                UpgradeAllowed = true
            },
            new List<AudioQualityModel> { new AudioQualityModel(AudioQuality.MP3_320, new Revision(version: 2)) },
            new List<CustomFormat>(),
            new AudioQualityModel(AudioQuality.FLAC, new Revision(version: 2))).Should().BeFalse();
        }

        [Test]
        public void should_return_true_if_cutoffs_are_met_but_is_a_revision_upgrade()
        {
            var profile = new AudioQualityProfile
            {
                Cutoff = AudioQuality.MP3_320.Id,
                Items = MusicTestData.DefaultQualities(),
                UpgradeAllowed = true
            };

            Subject.CutoffNotMet(
                profile,
                new List<AudioQualityModel> { new AudioQualityModel(AudioQuality.FLAC, new Revision(version: 1)) },
                new List<CustomFormat>(),
                new AudioQualityModel(AudioQuality.FLAC, new Revision(version: 2))).Should().BeTrue();
        }

        [Test]
        public void should_return_false_if_quality_profile_does_not_allow_upgrades_but_cutoff_is_set_to_highest_quality()
        {
            var profile = new AudioQualityProfile
            {
                Cutoff = AudioQuality.FLAC_24.Id,
                Items = MusicTestData.DefaultQualities(),
                UpgradeAllowed = false
            };

            Subject.CutoffNotMet(
                profile,
                new List<AudioQualityModel> { new AudioQualityModel(AudioQuality.MP3_320, new Revision(version: 1)) },
                new List<CustomFormat>(),
                new AudioQualityModel(AudioQuality.FLAC, new Revision(version: 2))).Should().BeFalse();
        }
    }
}
