using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    [TestFixture]
    public class ImportRequestFixture
    {
        private const string Default = "/config/import-report";

        [Test]
        public void should_read_the_documented_arguments()
        {
            var context = new StartupContext(
                "-nobrowser",
                "-data=/config",
                "/import",
                "/radarr=/import/radarr.db",
                "/prowlarr=/import/prowlarr_backup.zip",
                "/options=/import/options.json",
                "/report=/import/out",
                "/dry-run");

            var request = ImportRequest.From(context, Default);

            request.RadarrPath.Should().Be("/import/radarr.db");
            request.ProwlarrPath.Should().Be("/import/prowlarr_backup.zip");
            request.OptionsPath.Should().Be("/import/options.json");
            request.ReportDirectory.Should().Be("/import/out");
            request.DryRun.Should().BeTrue();
        }

        [Test]
        public void should_default_to_a_real_run_with_the_default_report_directory_and_one_source()
        {
            var request = ImportRequest.From(new StartupContext("/import", "/radarr=/import/radarr.db"), Default);

            request.DryRun.Should().BeFalse();
            request.ReportDirectory.Should().Be(Default);
            request.ProwlarrPath.Should().BeNull();
            request.OptionsPath.Should().BeNull();
        }

        [Test]
        public void should_refuse_a_run_with_no_source()
        {
            var act = () => ImportRequest.From(new StartupContext("/import", "/dry-run"), Default);

            act.Should().Throw<UpstreamImportException>().WithMessage("No source database was given.*");
        }

        [Test]
        public void should_treat_an_empty_value_as_not_given()
        {
            var act = () => ImportRequest.From(new StartupContext("/import", "/radarr="), Default);

            act.Should().Throw<UpstreamImportException>();
        }
    }
}
