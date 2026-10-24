using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.DecisionEngine.Specifications;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.DecisionEngineTests
{
    [TestFixture]

    public class QualityAllowedByProfileSpecificationFixture : CoreTest<QualityAllowedByProfileSpecification>
    {
        private RemoteAlbum _remoteAlbum;

        public static object[] AllowedTestCases =
        {
            new object[] { AudioQuality.MP3_192 },
            new object[] { AudioQuality.MP3_256 },
            new object[] { AudioQuality.MP3_320 }
        };

        public static object[] DeniedTestCases =
        {
            new object[] { AudioQuality.MP3_VBR },
            new object[] { AudioQuality.FLAC },
            new object[] { AudioQuality.Unknown }
        };

        [SetUp]
        public void Setup()
        {
            var fakeArtist = Builder<Artist>.CreateNew()
                         .With(c => c.QualityProfile = new AudioQualityProfile { Cutoff = AudioQuality.MP3_320.Id })
                         .Build();

            _remoteAlbum = new RemoteAlbum
            {
                Artist = fakeArtist,
                ParsedAlbumInfo = new ParsedAlbumInfo { Quality = new AudioQualityModel(AudioQuality.MP3_192, new Revision(version: 2)) },
            };
        }

        [Test]
        [TestCaseSource(nameof(AllowedTestCases))]
        public void should_allow_if_quality_is_defined_in_profile(AudioQuality qualityType)
        {
            _remoteAlbum.ParsedAlbumInfo.Quality.Quality = qualityType;
            _remoteAlbum.Artist.QualityProfile.Value.Items = MusicTestData.DefaultQualities(AudioQuality.MP3_192, AudioQuality.MP3_256, AudioQuality.MP3_320);

            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        [TestCaseSource(nameof(DeniedTestCases))]
        public void should_not_allow_if_quality_is_not_defined_in_profile(AudioQuality qualityType)
        {
            _remoteAlbum.ParsedAlbumInfo.Quality.Quality = qualityType;
            _remoteAlbum.Artist.QualityProfile.Value.Items = MusicTestData.DefaultQualities(AudioQuality.MP3_192, AudioQuality.MP3_256, AudioQuality.MP3_320);

            var rejection = Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation());

            rejection.Accepted.Should().BeFalse();

            rejection.Reason.Should().Be(DownloadRejectionReason.QualityNotWanted);
        }
    }
}
