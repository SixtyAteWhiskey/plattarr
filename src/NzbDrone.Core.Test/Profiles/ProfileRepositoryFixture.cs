using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.CustomFormats;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Profiles
{
    [TestFixture]
    public class ProfileRepositoryFixture : DbTest<QualityProfileRepository, QualityProfile>
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void should_be_able_to_read_and_write()
        {
            var profile = new QualityProfile
            {
                Items = Qualities.QualityFixture.GetDefaultQualities(Quality.Bluray1080p, Quality.DVD, Quality.HDTV720p),
                MinFormatScore = 0,
                FormatItems = CustomFormatsTestHelpers.GetDefaultFormatItems(),
                Cutoff = Quality.Bluray1080p.Id,
                Name = "TestProfile"
            };

            Subject.Insert(profile);

            StoredModel.Name.Should().Be(profile.Name);
            StoredModel.Cutoff.Should().Be(profile.Cutoff);

            StoredModel.Items.Should().Equal(profile.Items, (a, b) => a.Quality == b.Quality && a.Allowed == b.Allowed);
        }

        [Test]
        public void should_round_trip_applies_to()
        {
            var profile = new QualityProfile
            {
                Items = Qualities.QualityFixture.GetDefaultQualities(Quality.Bluray1080p),
                FormatItems = CustomFormatsTestHelpers.GetDefaultFormatItems(),
                Cutoff = Quality.Bluray1080p.Id,
                Name = "TV only",
                AppliesTo = new List<MediaKind> { MediaKind.Tv }
            };

            Subject.Insert(profile);

            StoredModel.AppliesTo.Should().Equal(MediaKind.Tv);
        }

        [Test]
        public void applies_to_should_default_to_empty()
        {
            Subject.Insert(new QualityProfile
            {
                Items = Qualities.QualityFixture.GetDefaultQualities(Quality.Bluray1080p),
                Cutoff = Quality.Bluray1080p.Id,
                Name = "Default"
            });

            StoredModel.AppliesTo.Should().BeEmpty();
        }
    }
}
