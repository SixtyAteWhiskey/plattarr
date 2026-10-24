using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
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
    [TestFixture]

    public class AudioQualityProfileServiceFixture : CoreTest<AudioQualityProfileService>
    {
        [Test]
        public void init_should_add_default_profiles()
        {
            Mocker.GetMock<ICustomFormatService>()
                .Setup(s => s.All())
                .Returns(new List<CustomFormat>());

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IAudioQualityProfileRepository>()
                .Verify(v => v.Insert(It.IsAny<AudioQualityProfile>()), Times.Exactly(3));
        }

        [Test]

        // This confirms that new profiles are added only if no other profiles exists.
        // We don't want to keep adding them back if a user deleted them on purpose.
        public void Init_should_skip_if_any_profiles_already_exist()
        {
            Mocker.GetMock<IAudioQualityProfileRepository>()
                  .Setup(s => s.All())
                  .Returns(Builder<AudioQualityProfile>.CreateListOfSize(2).Build().ToList());

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IAudioQualityProfileRepository>()
                .Verify(v => v.Insert(It.IsAny<AudioQualityProfile>()), Times.Never());
        }

        [Test]
        public void should_not_be_able_to_delete_profile_if_assigned_to_artist()
        {
            var profile = Builder<AudioQualityProfile>.CreateNew()
                                          .With(p => p.Id = 2)
                                          .Build();

            var artistList = Builder<Artist>.CreateListOfSize(3)
                                            .Random(1)
                                            .With(c => c.QualityProfileId = profile.Id)
                                            .Build().ToList();

            var rootFolders = Builder<RootFolder>.CreateListOfSize(2)
                .All()
                .With(f => f.DefaultQualityProfileId = 1)
                .BuildList();

            Mocker.GetMock<IArtistService>().Setup(c => c.GetAllArtists()).Returns(artistList);
            Mocker.GetMock<IRootFolderService>().Setup(c => c.All()).Returns(rootFolders);
            Mocker.GetMock<IAudioQualityProfileRepository>().Setup(c => c.Get(profile.Id)).Returns(profile);

            Assert.Throws<AudioQualityProfileInUseException>(() => Subject.Delete(profile.Id));

            Mocker.GetMock<IAudioQualityProfileRepository>().Verify(c => c.Delete(It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_not_be_able_to_delete_profile_if_assigned_to_root_folder()
        {
            var profile = Builder<AudioQualityProfile>.CreateNew()
                .With(p => p.Id = 2)
                .Build();

            var artistList = Builder<Artist>.CreateListOfSize(3)
                .All()
                .With(c => c.QualityProfileId = 1)
                .Build().ToList();

            var rootFolders = Builder<RootFolder>.CreateListOfSize(2)
                .Random(1)
                .With(f => f.DefaultQualityProfileId = profile.Id)
                .BuildList();

            Mocker.GetMock<IArtistService>().Setup(c => c.GetAllArtists()).Returns(artistList);
            Mocker.GetMock<IRootFolderService>().Setup(c => c.All()).Returns(rootFolders);
            Mocker.GetMock<IAudioQualityProfileRepository>().Setup(c => c.Get(profile.Id)).Returns(profile);

            Assert.Throws<AudioQualityProfileInUseException>(() => Subject.Delete(profile.Id));

            Mocker.GetMock<IAudioQualityProfileRepository>().Verify(c => c.Delete(It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_delete_profile_if_not_assigned_to_artist_or_root_folder()
        {
            var artistList = Builder<Artist>.CreateListOfSize(3)
                                            .All()
                                            .With(c => c.QualityProfileId = 2)
                                            .Build().ToList();

            var rootFolders = Builder<RootFolder>.CreateListOfSize(2)
                .All()
                .With(f => f.DefaultQualityProfileId = 2)
                .BuildList();

            Mocker.GetMock<IArtistService>().Setup(c => c.GetAllArtists()).Returns(artistList);
            Mocker.GetMock<IRootFolderService>().Setup(c => c.All()).Returns(rootFolders);

            Subject.Delete(1);

            Mocker.GetMock<IAudioQualityProfileRepository>().Verify(c => c.Delete(1), Times.Once());
        }

        private AudioQualityProfile GivenDefaultProfile(AudioQuality cutoff, params AudioQuality[] allowed)
        {
            Mocker.GetMock<ICustomFormatService>()
                .Setup(s => s.All())
                .Returns(new List<CustomFormat>());

            return Subject.GetDefaultProfile("Test", cutoff, allowed);
        }

        [Test]
        public void default_profile_should_allow_the_whole_group_of_an_allowed_quality()
        {
            var profile = GivenDefaultProfile(AudioQuality.FLAC, AudioQuality.FLAC);

            var lossless = profile.Items.Single(i => i.Name == "Lossless");

            lossless.Allowed.Should().BeTrue();
            lossless.Items.Should().OnlyContain(i => i.Allowed);
            lossless.Items.Select(i => i.Quality.Id).Should().Contain(new[] { AudioQuality.ALAC.Id, AudioQuality.APE.Id, AudioQuality.WAVPACK.Id });
        }

        [Test]
        public void default_profile_should_not_allow_groups_without_an_allowed_quality()
        {
            var profile = GivenDefaultProfile(AudioQuality.FLAC, AudioQuality.FLAC);

            profile.Items.Single(i => i.Name == "Trash Quality Lossy").Allowed.Should().BeFalse();
            profile.Items.Single(i => i.Quality == AudioQuality.WAV).Allowed.Should().BeFalse();
            profile.Items.Single(i => i.Quality == AudioQuality.Unknown).Allowed.Should().BeFalse();
        }

        [Test]
        public void default_profile_should_point_the_cutoff_at_the_group_that_holds_the_cutoff_quality()
        {
            var profile = GivenDefaultProfile(AudioQuality.MP3_192, AudioQuality.MP3_192);

            var group = profile.Items.Single(i => i.Name == "Low Quality Lossy");

            group.Id.Should().BeGreaterOrEqualTo(1000);
            profile.Cutoff.Should().Be(group.Id);
        }

        [Test]
        public void default_profile_should_use_the_quality_id_for_the_cutoff_of_an_ungrouped_quality()
        {
            GivenDefaultProfile(AudioQuality.WAV, AudioQuality.WAV).Cutoff.Should().Be(AudioQuality.WAV.Id);
        }

        [Test]
        public void default_profile_should_cut_off_at_unknown_when_no_cutoff_is_given()
        {
            GivenDefaultProfile(null, AudioQuality.FLAC).Cutoff.Should().Be(AudioQuality.Unknown.Id);
        }

        [Test]
        public void default_profile_groups_should_get_distinct_ids()
        {
            var profile = GivenDefaultProfile(AudioQuality.FLAC, AudioQuality.FLAC);

            var ids = profile.Items.Where(i => i.Quality == null).Select(i => i.Id).ToList();

            ids.Should().OnlyHaveUniqueItems();
            ids.Should().NotBeEmpty();
        }
    }
}
