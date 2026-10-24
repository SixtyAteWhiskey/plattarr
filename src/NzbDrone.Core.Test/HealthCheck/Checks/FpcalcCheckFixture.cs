using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.HealthCheck;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Health;
using NzbDrone.Core.Music.Parser;
using NzbDrone.Core.Test.Framework;
using MusicRootFolder = NzbDrone.Core.Music.RootFolders.RootFolder;
using MusicRootFolderService = NzbDrone.Core.Music.RootFolders.IRootFolderService;

namespace NzbDrone.Core.Test.HealthCheck.Checks
{
    [TestFixture]
    public class FpcalcCheckFixture : CoreTest<FpcalcCheck>
    {
        [SetUp]
        public void SetUp()
        {
            Mocker.GetMock<IConfigService>().SetupGet(s => s.AllowFingerprinting).Returns(AllowFingerprinting.NewFiles);
            Mocker.GetMock<IFingerprintingService>().Setup(s => s.IsSetup()).Returns(true);
            Mocker.GetMock<IFingerprintingService>().Setup(s => s.FpcalcVersion()).Returns(new Version("1.5.1"));
            Mocker.GetMock<IArtistService>().Setup(s => s.GetAllArtists()).Returns(new List<Artist> { new Artist() });
            Mocker.GetMock<MusicRootFolderService>().Setup(s => s.All()).Returns(new List<MusicRootFolder>());
        }

        private void GivenNoMusicLibrary()
        {
            Mocker.GetMock<IArtistService>().Setup(s => s.GetAllArtists()).Returns(new List<Artist>());
            Mocker.GetMock<MusicRootFolderService>().Setup(s => s.All()).Returns(new List<MusicRootFolder>());
        }

        [Test]
        public void should_pass_when_fpcalc_is_current()
        {
            Subject.Check().Type.Should().Be(HealthCheckResult.Ok);
        }

        [Test]
        public void should_warn_when_fpcalc_is_missing()
        {
            Mocker.GetMock<IFingerprintingService>().Setup(s => s.IsSetup()).Returns(false);

            var result = Subject.Check();

            result.Type.Should().Be(HealthCheckResult.Warning);
            result.Reason.Should().Be(HealthCheckReason.FpcalcMissing);
        }

        [TestCase("1.4.2")]
        [TestCase("1.3.0")]
        [TestCase(null)]
        public void should_warn_when_fpcalc_is_older_than_1_4_3(string version)
        {
            Mocker.GetMock<IFingerprintingService>().Setup(s => s.FpcalcVersion()).Returns(version == null ? null : new Version(version));

            var result = Subject.Check();

            result.Type.Should().Be(HealthCheckResult.Warning);
            result.Reason.Should().Be(HealthCheckReason.FpcalcUpgrade);
        }

        [Test]
        public void should_accept_exactly_1_4_3()
        {
            Mocker.GetMock<IFingerprintingService>().Setup(s => s.FpcalcVersion()).Returns(new Version("1.4.3"));

            Subject.Check().Type.Should().Be(HealthCheckResult.Ok);
        }

        [Test]
        public void should_pass_when_fingerprinting_is_off_even_without_fpcalc()
        {
            Mocker.GetMock<IConfigService>().SetupGet(s => s.AllowFingerprinting).Returns(AllowFingerprinting.Never);
            Mocker.GetMock<IFingerprintingService>().Setup(s => s.IsSetup()).Returns(false);

            Subject.Check().Type.Should().Be(HealthCheckResult.Ok);
        }

        [Test]
        public void should_pass_without_a_music_library_even_without_fpcalc()
        {
            GivenNoMusicLibrary();
            Mocker.GetMock<IFingerprintingService>().Setup(s => s.IsSetup()).Returns(false);

            Subject.Check().Type.Should().Be(HealthCheckResult.Ok);
        }

        [Test]
        public void should_warn_when_only_a_music_root_folder_exists()
        {
            GivenNoMusicLibrary();
            Mocker.GetMock<MusicRootFolderService>().Setup(s => s.All()).Returns(new List<MusicRootFolder> { new MusicRootFolder { Path = "/music" } });
            Mocker.GetMock<IFingerprintingService>().Setup(s => s.IsSetup()).Returns(false);

            Subject.Check().Reason.Should().Be(HealthCheckReason.FpcalcMissing);
        }
    }
}
