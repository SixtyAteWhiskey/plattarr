using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.MediaFiles.TrackImport;
using NzbDrone.Core.Music.Parser.Model;

namespace NzbDrone.Core.Test.MusicTests.Import
{
    // How the core reads a Music import result through IImportResult: which rejections fail the
    // download and which block the automatic import.
    [TestFixture]
    public class MusicImportResultContractFixture
    {
        private static IImportResult Rejected(ImportRejectionReason reason)
        {
            return new ImportResult(
                new ImportDecision<LocalTrack>(
                    new LocalTrack { Path = @"C:\Test\Artist - Album\01 Track.flac" },
                    new ImportRejection(reason, "Rejected")),
                "Rejected");
        }

        [Test]
        public void should_fail_the_download_for_an_executable_file()
        {
            Rejected(ImportRejectionReason.ExecutableFile).FailsDownloadAs.Should().Be(FailDownloads.Executables);
        }

        [TestCase(ImportRejectionReason.Unknown)]
        [TestCase(ImportRejectionReason.ArchiveFile)]
        [TestCase(ImportRejectionReason.UnmatchedTrack)]
        [TestCase(ImportRejectionReason.NotQualityUpgrade)]
        public void should_not_fail_the_download_for_other_rejections(ImportRejectionReason reason)
        {
            Rejected(reason).FailsDownloadAs.Should().BeNull();
        }

        [TestCase(ImportRejectionReason.ExecutableFile)]
        [TestCase(ImportRejectionReason.Unknown)]
        [TestCase(ImportRejectionReason.UnmatchedTrack)]
        public void should_never_block_the_automatic_import_on_one_result(ImportRejectionReason reason)
        {
            Rejected(reason).BlocksAutomaticImport.Should().BeFalse();
        }

        [Test]
        public void should_expose_the_path_of_the_local_track()
        {
            Rejected(ImportRejectionReason.Unknown).Path.Should().Be(@"C:\Test\Artist - Album\01 Track.flac");
        }

        [Test]
        public void should_report_no_path_for_a_decision_without_a_local_track()
        {
            IImportResult result = new ImportResult(
                new ImportDecision<LocalTrack>(null, new ImportRejection(ImportRejectionReason.Unknown, "Rejected")),
                "Rejected");

            result.Path.Should().BeNull();
        }

        [Test]
        public void should_report_imported_when_there_are_no_errors()
        {
            IImportResult result = new ImportResult(new ImportDecision<LocalTrack>(new LocalTrack()));

            result.Result.Should().Be(ImportResultType.Imported);
            result.Errors.Should().BeEmpty();
        }

        [Test]
        public void should_report_rejected_when_the_decision_was_not_approved()
        {
            Rejected(ImportRejectionReason.Unknown).Result.Should().Be(ImportResultType.Rejected);
        }

        [Test]
        public void should_report_skipped_when_an_approved_decision_has_errors()
        {
            IImportResult result = new ImportResult(new ImportDecision<LocalTrack>(new LocalTrack()), "Failed to import");

            result.Result.Should().Be(ImportResultType.Skipped);
        }
    }
}
