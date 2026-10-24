using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.CustomFormats;
using NzbDrone.Core.Music.DecisionEngine.Specifications;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using UpgradableSpecification = NzbDrone.Core.Music.DecisionEngine.Specifications.UpgradableSpecification;

namespace NzbDrone.Core.Test.MusicTests.DecisionEngineTests
{
    // The decision specifications that sit on the UpgradableSpecification: what they return for an
    // album that has files, and which rejection reason the user sees.
    public abstract class ExistingFileSpecificationFixtureBase<TSpec> : CoreTest<TSpec>
        where TSpec : class
    {
        protected RemoteAlbum _remoteAlbum;
        protected AudioQualityProfile _profile;

        [SetUp]
        public void SetUpBase()
        {
            Mocker.Resolve<UpgradableSpecification>();

            _profile = new AudioQualityProfile
            {
                UpgradeAllowed = true,
                Cutoff = AudioQuality.MP3_320.Id,
                Items = MusicTestData.DefaultQualities()
            };

            var artist = new Artist { Id = 1, QualityProfile = _profile };

            _remoteAlbum = new RemoteAlbum
            {
                Artist = artist,
                Albums = new List<Album> { new Album { Id = 5 } },
                ParsedAlbumInfo = new ParsedAlbumInfo { Quality = new AudioQualityModel(AudioQuality.FLAC) },
                Release = new ReleaseInfo { Title = "Artist - Album [FLAC]" },
                CustomFormats = new List<CustomFormat>()
            };

            Mocker.GetMock<ITrackService>()
                  .Setup(c => c.TracksWithoutFiles(It.IsAny<int>()))
                  .Returns(new List<Track>());

            Mocker.GetMock<IMusicCustomFormatCalculationService>()
                  .Setup(x => x.ParseCustomFormat(It.IsAny<TrackFile>()))
                  .Returns(new List<CustomFormat>());

            Mocker.GetMock<IMusicCustomFormatCalculationService>()
                  .Setup(x => x.ParseCustomFormat(It.IsAny<TrackFile>(), It.IsAny<Artist>()))
                  .Returns(new List<CustomFormat>());
        }

        protected void GivenExistingFile(AudioQuality quality)
        {
            Mocker.GetMock<IMediaFileService>()
                  .Setup(c => c.GetFilesByAlbum(It.IsAny<int>()))
                  .Returns(new List<TrackFile> { new TrackFile { Quality = new AudioQualityModel(quality), DateAdded = DateTime.UtcNow } });
        }

        protected void GivenNoFiles()
        {
            Mocker.GetMock<IMediaFileService>()
                  .Setup(c => c.GetFilesByAlbum(It.IsAny<int>()))
                  .Returns(new List<TrackFile>());
        }
    }

    [TestFixture]
    public class CutoffDecisionSpecificationFixture : ExistingFileSpecificationFixtureBase<CutoffSpecification>
    {
        [Test]
        public void should_accept_an_album_without_files()
        {
            GivenNoFiles();

            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_when_the_files_are_below_the_cutoff()
        {
            GivenExistingFile(AudioQuality.MP3_192);

            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_with_the_cutoff_reason_when_the_files_meet_the_cutoff()
        {
            GivenExistingFile(AudioQuality.FLAC);

            var decision = Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation());

            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(DownloadRejectionReason.DiskCutoffMet);
        }

        [Test]
        public void should_accept_when_a_track_has_no_file_yet()
        {
            GivenExistingFile(AudioQuality.FLAC);

            Mocker.GetMock<ITrackService>()
                  .Setup(c => c.TracksWithoutFiles(It.IsAny<int>()))
                  .Returns(new List<Track> { new Track() });

            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }
    }

    [TestFixture]
    public class UpgradeAllowedDecisionSpecificationFixture : ExistingFileSpecificationFixtureBase<UpgradeAllowedSpecification>
    {
        [Test]
        public void should_accept_an_album_without_files()
        {
            GivenNoFiles();
            _profile.UpgradeAllowed = false;

            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_an_upgrade_when_the_profile_allows_upgrades()
        {
            GivenExistingFile(AudioQuality.MP3_192);

            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_with_the_upgrades_disabled_reason_when_the_profile_does_not_allow_upgrades()
        {
            GivenExistingFile(AudioQuality.MP3_192);
            _profile.UpgradeAllowed = false;

            var decision = Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation());

            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(DownloadRejectionReason.QualityUpgradesDisabled);
        }
    }
}
