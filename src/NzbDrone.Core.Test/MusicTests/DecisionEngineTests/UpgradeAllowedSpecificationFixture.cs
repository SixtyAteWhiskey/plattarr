using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Music.DecisionEngine.Specifications;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.DecisionEngineTests
{
    [TestFixture]
    public class UpgradeAllowedSpecificationFixture : CoreTest<UpgradableSpecification>
    {
        private CustomFormat _customFormatOne;
        private CustomFormat _customFormatTwo;
        private AudioQualityProfile _qualityProfile;

        [SetUp]
        public void Setup()
        {
            _customFormatOne = new CustomFormat
            {
                Id = 1,
                Name = "One"
            };
            _customFormatTwo = new CustomFormat
            {
                Id = 2,
                Name = "Two"
            };

            _qualityProfile = new AudioQualityProfile
            {
                Cutoff = AudioQuality.FLAC.Id,
                Items = MusicTestData.DefaultQualities(),
                UpgradeAllowed = false,
                CutoffFormatScore = 100,
                FormatItems = new List<ProfileFormatItem>
                {
                    new ProfileFormatItem
                    {
                        Format = _customFormatOne,
                        Score = 50
                    },
                    new ProfileFormatItem
                    {
                        Format = _customFormatTwo,
                        Score = 100
                    }
                }
            };
        }

        [Test]
        public void should_return_false_when_quality_is_better_custom_formats_are_the_same_and_upgrading_is_not_allowed()
        {
            _qualityProfile.UpgradeAllowed = false;

            Subject.IsUpgradeAllowed(
                _qualityProfile,
                new List<AudioQualityModel> { new AudioQualityModel(AudioQuality.MP3_320) },
                new List<CustomFormat>(),
                new AudioQualityModel(AudioQuality.FLAC),
                new List<CustomFormat>())
            .Should().BeFalse();
        }

        [Test]
        public void should_return_false_when_quality_is_same_and_custom_format_is_upgrade_and_upgrading_is_not_allowed()
        {
            _qualityProfile.UpgradeAllowed = false;

            Subject.IsUpgradeAllowed(
                    _qualityProfile,
                    new List<AudioQualityModel> { new AudioQualityModel(AudioQuality.MP3_320) },
                    new List<CustomFormat> { _customFormatOne },
                    new AudioQualityModel(AudioQuality.MP3_320),
                    new List<CustomFormat> { _customFormatTwo })
                .Should().BeFalse();
        }

        [Test]
        public void should_return_true_for_custom_format_upgrade_when_upgrading_is_allowed()
        {
            _qualityProfile.UpgradeAllowed = true;

            Subject.IsUpgradeAllowed(
                _qualityProfile,
                new List<AudioQualityModel> { new AudioQualityModel(AudioQuality.MP3_320) },
                new List<CustomFormat> { _customFormatOne },
                new AudioQualityModel(AudioQuality.MP3_320),
                new List<CustomFormat> { _customFormatTwo })
            .Should().BeTrue();
        }

        [Test]
        public void should_return_true_for_same_custom_format_score_when_upgrading_is_not_allowed()
        {
            _qualityProfile.UpgradeAllowed = false;

            Subject.IsUpgradeAllowed(
                _qualityProfile,
                new List<AudioQualityModel> { new AudioQualityModel(AudioQuality.MP3_320) },
                new List<CustomFormat> { _customFormatOne },
                new AudioQualityModel(AudioQuality.MP3_320),
                new List<CustomFormat> { _customFormatOne })
            .Should().BeTrue();
        }

        [Test]
        public void should_return_true_for_lower_custom_format_score_when_upgrading_is_allowed()
        {
            _qualityProfile.UpgradeAllowed = true;

            Subject.IsUpgradeAllowed(
                _qualityProfile,
                new List<AudioQualityModel> { new AudioQualityModel(AudioQuality.MP3_320) },
                new List<CustomFormat> { _customFormatTwo },
                new AudioQualityModel(AudioQuality.MP3_320),
                new List<CustomFormat> { _customFormatOne })
            .Should().BeTrue();
        }

        [Test]
        public void should_return_true_for_lower_language_when_upgrading_is_not_allowed()
        {
            _qualityProfile.UpgradeAllowed = false;

            Subject.IsUpgradeAllowed(
                _qualityProfile,
                new List<AudioQualityModel> { new AudioQualityModel(AudioQuality.MP3_320) },
                new List<CustomFormat> { _customFormatTwo },
                new AudioQualityModel(AudioQuality.MP3_320),
                new List<CustomFormat> { _customFormatOne })
            .Should().BeTrue();
        }

        [Test]
        public void should_return_true_for_quality_upgrade_when_upgrading_is_allowed()
        {
            _qualityProfile.UpgradeAllowed = true;

            Subject.IsUpgradeAllowed(
                _qualityProfile,
                new List<AudioQualityModel> { new AudioQualityModel(AudioQuality.MP3_320) },
                new List<CustomFormat>(),
                new AudioQualityModel(AudioQuality.FLAC),
                new List<CustomFormat>())
            .Should().BeTrue();
        }

        [Test]
        public void should_return_true_for_same_quality_when_upgrading_is_allowed()
        {
            _qualityProfile.UpgradeAllowed = true;

            Subject.IsUpgradeAllowed(
                _qualityProfile,
                new List<AudioQualityModel> { new AudioQualityModel(AudioQuality.MP3_320) },
                new List<CustomFormat>(),
                new AudioQualityModel(AudioQuality.MP3_320),
                new List<CustomFormat>())
            .Should().BeTrue();
        }

        [Test]
        public void should_return_true_for_same_quality_when_upgrading_is_not_allowed()
        {
            _qualityProfile.UpgradeAllowed = false;

            Subject.IsUpgradeAllowed(
                _qualityProfile,
                new List<AudioQualityModel> { new AudioQualityModel(AudioQuality.MP3_320) },
                new List<CustomFormat>(),
                new AudioQualityModel(AudioQuality.MP3_320),
                new List<CustomFormat>())
            .Should().BeTrue();
        }

        [Test]
        public void should_return_true_for_lower_quality_when_upgrading_is_allowed()
        {
            _qualityProfile.UpgradeAllowed = true;

            Subject.IsUpgradeAllowed(
                _qualityProfile,
                new List<AudioQualityModel> { new AudioQualityModel(AudioQuality.MP3_320) },
                new List<CustomFormat>(),
                new AudioQualityModel(AudioQuality.MP3_192),
                new List<CustomFormat>())
            .Should().BeTrue();
        }

        [Test]
        public void should_return_true_for_lower_quality_when_upgrading_is_not_allowed()
        {
            _qualityProfile.UpgradeAllowed = false;

            Subject.IsUpgradeAllowed(
                _qualityProfile,
                new List<AudioQualityModel> { new AudioQualityModel(AudioQuality.MP3_320) },
                new List<CustomFormat>(),
                new AudioQualityModel(AudioQuality.MP3_192),
                new List<CustomFormat>())
            .Should().BeTrue();
        }
    }
}
