using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class AudioQualityProfileRepositoryFixture : DbTest<AudioQualityProfileRepository, AudioQualityProfile>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<ICustomFormatService>().Setup(s => s.All()).Returns(new List<CustomFormat>());
        }

        [Test]
        public void should_be_able_to_read_and_write()
        {
            var profile = new AudioQualityProfile
            {
                Items = MusicTestData.DefaultQualities(AudioQuality.MP3_320, AudioQuality.MP3_192, AudioQuality.MP3_256),
                Cutoff = AudioQuality.MP3_320.Id,
                Name = "TestProfile",
                UpgradeAllowed = true,
                MinFormatScore = 2,
                CutoffFormatScore = 9
            };

            Subject.Insert(profile);

            StoredModel.Name.Should().Be(profile.Name);
            StoredModel.Cutoff.Should().Be(profile.Cutoff);
            StoredModel.UpgradeAllowed.Should().BeTrue();
            StoredModel.MinFormatScore.Should().Be(2);
            StoredModel.CutoffFormatScore.Should().Be(9);
            StoredModel.Items.Should().Equal(profile.Items, (a, b) => a.Quality == b.Quality && a.Allowed == b.Allowed);
        }

        [Test]
        public void should_store_qualities_and_groups_as_ids()
        {
            var profile = MusicTestData.NewQualityProfile();
            profile.Items = new System.Collections.Generic.List<AudioQualityProfileItem>
            {
                new AudioQualityProfileItem { Quality = AudioQuality.MP3_192, Allowed = true },
                new AudioQualityProfileItem
                {
                    Id = 1001,
                    Name = "Lossless",
                    Allowed = true,
                    Items = new System.Collections.Generic.List<AudioQualityProfileItem>
                    {
                        new AudioQualityProfileItem { Quality = AudioQuality.FLAC, Allowed = true },
                        new AudioQualityProfileItem { Quality = AudioQuality.ALAC, Allowed = true }
                    }
                }
            };
            profile.Cutoff = 1001;

            Subject.Insert(profile);

            var stored = Subject.Get(profile.Id);

            stored.Cutoff.Should().Be(1001);
            stored.Items[0].Quality.Should().Be(AudioQuality.MP3_192);
            stored.Items[1].Quality.Should().BeNull();
            stored.Items[1].Name.Should().Be("Lossless");
            stored.Items[1].Items.Select(i => i.Quality).Should().Equal(AudioQuality.FLAC, AudioQuality.ALAC);
            stored.GetIndex(AudioQuality.ALAC).Index.Should().Be(1);
            stored.GetIndex(1001).Index.Should().Be(1);
            stored.FirstAllowedQuality().Should().Be(AudioQuality.MP3_192);
            stored.LastAllowedQuality().Should().Be(AudioQuality.ALAC);
        }

        [Test]
        public void should_report_whether_a_profile_exists()
        {
            var profile = MusicTestData.NewQualityProfile();
            Subject.Insert(profile);

            Subject.Exists(profile.Id).Should().BeTrue();
            Subject.Exists(profile.Id + 100).Should().BeFalse();
        }

        [Test]
        public void should_keep_names_unique()
        {
            Subject.Insert(MusicTestData.NewQualityProfile("Same"));

            System.Action insert = () => Subject.Insert(MusicTestData.NewQualityProfile("Same"));

            insert.Should().Throw<System.Exception>();
        }
    }

    [TestFixture]
    public class MetadataProfileRepositoryFixture : DbTest<MetadataProfileRepository, MetadataProfile>
    {
        [Test]
        public void should_be_able_to_read_and_write()
        {
            var profile = new MetadataProfile
            {
                PrimaryAlbumTypes = PrimaryAlbumType.All.OrderByDescending(l => l.Name).Select(l => new ProfilePrimaryAlbumTypeItem
                {
                    PrimaryAlbumType = l,
                    Allowed = l == PrimaryAlbumType.Album
                }).ToList(),

                SecondaryAlbumTypes = SecondaryAlbumType.All.OrderByDescending(l => l.Name).Select(l => new ProfileSecondaryAlbumTypeItem
                {
                    SecondaryAlbumType = l,
                    Allowed = l == SecondaryAlbumType.Studio
                }).ToList(),

                ReleaseStatuses = ReleaseStatus.All.OrderByDescending(l => l.Name).Select(l => new ProfileReleaseStatusItem
                {
                    ReleaseStatus = l,
                    Allowed = l == ReleaseStatus.Official
                }).ToList(),

                Name = "TestProfile"
            };

            Subject.Insert(profile);

            StoredModel.Name.Should().Be(profile.Name);

            StoredModel.PrimaryAlbumTypes.Should().Equal(profile.PrimaryAlbumTypes, (a, b) => a.PrimaryAlbumType == b.PrimaryAlbumType && a.Allowed == b.Allowed);
            StoredModel.SecondaryAlbumTypes.Should().Equal(profile.SecondaryAlbumTypes, (a, b) => a.SecondaryAlbumType == b.SecondaryAlbumType && a.Allowed == b.Allowed);
            StoredModel.ReleaseStatuses.Should().Equal(profile.ReleaseStatuses, (a, b) => a.ReleaseStatus == b.ReleaseStatus && a.Allowed == b.Allowed);
        }

        [Test]
        public void should_store_album_types_as_ids()
        {
            var profile = MusicTestData.NewMetadataProfile();
            Subject.Insert(profile);

            using var conn = Mocker.Resolve<NzbDrone.Core.Datastore.IMainDatabase>().OpenConnection();
            var json = Dapper.SqlMapper.ExecuteScalar<string>(conn, $"SELECT \"PrimaryAlbumTypes\" FROM \"Music_MetadataProfiles\" WHERE \"Id\" = {profile.Id}");

            json.Should().Contain($"\"primaryAlbumType\": {PrimaryAlbumType.Album.Id},");
            json.Should().NotContain("Album\"");
        }
    }
}
