using System;
using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.DecisionEngine.Specifications.RssSync;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Music.Search;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MusicTests.DecisionEngineTests.RssSync
{
    [TestFixture]
    public class DeletedTrackFileSpecificationFixture : CoreTest<DeletedTrackFileSpecification>
    {
        private RemoteAlbum _parseResultMulti;
        private RemoteAlbum _parseResultSingle;
        private TrackFile _firstFile;
        private TrackFile _secondFile;

        [SetUp]
        public void Setup()
        {
            _firstFile =
                new TrackFile
                {
                    Id = 1,
                    Path = "/My.Artist.S01E01.mp3",
                    Quality = new AudioQualityModel(AudioQuality.FLAC, new Revision(version: 1)),
                    DateAdded = DateTime.Now,
                    AlbumId = 1
                };
            _secondFile =
                new TrackFile
                {
                    Id = 2,
                    Path = "/My.Artist.S01E02.mp3",
                    Quality = new AudioQualityModel(AudioQuality.FLAC, new Revision(version: 1)),
                    DateAdded = DateTime.Now,
                    AlbumId = 2
                };

            var singleAlbumList = new List<Album> { new Album { Id = 1 } };
            var doubleAlbumList = new List<Album>
            {
                new Album { Id = 1 },
                new Album { Id = 2 }
            };

            var firstTrack = new Track { TrackFile = _firstFile, TrackFileId = 1, AlbumId = 1 };
            var secondTrack = new Track { TrackFile = _secondFile, TrackFileId = 2, AlbumId = 2 };

            var fakeArtist = Builder<Artist>.CreateNew()
                         .With(c => c.QualityProfile = new AudioQualityProfile { Cutoff = AudioQuality.FLAC.Id })
                         .With(c => c.Path = @"C:\Music\My.Artist".AsOsAgnostic())
                         .Build();

            _parseResultMulti = new RemoteAlbum
            {
                Artist = fakeArtist,
                ParsedAlbumInfo = new ParsedAlbumInfo { Quality = new AudioQualityModel(AudioQuality.MP3_256, new Revision(version: 2)) },
                Albums = doubleAlbumList
            };

            _parseResultSingle = new RemoteAlbum
            {
                Artist = fakeArtist,
                ParsedAlbumInfo = new ParsedAlbumInfo { Quality = new AudioQualityModel(AudioQuality.MP3_256, new Revision(version: 2)) },
                Albums = singleAlbumList
            };

            GivenUnmonitorDeletedTracks(true);
        }

        private void GivenUnmonitorDeletedTracks(bool enabled)
        {
            Mocker.GetMock<IConfigService>()
                  .SetupGet(v => v.AutoUnmonitorPreviouslyDownloadedTracks)
                  .Returns(enabled);
        }

        private void SetupMediaFile(List<TrackFile> files)
        {
            Mocker.GetMock<IMediaFileService>()
                              .Setup(v => v.GetFilesByAlbum(It.IsAny<int>()))
                              .Returns(files);
        }

        private void WithExistingFile(TrackFile trackFile)
        {
            var path = trackFile.Path;

            Mocker.GetMock<IDiskProvider>()
                  .Setup(v => v.FileExists(path))
                  .Returns(true);
        }

        [Test]
        public void should_return_true_when_unmonitor_deleted_tracks_is_off()
        {
            GivenUnmonitorDeletedTracks(false);

            Subject.IsSatisfiedBy(_parseResultSingle, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_true_when_searching()
        {
            Subject.IsSatisfiedBy(_parseResultSingle, new ReleaseDecisionInformation(false, new ArtistSearchRequest())).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_true_if_file_exists()
        {
            WithExistingFile(_firstFile);
            SetupMediaFile(new List<TrackFile> { _firstFile });

            Subject.IsSatisfiedBy(_parseResultSingle, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_false_if_file_is_missing()
        {
            SetupMediaFile(new List<TrackFile> { _firstFile });
            var rejection = Subject.IsSatisfiedBy(_parseResultSingle, new ReleaseDecisionInformation());
            rejection.Accepted.Should().BeFalse();
            rejection.Reason.Should().Be(DownloadRejectionReason.ArtistNotMonitored);
        }

        [Test]
        public void should_return_true_if_both_of_multiple_episode_exist()
        {
            WithExistingFile(_firstFile);
            WithExistingFile(_secondFile);
            SetupMediaFile(new List<TrackFile> { _firstFile, _secondFile });

            Subject.IsSatisfiedBy(_parseResultMulti, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_false_if_one_of_multiple_episode_is_missing()
        {
            WithExistingFile(_firstFile);
            SetupMediaFile(new List<TrackFile> { _firstFile, _secondFile });

            var rejection = Subject.IsSatisfiedBy(_parseResultMulti, new ReleaseDecisionInformation());

            rejection.Accepted.Should().BeFalse();

            rejection.Reason.Should().Be(DownloadRejectionReason.ArtistNotMonitored);
        }
    }
}
