using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Upstream;
using NzbDrone.Host;
using NzbDrone.Test.Common;

namespace NzbDrone.App.Test
{
    [TestFixture]
    public class ImportCommandFixture : TestBase<ImportCommand>
    {
        private ImportRequest _request;

        private void Arguments(params string[] args)
        {
            Arguments(new ImportRunResult { ExitCode = 1, Error = "stop" }, args);
        }

        private void Arguments(ImportRunResult result, params string[] args)
        {
            Mocker.SetConstant<IStartupContext>(new StartupContext(args));
            Mocker.GetMock<IAppFolderInfo>().SetupGet(a => a.AppDataFolder).Returns("/config");
            Mocker.GetMock<IUpstreamImportRunner>()
                .Setup(r => r.Run(It.IsAny<ImportRequest>()))
                .Callback<ImportRequest>(r => _request = r)
                .Returns(result);
        }

        [SetUp]
        public void Setup()
        {
            _request = null;
        }

        [Test]
        public void should_select_import_mode_from_the_import_flag()
        {
            Bootstrap.GetApplicationMode(new StartupContext("-nobrowser", "/import", "/radarr=/import/radarr.db")).Should().Be(ApplicationModes.Import);
            Bootstrap.GetApplicationMode(new StartupContext("-nobrowser")).Should().Be(ApplicationModes.Interactive);
        }

        [Test]
        public void should_report_into_the_data_folder_by_default()
        {
            Arguments("/import", "/radarr=/import/radarr.db");

            Subject.Run();

            _request.ReportDirectory.Should().EndWith("import-report").And.StartWith("/config");
            _request.RadarrPath.Should().Be("/import/radarr.db");
        }

        [Test]
        public void should_return_1_without_running_when_no_source_was_given()
        {
            Arguments("/import");

            Subject.Run().Should().Be(1);

            Mocker.GetMock<IUpstreamImportRunner>().Verify(r => r.Run(It.IsAny<ImportRequest>()), Times.Never());
        }

        [Test]
        public void should_return_the_exit_code_of_a_failed_run()
        {
            Arguments("/import", "/radarr=/import/radarr.db");

            Subject.Run().Should().Be(1);
        }

        [Test]
        public void should_return_the_exit_code_of_a_finished_run()
        {
            Arguments(
                new ImportRunResult
                {
                    ExitCode = 2,
                    Report = new ImportReport { Outcome = "SkippedRows", Tables = new List<ReportTable>() },
                    ReportFiles = new[] { "/config/import-report/import.json" }
                },
                "/import",
                "/radarr=/import/radarr.db");

            Subject.Run().Should().Be(2);
        }
    }
}
