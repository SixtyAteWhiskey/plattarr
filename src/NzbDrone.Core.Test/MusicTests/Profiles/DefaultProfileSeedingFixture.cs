using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Music.RootFolders;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.Profiles
{
    // The startup seeding of Music's profiles (Lidarr's defaults) against the real tables: the
    // service tests mock the repository, so they cannot see a profile that does not serialize.
    [TestFixture]
    public class DefaultAudioQualityProfileSeedingFixture : DbTest<AudioQualityProfileService, AudioQualityProfile>
    {
        [SetUp]
        public void Setup()
        {
            // The service takes the repository interface: without this it gets a mock, not the table.
            Mocker.SetConstant<IAudioQualityProfileRepository>(Mocker.Resolve<AudioQualityProfileRepository>());
            Mocker.GetMock<ICustomFormatService>().Setup(s => s.All()).Returns(new List<CustomFormat>());
            Mocker.GetMock<IArtistService>().Setup(s => s.GetAllArtists()).Returns(new List<Artist>());
            Mocker.GetMock<IRootFolderService>().Setup(s => s.All()).Returns(new List<RootFolder>());
        }

        private AudioQualityProfile Seeded(string name)
        {
            Subject.Handle(new ApplicationStartedEvent());

            return Subject.All().Single(p => p.Name == name);
        }

        private static List<AudioQuality> Allowed(AudioQualityProfile profile)
        {
            return profile.Items.Where(i => i.Allowed).SelectMany(i => i.GetQualities()).ToList();
        }

        [Test]
        public void should_seed_lidarrs_three_default_profiles_in_the_real_table()
        {
            Subject.Handle(new ApplicationStartedEvent());

            Subject.All().Select(p => p.Name).Should().Equal("Any", "Lossless", "Standard");
        }

        [Test]
        public void should_seed_nothing_when_a_profile_exists()
        {
            Subject.Add(MusicTestData.NewQualityProfile("Mine"));

            Subject.Handle(new ApplicationStartedEvent());

            Subject.All().Select(p => p.Name).Should().Equal("Mine");
        }

        [Test]
        public void should_seed_the_lossless_profile_with_the_lossless_formats_and_a_flac_cutoff()
        {
            var profile = Seeded("Lossless");

            // A quality group is allowed as a whole (Lidarr's GetDefaultProfile does the same), so the
            // lossless groups bring APE and WavPack in with FLAC and ALAC. No lossy format is allowed.
            Allowed(profile).Should().BeEquivalentTo(new[] { AudioQuality.FLAC, AudioQuality.ALAC, AudioQuality.APE, AudioQuality.WAVPACK, AudioQuality.FLAC_24, AudioQuality.ALAC_24 });
            Allowed(profile).Should().NotContain(new[] { AudioQuality.MP3_320, AudioQuality.AAC_320, AudioQuality.Unknown });
            profile.GetIndex(profile.Cutoff).Index.Should().Be(profile.GetIndex(AudioQuality.FLAC).Index);
        }

        [Test]
        public void should_seed_the_standard_profile_with_mp3_192_to_320_and_an_mp3_192_cutoff()
        {
            var profile = Seeded("Standard");

            // The MP3-192 to MP3-320 steps sit in groups that also hold the other lossy codecs of the
            // same bitrate, and a group is allowed as a whole. Lossless and lower bitrates stay out.
            Allowed(profile).Should().Contain(new[] { AudioQuality.MP3_192, AudioQuality.MP3_256, AudioQuality.MP3_320 });
            Allowed(profile).Should().NotContain(new[] { AudioQuality.FLAC, AudioQuality.ALAC, AudioQuality.MP3_128, AudioQuality.Unknown });
            profile.GetIndex(profile.Cutoff).Index.Should().Be(profile.GetIndex(AudioQuality.MP3_192).Index);
        }

        [Test]
        public void should_seed_the_any_profile_with_every_listed_quality_and_an_unknown_cutoff()
        {
            var profile = Seeded("Any");

            Allowed(profile).Should().Contain(new[] { AudioQuality.Unknown, AudioQuality.MP3_128, AudioQuality.AAC_320, AudioQuality.VORBIS_Q10, AudioQuality.WMA, AudioQuality.FLAC_24 });
            profile.GetIndex(profile.Cutoff).Index.Should().Be(profile.GetIndex(AudioQuality.Unknown).Index);
        }

        [Test]
        public void should_keep_a_group_id_of_1000_or_more_for_grouped_qualities()
        {
            var profile = Seeded("Standard");

            profile.Items.Where(i => i.Quality == null).Should().OnlyContain(i => i.Id >= 1000 && i.Items.Count > 1);
        }
    }

    [TestFixture]
    public class DefaultMetadataProfileSeedingFixture : DbTest<MetadataProfileService, MetadataProfile>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.SetConstant<IMetadataProfileRepository>(Mocker.Resolve<MetadataProfileRepository>());
            Mocker.GetMock<IArtistService>().Setup(s => s.GetAllArtists()).Returns(new List<Artist>());
            Mocker.GetMock<IRootFolderService>().Setup(s => s.All()).Returns(new List<RootFolder>());
        }

        [Test]
        public void should_seed_standard_and_none_profiles_in_the_real_table()
        {
            Subject.Handle(new ApplicationStartedEvent());

            Subject.All().Select(p => p.Name).Should().Equal("Standard", "None");
        }

        [Test]
        public void should_seed_standard_with_studio_albums_and_official_releases_only()
        {
            Subject.Handle(new ApplicationStartedEvent());

            var standard = Subject.All().Single(p => p.Name == "Standard");

            standard.PrimaryAlbumTypes.Where(t => t.Allowed).Select(t => t.PrimaryAlbumType).Should().Equal(PrimaryAlbumType.Album);
            standard.SecondaryAlbumTypes.Where(t => t.Allowed).Select(t => t.SecondaryAlbumType).Should().Equal(SecondaryAlbumType.Studio);
            standard.ReleaseStatuses.Where(t => t.Allowed).Select(t => t.ReleaseStatus).Should().Equal(ReleaseStatus.Official);
            standard.PrimaryAlbumTypes.Should().HaveCount(PrimaryAlbumType.All.Count);
        }

        [Test]
        public void should_seed_none_with_nothing_allowed()
        {
            Subject.Handle(new ApplicationStartedEvent());

            var none = Subject.All().Single(p => p.Name == MetadataProfileService.NONE_PROFILE_NAME);

            none.PrimaryAlbumTypes.Should().NotContain(t => t.Allowed);
            none.SecondaryAlbumTypes.Should().NotContain(t => t.Allowed);
            none.ReleaseStatuses.Should().NotContain(t => t.Allowed);
        }

        [Test]
        public void should_not_seed_twice()
        {
            Subject.Handle(new ApplicationStartedEvent());
            Subject.Handle(new ApplicationStartedEvent());

            Subject.All().Select(p => p.Name).Should().Equal("Standard", "None");
        }

        [Test]
        public void should_refuse_to_change_or_delete_the_none_profile()
        {
            Subject.Handle(new ApplicationStartedEvent());
            var none = Subject.All().Single(p => p.Name == MetadataProfileService.NONE_PROFILE_NAME);

            Assert.Throws<System.InvalidOperationException>(() => Subject.Update(none));
            Assert.Throws<MetadataProfileInUseException>(() => Subject.Delete(none.Id));
        }
    }
}
