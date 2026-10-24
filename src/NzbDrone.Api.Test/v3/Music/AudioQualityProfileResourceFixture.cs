using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FluentValidation;
using NUnit.Framework;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Qualities;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Music.Profiles.Quality;

namespace NzbDrone.Api.Test.v3.Music
{
    [TestFixture]
    public class AudioQualityProfileResourceFixture : TestBase
    {
        public class Holder
        {
            public IList<AudioQualityProfileQualityItemResource> Items { get; set; }
            public int Cutoff { get; set; }
        }

        public class HolderValidator : AbstractValidator<Holder>
        {
            public HolderValidator()
            {
                RuleFor(h => h.Items).ValidItems();
                RuleFor(h => h.Cutoff).ValidCutoff();
            }
        }

        private static List<AudioQualityProfileQualityItemResource> AllQualities(bool allowed = true)
        {
            return AudioQuality.All
                .Select(q => new AudioQualityProfileQualityItemResource { Quality = q, Allowed = allowed })
                .ToList();
        }

        private static bool IsValid(List<AudioQualityProfileQualityItemResource> items, int cutoff)
        {
            return new HolderValidator().Validate(new Holder { Items = items, Cutoff = cutoff }).IsValid;
        }

        [Test]
        public void should_accept_every_audio_quality()
        {
            IsValid(AllQualities(), AudioQuality.FLAC.Id).Should().BeTrue();
        }

        [Test]
        public void should_reject_a_profile_missing_an_audio_quality()
        {
            var items = AllQualities();
            items.RemoveAll(i => i.Quality == AudioQuality.FLAC);

            IsValid(items, AudioQuality.MP3_320.Id).Should().BeFalse();
        }

        [Test]
        public void should_not_be_satisfied_by_the_video_catalog()
        {
            // The list a video profile must hold is not what an audio profile is checked against.
            var items = Quality.All.Select(q => new AudioQualityProfileQualityItemResource { Quality = new AudioQuality { Id = q.Id, Name = q.Name }, Allowed = true }).ToList();

            IsValid(items, items.First().Quality.Id).Should().BeFalse();
        }

        [Test]
        public void should_reject_a_cutoff_that_is_not_allowed()
        {
            var items = AllQualities();
            items.Single(i => i.Quality == AudioQuality.FLAC).Allowed = false;

            IsValid(items, AudioQuality.FLAC.Id).Should().BeFalse();
        }

        [Test]
        public void should_reject_a_profile_that_allows_nothing()
        {
            IsValid(AllQualities(false), AudioQuality.FLAC.Id).Should().BeFalse();
        }

        [Test]
        public void should_round_trip_a_profile_with_a_group()
        {
            var profile = new AudioQualityProfile
            {
                Id = 3,
                Name = "Lossless",
                UpgradeAllowed = true,
                Cutoff = 1001,
                MinFormatScore = 0,
                CutoffFormatScore = 5,
                Items = new List<AudioQualityProfileItem>
                {
                    new AudioQualityProfileItem { Quality = AudioQuality.MP3_320, Allowed = false },
                    new AudioQualityProfileItem
                    {
                        Id = 1001,
                        Name = "Lossless",
                        Allowed = true,
                        Items = new List<AudioQualityProfileItem>
                        {
                            new AudioQualityProfileItem { Quality = AudioQuality.FLAC, Allowed = true },
                            new AudioQualityProfileItem { Quality = AudioQuality.ALAC, Allowed = true }
                        }
                    }
                }
            };

            var resource = profile.ToResource();
            var model = resource.ToModel();

            resource.Items[1].Items.Select(i => i.Quality.Id).Should().Equal(AudioQuality.FLAC.Id, AudioQuality.ALAC.Id);
            resource.Items[0].Quality.Id.Should().Be(AudioQuality.MP3_320.Id);

            model.Should().BeEquivalentTo(profile);
        }
    }
}
