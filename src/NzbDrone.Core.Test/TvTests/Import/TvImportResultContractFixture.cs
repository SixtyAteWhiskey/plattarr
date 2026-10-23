using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Tv.MediaFiles.EpisodeImport;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Test.TvTests.Import
{
    // How the core reads a TV import result through IImportResult: which rejections fail the download
    // and which block the automatic import.
    [TestFixture]
    public class TvImportResultContractFixture
    {
        private static IImportResult Rejected(ImportRejectionReason reason)
        {
            return new ImportResult(
                new ImportDecision(
                    new LocalEpisode { Path = @"C:\Test\Drone.S01E01.mkv" },
                    new ImportRejection(reason, "Rejected")),
                "Rejected");
        }

        [TestCase(ImportRejectionReason.DangerousFile, FailDownloads.PotentiallyDangerous)]
        [TestCase(ImportRejectionReason.ExecutableFile, FailDownloads.Executables)]
        [TestCase(ImportRejectionReason.UserRejectedExtension, FailDownloads.UserDefinedExtensions)]
        public void should_fail_the_download_for_these_rejections(ImportRejectionReason reason, FailDownloads expected)
        {
            Rejected(reason).FailsDownloadAs.Should().Be(expected);
        }

        [TestCase(ImportRejectionReason.Unknown)]
        [TestCase(ImportRejectionReason.MultiSeason)]
        public void should_not_fail_the_download_for_other_rejections(ImportRejectionReason reason)
        {
            Rejected(reason).FailsDownloadAs.Should().BeNull();
        }

        [Test]
        public void should_block_the_automatic_import_for_a_multi_season_release_only()
        {
            Rejected(ImportRejectionReason.MultiSeason).BlocksAutomaticImport.Should().BeTrue();
            Rejected(ImportRejectionReason.Unknown).BlocksAutomaticImport.Should().BeFalse();
            Rejected(ImportRejectionReason.DangerousFile).BlocksAutomaticImport.Should().BeFalse();
        }

        [Test]
        public void should_expose_the_path_of_the_local_episode()
        {
            Rejected(ImportRejectionReason.Unknown).Path.Should().Be(@"C:\Test\Drone.S01E01.mkv");
        }

        [Test]
        public void should_report_no_path_for_a_decision_without_a_local_episode()
        {
            IImportResult result = new ImportResult(new ImportDecision(null, new ImportRejection(ImportRejectionReason.Unknown, "Rejected")), "Rejected");

            result.Path.Should().BeNull();
        }
    }
}
