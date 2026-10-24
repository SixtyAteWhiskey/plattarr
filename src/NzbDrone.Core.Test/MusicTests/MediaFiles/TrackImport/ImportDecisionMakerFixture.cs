using System.Collections.Generic;
using System.IO;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.History;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.MediaFiles.TrackImport;
using NzbDrone.Core.Music.MediaFiles.TrackImport.Aggregation;
using NzbDrone.Core.Music.MediaFiles.TrackImport.Identification;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MusicTests.MediaFiles.TrackImport
{
    [TestFixture]
    public class ImportDecisionMakerFixture : FileSystemTest<ImportDecisionMaker>
    {
        private List<FileInfo> _fileInfos;
        private LocalTrack _localTrack;
        private Artist _artist;
        private Album _album;
        private AlbumRelease _albumRelease;
        private AudioQualityModel _quality;

        private IdentificationOverrides _idOverrides;
        private ImportDecisionMakerConfig _idConfig;

        private Mock<IImportDecisionEngineSpecification<LocalAlbumRelease>> _albumpass1;
        private Mock<IImportDecisionEngineSpecification<LocalAlbumRelease>> _albumpass2;
        private Mock<IImportDecisionEngineSpecification<LocalAlbumRelease>> _albumpass3;

        private Mock<IImportDecisionEngineSpecification<LocalAlbumRelease>> _albumfail1;
        private Mock<IImportDecisionEngineSpecification<LocalAlbumRelease>> _albumfail2;
        private Mock<IImportDecisionEngineSpecification<LocalAlbumRelease>> _albumfail3;

        private Mock<IImportDecisionEngineSpecification<LocalTrack>> _pass1;
        private Mock<IImportDecisionEngineSpecification<LocalTrack>> _pass2;
        private Mock<IImportDecisionEngineSpecification<LocalTrack>> _pass3;

        private Mock<IImportDecisionEngineSpecification<LocalTrack>> _fail1;
        private Mock<IImportDecisionEngineSpecification<LocalTrack>> _fail2;
        private Mock<IImportDecisionEngineSpecification<LocalTrack>> _fail3;

        [SetUp]
        public void Setup()
        {
            _albumpass1 = new Mock<IImportDecisionEngineSpecification<LocalAlbumRelease>>();
            _albumpass2 = new Mock<IImportDecisionEngineSpecification<LocalAlbumRelease>>();
            _albumpass3 = new Mock<IImportDecisionEngineSpecification<LocalAlbumRelease>>();

            _albumfail1 = new Mock<IImportDecisionEngineSpecification<LocalAlbumRelease>>();
            _albumfail2 = new Mock<IImportDecisionEngineSpecification<LocalAlbumRelease>>();
            _albumfail3 = new Mock<IImportDecisionEngineSpecification<LocalAlbumRelease>>();

            _pass1 = new Mock<IImportDecisionEngineSpecification<LocalTrack>>();
            _pass2 = new Mock<IImportDecisionEngineSpecification<LocalTrack>>();
            _pass3 = new Mock<IImportDecisionEngineSpecification<LocalTrack>>();

            _fail1 = new Mock<IImportDecisionEngineSpecification<LocalTrack>>();
            _fail2 = new Mock<IImportDecisionEngineSpecification<LocalTrack>>();
            _fail3 = new Mock<IImportDecisionEngineSpecification<LocalTrack>>();

            _albumpass1.Setup(c => c.IsSatisfiedBy(It.IsAny<LocalAlbumRelease>(), It.IsAny<DownloadClientItem>())).Returns(ImportSpecDecision.Accept());
            _albumpass2.Setup(c => c.IsSatisfiedBy(It.IsAny<LocalAlbumRelease>(), It.IsAny<DownloadClientItem>())).Returns(ImportSpecDecision.Accept());
            _albumpass3.Setup(c => c.IsSatisfiedBy(It.IsAny<LocalAlbumRelease>(), It.IsAny<DownloadClientItem>())).Returns(ImportSpecDecision.Accept());

            _albumfail1.Setup(c => c.IsSatisfiedBy(It.IsAny<LocalAlbumRelease>(), It.IsAny<DownloadClientItem>())).Returns(ImportSpecDecision.Reject(ImportRejectionReason.Unknown, "_albumfail1"));
            _albumfail2.Setup(c => c.IsSatisfiedBy(It.IsAny<LocalAlbumRelease>(), It.IsAny<DownloadClientItem>())).Returns(ImportSpecDecision.Reject(ImportRejectionReason.Unknown, "_albumfail2"));
            _albumfail3.Setup(c => c.IsSatisfiedBy(It.IsAny<LocalAlbumRelease>(), It.IsAny<DownloadClientItem>())).Returns(ImportSpecDecision.Reject(ImportRejectionReason.Unknown, "_albumfail3"));

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<LocalTrack>(), It.IsAny<DownloadClientItem>())).Returns(ImportSpecDecision.Accept());
            _pass2.Setup(c => c.IsSatisfiedBy(It.IsAny<LocalTrack>(), It.IsAny<DownloadClientItem>())).Returns(ImportSpecDecision.Accept());
            _pass3.Setup(c => c.IsSatisfiedBy(It.IsAny<LocalTrack>(), It.IsAny<DownloadClientItem>())).Returns(ImportSpecDecision.Accept());

            _fail1.Setup(c => c.IsSatisfiedBy(It.IsAny<LocalTrack>(), It.IsAny<DownloadClientItem>())).Returns(ImportSpecDecision.Reject(ImportRejectionReason.Unknown, "_fail1"));
            _fail2.Setup(c => c.IsSatisfiedBy(It.IsAny<LocalTrack>(), It.IsAny<DownloadClientItem>())).Returns(ImportSpecDecision.Reject(ImportRejectionReason.Unknown, "_fail2"));
            _fail3.Setup(c => c.IsSatisfiedBy(It.IsAny<LocalTrack>(), It.IsAny<DownloadClientItem>())).Returns(ImportSpecDecision.Reject(ImportRejectionReason.Unknown, "_fail3"));

            _artist = Builder<Artist>.CreateNew()
                .With(e => e.QualityProfileId = 1)
                .With(e => e.QualityProfile = new AudioQualityProfile { Items = MusicTests.MusicTestData.DefaultQualities() })
                .Build();

            _album = Builder<Album>.CreateNew()
                .With(x => x.Artist = _artist)
                .Build();

            _albumRelease = Builder<AlbumRelease>.CreateNew()
                .With(x => x.Album = _album)
                .Build();

            _quality = new AudioQualityModel(AudioQuality.MP3_256);

            _localTrack = new LocalTrack
            {
                Artist = _artist,
                Quality = _quality,
                Tracks = new List<Track> { new Track() },
                Path = Tmp(@"C:\Test\Unsorted\The.Office.S03E115.DVDRip.XviD-OSiTV.avi")
            };

            _idOverrides = new IdentificationOverrides
            {
                Artist = _artist
            };

            _idConfig = new ImportDecisionMakerConfig();

            GivenAudioFiles(new List<string> { Tmp(@"C:\Test\Unsorted\The.Office.S03E115.DVDRip.XviD-OSiTV.avi") });

            Mocker.GetMock<IIdentificationService>()
                .Setup(s => s.Identify(It.IsAny<List<LocalTrack>>(), It.IsAny<IdentificationOverrides>(), It.IsAny<ImportDecisionMakerConfig>()))
                .Returns((List<LocalTrack> tracks, IdentificationOverrides idOverrides, ImportDecisionMakerConfig config) =>
                {
                    var ret = new LocalAlbumRelease(tracks);
                    ret.AlbumRelease = _albumRelease;
                    return new List<LocalAlbumRelease> { ret };
                });

            Mocker.GetMock<IMediaFileService>()
                .Setup(c => c.FilterUnchangedFiles(It.IsAny<List<FileInfo>>(), It.IsAny<FilterFilesType>()))
                .Returns((List<FileInfo> files, FilterFilesType filter) => files);

            Mocker.GetMock<IHistoryService>()
                .Setup(x => x.FindByDownloadId(It.IsAny<string>()))
                .Returns(new List<AlbumHistory>());

            GivenSpecifications(_albumpass1);
        }

        private void GivenSpecifications<T>(params Mock<IImportDecisionEngineSpecification<T>>[] mocks)
        {
            Mocker.SetConstant(mocks.Select(c => c.Object));
        }

        private void GivenAudioFiles(IEnumerable<string> videoFiles)
        {
            foreach (var file in videoFiles)
            {
                FileSystem.AddFile(file, new MockFileData(string.Empty));
            }

            _fileInfos = videoFiles.Select(x => new FileInfo(x)).ToList();
        }

        private void GivenAugmentationSuccess()
        {
            Mocker.GetMock<IAugmentingService>()
                  .Setup(s => s.Augment(It.IsAny<LocalTrack>(), It.IsAny<bool>()))
                  .Callback<LocalTrack, bool>((localTrack, otherFiles) =>
                  {
                      localTrack.Tracks = _localTrack.Tracks;
                  });
        }

        [Test]
        public void should_call_all_album_specifications()
        {
            var downloadClientItem = Builder<DownloadClientItem>.CreateNew().Build();
            var itemInfo = new ImportDecisionMakerInfo { DownloadClientItem = downloadClientItem };

            GivenAugmentationSuccess();
            GivenSpecifications(_albumpass1, _albumpass2, _albumpass3, _albumfail1, _albumfail2, _albumfail3);

            Subject.GetImportDecisions(_fileInfos, null, itemInfo, _idConfig);

            _albumfail1.Verify(c => c.IsSatisfiedBy(It.IsAny<LocalAlbumRelease>(), It.IsAny<DownloadClientItem>()), Times.Once());
            _albumfail2.Verify(c => c.IsSatisfiedBy(It.IsAny<LocalAlbumRelease>(), It.IsAny<DownloadClientItem>()), Times.Once());
            _albumfail3.Verify(c => c.IsSatisfiedBy(It.IsAny<LocalAlbumRelease>(), It.IsAny<DownloadClientItem>()), Times.Once());
            _albumpass1.Verify(c => c.IsSatisfiedBy(It.IsAny<LocalAlbumRelease>(), It.IsAny<DownloadClientItem>()), Times.Once());
            _albumpass2.Verify(c => c.IsSatisfiedBy(It.IsAny<LocalAlbumRelease>(), It.IsAny<DownloadClientItem>()), Times.Once());
            _albumpass3.Verify(c => c.IsSatisfiedBy(It.IsAny<LocalAlbumRelease>(), It.IsAny<DownloadClientItem>()), Times.Once());
        }

        [Test]
        public void should_call_all_track_specifications_if_album_accepted()
        {
            var downloadClientItem = Builder<DownloadClientItem>.CreateNew().Build();
            var itemInfo = new ImportDecisionMakerInfo { DownloadClientItem = downloadClientItem };

            GivenAugmentationSuccess();
            GivenSpecifications(_pass1, _pass2, _pass3, _fail1, _fail2, _fail3);

            Subject.GetImportDecisions(_fileInfos, null, itemInfo, _idConfig);

            _fail1.Verify(c => c.IsSatisfiedBy(It.IsAny<LocalTrack>(), It.IsAny<DownloadClientItem>()), Times.Once());
            _fail2.Verify(c => c.IsSatisfiedBy(It.IsAny<LocalTrack>(), It.IsAny<DownloadClientItem>()), Times.Once());
            _fail3.Verify(c => c.IsSatisfiedBy(It.IsAny<LocalTrack>(), It.IsAny<DownloadClientItem>()), Times.Once());
            _pass1.Verify(c => c.IsSatisfiedBy(It.IsAny<LocalTrack>(), It.IsAny<DownloadClientItem>()), Times.Once());
            _pass2.Verify(c => c.IsSatisfiedBy(It.IsAny<LocalTrack>(), It.IsAny<DownloadClientItem>()), Times.Once());
            _pass3.Verify(c => c.IsSatisfiedBy(It.IsAny<LocalTrack>(), It.IsAny<DownloadClientItem>()), Times.Once());
        }

        [Test]
        public void should_call_no_track_specifications_if_album_rejected()
        {
            var downloadClientItem = Builder<DownloadClientItem>.CreateNew().Build();
            var itemInfo = new ImportDecisionMakerInfo { DownloadClientItem = downloadClientItem };

            GivenAugmentationSuccess();
            GivenSpecifications(_albumpass1, _albumpass2, _albumpass3, _albumfail1, _albumfail2, _albumfail3);
            GivenSpecifications(_pass1, _pass2, _pass3, _fail1, _fail2, _fail3);

            Subject.GetImportDecisions(_fileInfos, null, itemInfo, _idConfig);

            _fail1.Verify(c => c.IsSatisfiedBy(It.IsAny<LocalTrack>(), It.IsAny<DownloadClientItem>()), Times.Never());
            _fail2.Verify(c => c.IsSatisfiedBy(It.IsAny<LocalTrack>(), It.IsAny<DownloadClientItem>()), Times.Never());
            _fail3.Verify(c => c.IsSatisfiedBy(It.IsAny<LocalTrack>(), It.IsAny<DownloadClientItem>()), Times.Never());
            _pass1.Verify(c => c.IsSatisfiedBy(It.IsAny<LocalTrack>(), It.IsAny<DownloadClientItem>()), Times.Never());
            _pass2.Verify(c => c.IsSatisfiedBy(It.IsAny<LocalTrack>(), It.IsAny<DownloadClientItem>()), Times.Never());
            _pass3.Verify(c => c.IsSatisfiedBy(It.IsAny<LocalTrack>(), It.IsAny<DownloadClientItem>()), Times.Never());
        }

        [Test]
        public void should_return_rejected_if_only_album_spec_fails()
        {
            GivenSpecifications(_albumfail1);
            GivenSpecifications(_pass1);

            var result = Subject.GetImportDecisions(_fileInfos, null, null, _idConfig);

            result.Single().Approved.Should().BeFalse();
        }

        [Test]
        public void should_return_rejected_if_only_track_spec_fails()
        {
            GivenSpecifications(_albumpass1);
            GivenSpecifications(_fail1);

            var result = Subject.GetImportDecisions(_fileInfos, null, null, _idConfig);

            result.Single().Approved.Should().BeFalse();
        }

        [Test]
        public void should_return_rejected_if_one_album_spec_fails()
        {
            GivenSpecifications(_albumpass1, _albumfail1, _albumpass2, _albumpass3);
            GivenSpecifications(_pass1, _pass2, _pass3);

            var result = Subject.GetImportDecisions(_fileInfos, null, null, _idConfig);

            result.Single().Approved.Should().BeFalse();
        }

        [Test]
        public void should_return_rejected_if_one_track_spec_fails()
        {
            GivenSpecifications(_albumpass1, _albumpass2, _albumpass3);
            GivenSpecifications(_pass1, _fail1, _pass2, _pass3);

            var result = Subject.GetImportDecisions(_fileInfos, null, null, _idConfig);

            result.Single().Approved.Should().BeFalse();
        }

        [Test]
        public void should_return_approved_if_all_specs_pass()
        {
            GivenAugmentationSuccess();
            GivenSpecifications(_albumpass1, _albumpass2, _albumpass3);
            GivenSpecifications(_pass1, _pass2, _pass3);

            var result = Subject.GetImportDecisions(_fileInfos, null, null, _idConfig);

            result.Single().Approved.Should().BeTrue();
        }

        [Test]
        public void should_reject_more_than_one_version_of_an_album()
        {
            GivenAugmentationSuccess();

            GivenAudioFiles(new[]
            {
                Tmp(@"C:\Test\Unsorted\release1\track1.mp3"),
                Tmp(@"C:\Test\Unsorted\release2\track1.mp3")
            });

            Mocker.GetMock<IIdentificationService>()
                .Setup(s => s.Identify(It.IsAny<List<LocalTrack>>(), It.IsAny<IdentificationOverrides>(), It.IsAny<ImportDecisionMakerConfig>()))
                .Returns((List<LocalTrack> tracks, IdentificationOverrides idOverrides, ImportDecisionMakerConfig config) =>
                {
                    var ret1 = new LocalAlbumRelease(tracks.Take(1).ToList());
                    ret1.AlbumRelease = _albumRelease;

                    var ret2 = new LocalAlbumRelease(tracks.Skip(1).ToList());
                    var albumRelease2 = Builder<AlbumRelease>.CreateNew()
                        .With(x => x.Album = _album)
                        .With(x => x.ForeignReleaseId = "anotherid")
                        .Build();
                    ret2.AlbumRelease = albumRelease2;

                    return new List<LocalAlbumRelease> { ret1, ret2 };
                });

            GivenSpecifications(_pass1);

            var result = Subject.GetImportDecisions(_fileInfos, null, null, _idConfig);
            result.Count(x => x.Approved).Should().Be(1);
            result.Count(x => !x.Approved).Should().Be(1);
        }

        [Test]
        public void should_have_same_number_of_rejections_as_specs_that_failed()
        {
            GivenAugmentationSuccess();
            GivenSpecifications(_pass1, _pass2, _pass3, _fail1, _fail2, _fail3);

            var result = Subject.GetImportDecisions(_fileInfos, null, null, _idConfig);
            result.Single().Rejections.Should().HaveCount(3);
        }

        [Test]
        public void should_not_blowup_the_process_due_to_failed_augment()
        {
            GivenSpecifications(_pass1);

            Mocker.GetMock<IAugmentingService>()
                  .Setup(c => c.Augment(It.IsAny<LocalTrack>(), It.IsAny<bool>()))
                  .Throws<TestException>();

            GivenAudioFiles(new[]
                {
                    Tmp(@"C:\Test\Unsorted\The.Office.S03E115.DVDRip.XviD-OSiTV"),
                    Tmp(@"C:\Test\Unsorted\The.Office.S03E115.DVDRip.XviD-OSiTV"),
                    Tmp(@"C:\Test\Unsorted\The.Office.S03E115.DVDRip.XviD-OSiTV")
                });

            var decisions = Subject.GetImportDecisions(_fileInfos, _idOverrides, null, _idConfig);

            Mocker.GetMock<IAugmentingService>()
                  .Verify(c => c.Augment(It.IsAny<LocalTrack>(), It.IsAny<bool>()), Times.Exactly(_fileInfos.Count));

            ExceptionVerification.ExpectedErrors(3);
        }

        [Test]
        public void should_not_throw_if_release_not_identified()
        {
            GivenSpecifications(_pass1);

            GivenAudioFiles(new[]
                {
                    Tmp(@"C:\Test\Unsorted\The.Office.S03E115.DVDRip.XviD-OSiTV"),
                    Tmp(@"C:\Test\Unsorted\The.Office.S03E115.DVDRip.XviD-OSiTV"),
                    Tmp(@"C:\Test\Unsorted\The.Office.S03E115.DVDRip.XviD-OSiTV")
                });

            Mocker.GetMock<IIdentificationService>()
                .Setup(s => s.Identify(It.IsAny<List<LocalTrack>>(), It.IsAny<IdentificationOverrides>(), It.IsAny<ImportDecisionMakerConfig>()))
                .Returns((List<LocalTrack> tracks, IdentificationOverrides idOverrides, ImportDecisionMakerConfig config) =>
                    {
                        return new List<LocalAlbumRelease> { new LocalAlbumRelease(tracks) };
                    });

            var decisions = Subject.GetImportDecisions(_fileInfos, _idOverrides, null, _idConfig);

            Mocker.GetMock<IAugmentingService>()
                  .Verify(c => c.Augment(It.IsAny<LocalTrack>(), It.IsAny<bool>()), Times.Exactly(_fileInfos.Count));

            decisions.Should().HaveCount(3);
            decisions.First().Rejections.Should().NotBeEmpty();
        }

        [Test]
        public void should_not_throw_if_tracks_are_not_found()
        {
            GivenSpecifications(_pass1);

            GivenAudioFiles(new[]
                {
                    Tmp(@"C:\Test\Unsorted\The.Office.S03E115.DVDRip.XviD-OSiTV"),
                    Tmp(@"C:\Test\Unsorted\The.Office.S03E115.DVDRip.XviD-OSiTV"),
                    Tmp(@"C:\Test\Unsorted\The.Office.S03E115.DVDRip.XviD-OSiTV")
                });

            var decisions = Subject.GetImportDecisions(_fileInfos, _idOverrides, null, _idConfig);

            Mocker.GetMock<IAugmentingService>()
                  .Verify(c => c.Augment(It.IsAny<LocalTrack>(), It.IsAny<bool>()), Times.Exactly(_fileInfos.Count));

            decisions.Should().HaveCount(3);
            decisions.First().Rejections.Should().NotBeEmpty();
        }

        [Test]
        public void should_return_a_decision_when_exception_is_caught()
        {
            Mocker.GetMock<IAugmentingService>()
                  .Setup(c => c.Augment(It.IsAny<LocalTrack>(), It.IsAny<bool>()))
                  .Throws<TestException>();

            GivenAudioFiles(new[]
                {
                    Tmp(@"C:\Test\Unsorted\The.Office.S03E115.DVDRip.XviD-OSiTV")
                });

            Subject.GetImportDecisions(_fileInfos, _idOverrides, null, _idConfig).Should().HaveCount(1);

            ExceptionVerification.ExpectedErrors(1);
        }
    }
}
