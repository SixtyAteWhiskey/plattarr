using System;
using System.IO;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Host
{
    // /import (docs/importer.md, "How it runs"): reads the arguments, runs the importer over the
    // migrated target in the data folder, prints a summary and returns the exit code. The summary has
    // counts and reason codes only; titles and names are in the report files.
    public class ImportCommand
    {
        private readonly IStartupContext _startupContext;
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IUpstreamImportRunner _runner;

        public ImportCommand(IStartupContext startupContext, IAppFolderInfo appFolderInfo, IUpstreamImportRunner runner)
        {
            _startupContext = startupContext;
            _appFolderInfo = appFolderInfo;
            _runner = runner;
        }

        public int Run()
        {
            ImportRequest request;

            try
            {
                request = ImportRequest.From(_startupContext, Path.Combine(_appFolderInfo.AppDataFolder, "import-report"));
            }
            catch (UpstreamImportException ex)
            {
                Console.Error.WriteLine(ex.Message);

                return 1;
            }

            var result = _runner.Run(request);

            if (result.Report == null)
            {
                Console.Error.WriteLine("Import failed: " + result.Error);

                return result.ExitCode;
            }

            Console.WriteLine(ImportReportWriter.ToSummary(result.Report));

            foreach (var file in result.ReportFiles)
            {
                Console.WriteLine("Report: " + file);
            }

            return result.ExitCode;
        }
    }
}
