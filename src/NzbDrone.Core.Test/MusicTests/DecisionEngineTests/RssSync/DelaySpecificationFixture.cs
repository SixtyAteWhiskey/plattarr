using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.DecisionEngine.Specifications.RssSync;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Pending;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Music.Search;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.DecisionEngineTests.RssSync
{
    [TestFixture]
    public class DelaySpecificationFixture : CoreTest<DelaySpecification>
    {
        private AudioQualityProfile _profile;
        private DelayProfile _delayProfile;
        private RemoteAlbum _remoteAlbum;

        [SetUp]
        public void Setup()
        {
            _profile = Builder<AudioQualityProfile>.CreateNew()
                                       .Build();

            _delayProfile = new DelayProfile { PreferredProtocol = DownloadProtocol.Usenet };

            var artist = Builder<Artist>.CreateNew()
                                        .With(s => s.QualityProfile = _profile)
                                        .Build();

            _remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                                                   .With(r => r.Artist = artist)
                                                   .Build();

            _profile.Items = new List<AudioQualityProfileItem>();
            _profile.Items.Add(new AudioQualityProfileItem { Allowed = true, Quality = AudioQuality.MP3_256 });
            _profile.Items.Add(new AudioQualityProfileItem { Allowed = true, Quality = AudioQuality.MP3_320 });
            _profile.Items.Add(new AudioQualityProfileItem { Allowed = true, Quality = AudioQuality.FLAC });

            _profile.Cutoff = AudioQuality.MP3_320.Id;

            _remoteAlbum.ParsedAlbumInfo = new ParsedAlbumInfo();
            _remoteAlbum.Release = new ReleaseInfo();
            _remoteAlbum.Release.DownloadProtocol = DownloadProtocol.Usenet;

            _remoteAlbum.Albums = Builder<Album>.CreateListOfSize(1).Build().ToList();

            Mocker.GetMock<IMediaFileService>()
                .Setup(s => s.GetFilesByAlbum(It.IsAny<int>()))
                .Returns(new List<TrackFile> { });

            Mocker.GetMock<IDelayProfileService>()
                  .Setup(s => s.BestForTags(It.IsAny<HashSet<int>>()))
                  .Returns(_delayProfile);

            Mocker.GetMock<IPendingReleaseService>()
                  .Setup(s => s.GetPendingRemoteAlbums(It.IsAny<int>()))
                  .Returns(new List<RemoteAlbum>());
        }

        private void GivenExistingFile(AudioQualityModel quality)
        {
            Mocker.GetMock<IMediaFileService>()
                .Setup(s => s.GetFilesByAlbum(It.IsAny<int>()))
                .Returns(new List<TrackFile>
                {
                    new TrackFile
                {
                                                                Quality = quality
                }
                });
        }

        [Test]
        public void should_be_true_when_user_invoked_search()
        {
            Subject.IsSatisfiedBy(new RemoteAlbum(), new ReleaseDecisionInformation(false, new AlbumSearchRequest { UserInvokedSearch = true })).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_be_false_when_system_invoked_search_and_release_is_younger_than_delay()
        {
            _remoteAlbum.ParsedAlbumInfo.Quality = new AudioQualityModel(AudioQuality.MP3_192);
            _remoteAlbum.Release.PublishDate = DateTime.UtcNow;

            _delayProfile.UsenetDelay = 720;

            var rejection = Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation(false, new AlbumSearchRequest()));

            rejection.Accepted.Should().BeFalse();

            rejection.Reason.Should().Be(DownloadRejectionReason.MinimumAgeDelay);
        }

        [Test]
        public void should_be_true_when_profile_does_not_have_a_delay()
        {
            _delayProfile.UsenetDelay = 0;

            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_be_false_when_quality_is_last_allowed_in_profile_and_bypass_disabled()
        {
            _remoteAlbum.Release.PublishDate = DateTime.UtcNow;
            _remoteAlbum.ParsedAlbumInfo.Quality = new AudioQualityModel(AudioQuality.MP3_320);

            _delayProfile.UsenetDelay = 720;

            var rejection = Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation());

            rejection.Accepted.Should().BeFalse();

            rejection.Reason.Should().Be(DownloadRejectionReason.MinimumAgeDelay);
        }

        [Test]
        public void should_be_true_when_quality_is_last_allowed_in_profile_and_bypass_enabled()
        {
            _delayProfile.UsenetDelay = 720;
            _delayProfile.BypassIfHighestQuality = true;

            _remoteAlbum.Release.PublishDate = DateTime.UtcNow;
            _remoteAlbum.ParsedAlbumInfo.Quality = new AudioQualityModel(AudioQuality.FLAC);

            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_be_true_when_release_is_older_than_delay()
        {
            _remoteAlbum.ParsedAlbumInfo.Quality = new AudioQualityModel(AudioQuality.MP3_256);
            _remoteAlbum.Release.PublishDate = DateTime.UtcNow.AddHours(-10);

            _delayProfile.UsenetDelay = 60;

            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_be_false_when_release_is_younger_than_delay()
        {
            _remoteAlbum.ParsedAlbumInfo.Quality = new AudioQualityModel(AudioQuality.MP3_192);
            _remoteAlbum.Release.PublishDate = DateTime.UtcNow;

            _delayProfile.UsenetDelay = 720;

            var rejection = Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation());

            rejection.Accepted.Should().BeFalse();

            rejection.Reason.Should().Be(DownloadRejectionReason.MinimumAgeDelay);
        }

        [Test]
        public void should_be_true_when_release_is_a_proper_for_existing_album()
        {
            _remoteAlbum.ParsedAlbumInfo.Quality = new AudioQualityModel(AudioQuality.MP3_256, new Revision(version: 2));
            _remoteAlbum.Release.PublishDate = DateTime.UtcNow;

            GivenExistingFile(new AudioQualityModel(AudioQuality.MP3_256));

            _delayProfile.UsenetDelay = 720;

            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_be_true_when_release_is_a_real_for_existing_album()
        {
            _remoteAlbum.ParsedAlbumInfo.Quality = new AudioQualityModel(AudioQuality.MP3_256, new Revision(real: 1));
            _remoteAlbum.Release.PublishDate = DateTime.UtcNow;

            GivenExistingFile(new AudioQualityModel(AudioQuality.MP3_256));

            _delayProfile.UsenetDelay = 720;

            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_be_false_when_release_is_proper_for_existing_album_of_different_quality()
        {
            _remoteAlbum.ParsedAlbumInfo.Quality = new AudioQualityModel(AudioQuality.MP3_320, new Revision(version: 2));
            _remoteAlbum.Release.PublishDate = DateTime.UtcNow;

            GivenExistingFile(new AudioQualityModel(AudioQuality.MP3_256));

            _delayProfile.UsenetDelay = 720;

            var rejection = Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation());

            rejection.Accepted.Should().BeFalse();

            rejection.Reason.Should().Be(DownloadRejectionReason.MinimumAgeDelay);
        }

        [Test]
        public void should_be_false_when_custom_format_score_is_above_minimum_but_bypass_disabled()
        {
            _remoteAlbum.Release.PublishDate = DateTime.UtcNow;
            _remoteAlbum.CustomFormatScore = 100;

            _delayProfile.UsenetDelay = 720;
            _delayProfile.MinimumCustomFormatScore = 50;

            var rejection = Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation());

            rejection.Accepted.Should().BeFalse();

            rejection.Reason.Should().Be(DownloadRejectionReason.MinimumAgeDelay);
        }

        [Test]
        public void should_be_false_when_custom_format_score_is_above_minimum_and_bypass_enabled_but_under_minimum()
        {
            _remoteAlbum.Release.PublishDate = DateTime.UtcNow;
            _remoteAlbum.CustomFormatScore = 5;

            _delayProfile.UsenetDelay = 720;
            _delayProfile.BypassIfAboveCustomFormatScore = true;
            _delayProfile.MinimumCustomFormatScore = 50;

            var rejection = Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation());

            rejection.Accepted.Should().BeFalse();

            rejection.Reason.Should().Be(DownloadRejectionReason.MinimumAgeDelay);
        }

        [Test]
        public void should_be_true_when_custom_format_score_is_above_minimum_and_bypass_enabled()
        {
            _remoteAlbum.Release.PublishDate = DateTime.UtcNow;
            _remoteAlbum.CustomFormatScore = 100;

            _delayProfile.UsenetDelay = 720;
            _delayProfile.BypassIfAboveCustomFormatScore = true;
            _delayProfile.MinimumCustomFormatScore = 50;

            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }
    }
}
