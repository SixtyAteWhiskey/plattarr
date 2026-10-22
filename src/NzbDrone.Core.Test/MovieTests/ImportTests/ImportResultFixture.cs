using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.MediaFiles.MovieImport;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MovieTests.ImportTests
{
    // Contract 5: what the core reads from a movie import result.
    [TestFixture]
    public class ImportResultFixture : CoreTest
    {
        private static IImportResult Rejected(params ImportRejectionReason[] reasons)
        {
            var rejections = System.Array.ConvertAll(reasons, r => new ImportRejection(r, r.ToString()));

            return new ImportResult(new ImportDecision(new LocalMovie { Path = "/downloads/movie.mkv" }, rejections), "Rejected");
        }

        [TestCase(ImportRejectionReason.DangerousFile, FailDownloads.PotentiallyDangerous)]
        [TestCase(ImportRejectionReason.ExecutableFile, FailDownloads.Executables)]
        public void should_map_rejections_indexers_can_fail(ImportRejectionReason reason, FailDownloads expected)
        {
            Rejected(reason).FailsDownloadAs.Should().Be(expected);
        }

        [TestCase(ImportRejectionReason.Sample)]
        [TestCase(ImportRejectionReason.ArchiveFile)]
        [TestCase(ImportRejectionReason.Unknown)]
        public void should_not_map_other_rejections(ImportRejectionReason reason)
        {
            Rejected(reason).FailsDownloadAs.Should().BeNull();
        }

        [Test]
        public void should_use_the_first_rejection()
        {
            Rejected(ImportRejectionReason.Sample, ImportRejectionReason.ExecutableFile).FailsDownloadAs.Should().BeNull();
            Rejected(ImportRejectionReason.ExecutableFile, ImportRejectionReason.Sample).FailsDownloadAs.Should().Be(FailDownloads.Executables);
        }

        [Test]
        public void should_report_the_local_movie_path()
        {
            Rejected(ImportRejectionReason.Sample).Path.Should().Be("/downloads/movie.mkv");
            ((IImportResult)new ImportResult(new ImportDecision(null), "Unknown movie")).Path.Should().BeNull();
        }
    }
}
