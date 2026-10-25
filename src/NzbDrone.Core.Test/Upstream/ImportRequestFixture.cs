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
                "/skip=Radarr:Notifications,Sonarr:History",
                "/report=/import/out",
                "/dry-run");

            var request = ImportRequest.From(context, Default);

            request.RadarrPath.Should().Be("/import/radarr.db");
            request.ProwlarrPath.Should().Be("/import/prowlarr_backup.zip");
            request.OptionsPath.Should().Be("/import/options.json");
            request.SkipTables.Should().BeEquivalentTo(new[]
            {
                new SourceTableRef("Radarr", "Notifications"),
                new SourceTableRef("Sonarr", "History")
            });
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
        public void should_accept_a_sonarr_database_as_the_only_source()
        {
            var request = ImportRequest.From(new StartupContext("/import", "/sonarr=/import/sonarr.db"), Default);

            request.SonarrPath.Should().Be("/import/sonarr.db");
            request.RadarrPath.Should().BeNull();
            request.ProwlarrPath.Should().BeNull();
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

        [TestCase("Radarr:Notifications,Radarr:Notifications")]
        [TestCase("Radarr")]
        [TestCase(":Notifications")]
        public void should_reject_invalid_skip_lists(string value)
        {
            var act = () => ImportRequest.From(new StartupContext("/import", "/radarr=/import/radarr.db", "/skip=" + value), Default);

            act.Should().Throw<UpstreamImportException>();
        }
    }
}
