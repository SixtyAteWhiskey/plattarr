using System.IO;
using NzbDrone.Common.EnvironmentInfo;

namespace NzbDrone.Core.Upstream
{
    // The command line of /import (docs/importer.md, "How it runs"), parsed. Paths of the
    // sources are the user's own files; the importer copies them and never opens them in place.
    public class ImportRequest
    {
        public const string ImportFlag = "import";
        public const string DryRunFlag = "dry-run";
        public const string RadarrArg = "radarr";
        public const string ProwlarrArg = "prowlarr";
        public const string SonarrArg = "sonarr";
        public const string OptionsArg = "options";
        public const string ReportArg = "report";

        public const string Usage = "Usage: /import [/radarr=<radarr.db or backup .zip>] [/prowlarr=<prowlarr.db or backup .zip>] "
            + "[/sonarr=<sonarr.db or backup .zip>] [/options=<options.json>] [/dry-run] [/report=<directory>]. At least one source is required.";

        // Null when that app's database was not given.
        public string RadarrPath { get; set; }
        public string ProwlarrPath { get; set; }
        public string SonarrPath { get; set; }
        public string OptionsPath { get; set; }

        // Runs every stage on the real schema and rolls back.
        public bool DryRun { get; set; }

        public string ReportDirectory { get; set; }

        // Where the private copies of the sources live while the import runs.
        public string ScratchDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "plattarr-import");

        public static ImportRequest From(IStartupContext context, string defaultReportDirectory)
        {
            var request = new ImportRequest
            {
                RadarrPath = Arg(context, RadarrArg),
                ProwlarrPath = Arg(context, ProwlarrArg),
                SonarrPath = Arg(context, SonarrArg),
                OptionsPath = Arg(context, OptionsArg),
                DryRun = context.Flags.Contains(DryRunFlag),
                ReportDirectory = Arg(context, ReportArg) ?? defaultReportDirectory
            };

            if (request.RadarrPath == null && request.ProwlarrPath == null && request.SonarrPath == null)
            {
                throw new UpstreamImportException("No source database was given. " + Usage);
            }

            return request;
        }

        private static string Arg(IStartupContext context, string name)
        {
            return context.Args.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : null;
        }
    }
}
