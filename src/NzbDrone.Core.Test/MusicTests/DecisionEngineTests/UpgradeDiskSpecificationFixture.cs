using System;
using System.Collections.Generic;
using FizzWare.NBuilder;
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
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.CustomFormats;
using NzbDrone.Core.Test.Framework;
using UpgradableSpecification = NzbDrone.Core.Music.DecisionEngine.Specifications.UpgradableSpecification;

namespace NzbDrone.Core.Test.MusicTests.DecisionEngineTests
{
    [TestFixture]

    public class UpgradeDiskSpecificationFixture : CoreTest<UpgradeDiskSpecification>
    {
        private RemoteAlbum _parseResultMulti;
        private RemoteAlbum _parseResultSingle;
        private TrackFile _firstFile;
        private TrackFile _secondFile;

        [SetUp]
        public void Setup()
        {
            Mocker.Resolve<UpgradableSpecification>();

            CustomFormatsTestHelpers.GivenCustomFormats();

            _firstFile = new TrackFile { Quality = new AudioQualityModel(AudioQuality.FLAC, new Revision(version: 2)), DateAdded = DateTime.Now };
            _secondFile = new TrackFile { Quality = new AudioQualityModel(AudioQuality.FLAC, new Revision(version: 2)), DateAdded = DateTime.Now };

            var singleAlbumList = new List<Album> { new Album { } };
            var doubleAlbumList = new List<Album> { new Album { }, new Album { }, new Album { } };

            var fakeArtist = Builder<Artist>.CreateNew()
                         .With(c => c.QualityProfile = new AudioQualityProfile
                         {
                             UpgradeAllowed = true,
                             Cutoff = AudioQuality.MP3_320.Id,
                             Items = MusicTestData.DefaultQualities(),
                             FormatItems = CustomFormatsTestHelpers.GetSampleFormatItems("None"),
                             MinFormatScore = 0,
                         })
                         .Build();

            Mocker.GetMock<ITrackService>()
                .Setup(c => c.TracksWithoutFiles(It.IsAny<int>()))
                .Returns(new List<Track>());

            Mocker.GetMock<IMediaFileService>()
                  .Setup(c => c.GetFilesByAlbum(It.IsAny<int>()))
                  .Returns(new List<TrackFile> { _firstFile, _secondFile });

            _parseResultMulti = new RemoteAlbum
            {
                Artist = fakeArtist,
                ParsedAlbumInfo = new ParsedAlbumInfo { Quality = new AudioQualityModel(AudioQuality.MP3_256, new Revision(version: 2)) },
                Albums = doubleAlbumList,
                CustomFormats = new List<CustomFormat>()
            };

            _parseResultSingle = new RemoteAlbum
            {
                Artist = fakeArtist,
                ParsedAlbumInfo = new ParsedAlbumInfo { Quality = new AudioQualityModel(AudioQuality.MP3_256, new Revision(version: 2)) },
                Albums = singleAlbumList,
                CustomFormats = new List<CustomFormat>()
            };

            Mocker.GetMock<IMusicCustomFormatCalculationService>()
                  .Setup(x => x.ParseCustomFormat(It.IsAny<TrackFile>()))
                  .Returns(new List<CustomFormat>());
        }

        private void WithFirstFileUpgradable()
        {
            _firstFile.Quality = new AudioQualityModel(AudioQuality.MP3_192);
        }

        private void WithSecondFileUpgradable()
        {
            _secondFile.Quality = new AudioQualityModel(AudioQuality.MP3_192);
        }

        [Test]
        public void should_return_true_if_album_has_no_existing_file()
        {
            Mocker.GetMock<IMediaFileService>()
                  .Setup(c => c.GetFilesByAlbum(It.IsAny<int>()))
                  .Returns(new List<TrackFile> { });

            Subject.IsSatisfiedBy(_parseResultSingle, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_true_if_track_is_missing()
        {
            Mocker.GetMock<ITrackService>()
                  .Setup(c => c.TracksWithoutFiles(It.IsAny<int>()))
                .Returns(new List<Track> { new Track() });

            Subject.IsSatisfiedBy(_parseResultSingle, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_only_query_db_for_missing_tracks_once()
        {
            Subject.IsSatisfiedBy(_parseResultSingle, new ReleaseDecisionInformation()).Accepted.Should().BeFalse();
            Subject.IsSatisfiedBy(_parseResultSingle, new ReleaseDecisionInformation()).Accepted.Should().BeFalse();
            Mocker.GetMock<ITrackService>()
                .Verify(c => c.TracksWithoutFiles(It.IsAny<int>()), Times.Once());
        }

        [Test]
        public void should_return_true_if_single_album_doesnt_exist_on_disk()
        {
            _parseResultSingle.Albums = new List<Album>();

            Subject.IsSatisfiedBy(_parseResultSingle, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_be_upgradable_if_all_files_are_upgradable()
        {
            WithFirstFileUpgradable();
            WithSecondFileUpgradable();
            Subject.IsSatisfiedBy(_parseResultSingle, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_not_be_upgradable_if_qualities_are_the_same()
        {
            _firstFile.Quality = new AudioQualityModel(AudioQuality.MP3_320);
            _secondFile.Quality = new AudioQualityModel(AudioQuality.MP3_320);
            _parseResultSingle.ParsedAlbumInfo.Quality = new AudioQualityModel(AudioQuality.MP3_320);
            var rejection = Subject.IsSatisfiedBy(_parseResultSingle, new ReleaseDecisionInformation());
            rejection.Accepted.Should().BeFalse();
            rejection.Reason.Should().Be(DownloadRejectionReason.DiskHigherPreference);
        }

        [Test]
        public void should_not_be_upgradable_if_all_tracks_are_not_upgradable()
        {
            var rejection = Subject.IsSatisfiedBy(_parseResultSingle, new ReleaseDecisionInformation());
            rejection.Accepted.Should().BeFalse();
            rejection.Reason.Should().Be(DownloadRejectionReason.DiskHigherPreference);
        }

        [Test]
        public void should_be_true_if_some_tracks_are_upgradable_and_none_are_downgrades()
        {
            WithFirstFileUpgradable();
            _parseResultSingle.ParsedAlbumInfo.Quality = _secondFile.Quality;
            Subject.IsSatisfiedBy(_parseResultSingle, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_be_false_if_some_tracks_are_upgradable_and_some_are_downgrades()
        {
            Mocker.GetMock<IMusicCustomFormatCalculationService>()
                  .Setup(s => s.ParseCustomFormat(It.IsAny<TrackFile>()))
                  .Returns(new List<CustomFormat>());

            WithFirstFileUpgradable();
            _parseResultSingle.ParsedAlbumInfo.Quality = new AudioQualityModel(AudioQuality.MP3_320);
            var rejection = Subject.IsSatisfiedBy(_parseResultSingle, new ReleaseDecisionInformation());
            rejection.Accepted.Should().BeFalse();
            rejection.Reason.Should().Be(DownloadRejectionReason.DiskHigherPreference);
        }
    }
}
