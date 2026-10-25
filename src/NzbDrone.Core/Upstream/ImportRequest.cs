using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public const string SkipArg = "skip";

        public const string Usage = "Usage: /import [/radarr=<radarr.db or backup .zip>] [/prowlarr=<prowlarr.db or backup .zip>] "
            + "[/sonarr=<sonarr.db or backup .zip>] [/options=<options.json>] [/skip=<app:table,...>] [/dry-run] [/report=<directory>]. At least one source is required.";

        // Null when that app's database was not given.
        public string RadarrPath { get; set; }
        public string ProwlarrPath { get; set; }
        public string SonarrPath { get; set; }
        public string OptionsPath { get; set; }

        // Read tables with no stage that the owner explicitly defers for this run.
        public List<SourceTableRef> SkipTables { get; set; } = new List<SourceTableRef>();

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
                SkipTables = SkipTablesFrom(context),
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

        private static List<SourceTableRef> SkipTablesFrom(IStartupContext context)
        {
            if (!context.Args.TryGetValue(SkipArg, out var value))
            {
                return new List<SourceTableRef>();
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new UpstreamImportException("The /skip= value cannot be empty. " + Usage);
            }

            var result = new List<SourceTableRef>();

            foreach (var entry in value.Split(','))
            {
                var parts = entry.Trim().Split(':');

                if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                {
                    throw new UpstreamImportException($"Invalid /skip= entry '{entry}'. Use App:Table, for example Radarr:Notifications.");
                }

                result.Add(new SourceTableRef(parts[0].Trim(), parts[1].Trim()));
            }

            if (result.Distinct().Count() != result.Count)
            {
                throw new UpstreamImportException("The /skip= list contains a duplicate table.");
            }

            return result;
        }
    }
}
