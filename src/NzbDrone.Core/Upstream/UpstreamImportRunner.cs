using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NzbDrone.Core.Upstream
{
    public class ImportRunResult
    {
        // 0 done (skips are in the report), 1 failed and nothing was written, 2 strict mode and a row was skipped.
        public int ExitCode { get; set; }

        // Set when the run reached a report; null when it failed before.
        public ImportReport Report { get; set; }
        public IReadOnlyList<string> ReportFiles { get; set; } = Array.Empty<string>();

        // For the failure case. It names a file or a table, never a row value.
        public string Error { get; set; }
    }

    public interface IUpstreamImportRunner
    {
        ImportRunResult Run(ImportRequest request);
    }

    // The /import command: copies each source, reads it through its adapter, runs the importer and
    // writes the report. The only code that touches the user's files; the importer itself works on
    // snapshots (docs/importer.md, "Safety rules").
    public class UpstreamImportRunner : IUpstreamImportRunner
    {
        private static readonly JsonSerializerOptions OptionsFormat = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private readonly IEnumerable<IUpstreamAdapter> _adapters;
        private readonly IEnumerable<IImportStage> _stages;
        private readonly IUpstreamImporter _importer;

        public UpstreamImportRunner(IEnumerable<IUpstreamAdapter> adapters, IEnumerable<IImportStage> stages, IUpstreamImporter importer)
        {
            _adapters = adapters;
            _stages = stages;
            _importer = importer;
        }

        public ImportRunResult Run(ImportRequest request)
        {
            var copies = new List<UpstreamSourceCopy>();

            try
            {
                var options = LoadOptions(request.OptionsPath);
                var snapshots = new List<SourceSnapshot>();
                var sources = new List<ReportSource>();

                foreach (var (adapter, path) in SourcesOf(request))
                {
                    var copy = UpstreamSourceCopy.Create(path, adapter.DatabaseFileName, request.ScratchDirectory);

                    copies.Add(copy);

                    using var connection = copy.OpenReadOnly();
                    var snapshot = adapter.Read(connection, options);

                    snapshots.Add(snapshot);
                    sources.Add(new ReportSource
                    {
                        App = adapter.AppName,
                        File = Path.GetFileName(path),
                        Version = snapshot.Version,
                        Sha256 = copy.Sha256
                    });
                }

                var unclaimed = Unclaimed(snapshots);

                // A committed import that leaves tables behind would look finished and not be. Until
                // every table has a stage, only a dry run is allowed.
                if (!request.DryRun && unclaimed.Count > 0)
                {
                    throw new UpstreamImportException(
                        "This version cannot import every table yet (" + string.Join(", ", unclaimed.Select(u => $"{u.App} {u.Table}")) + "), so nothing was written. Run again with /dry-run to see what it would do.");
                }

                var result = _importer.Run(snapshots, options, request.DryRun);
                var report = ImportReportWriter.Build(result, sources, unclaimed, request.DryRun, DateTime.UtcNow);
                var files = ImportReportWriter.Write(request.ReportDirectory, report, result);

                return new ImportRunResult
                {
                    ExitCode = result.Outcome == ImportOutcome.SkippedRows ? 2 : 0,
                    Report = report,
                    ReportFiles = files
                };
            }
            catch (UpstreamImportException ex)
            {
                return new ImportRunResult { ExitCode = 1, Error = ex.Message };
            }
            finally
            {
                foreach (var copy in copies)
                {
                    copy.Dispose();
                }
            }
        }

        private IEnumerable<(IUpstreamAdapter Adapter, string Path)> SourcesOf(ImportRequest request)
        {
            if (request.RadarrPath != null)
            {
                yield return (AdapterFor(UpstreamApps.Radarr), request.RadarrPath);
            }

            if (request.ProwlarrPath != null)
            {
                yield return (AdapterFor(UpstreamApps.Prowlarr), request.ProwlarrPath);
            }
        }

        private IUpstreamAdapter AdapterFor(string app)
        {
            return _adapters.SingleOrDefault(a => string.Equals(a.AppName, app, StringComparison.OrdinalIgnoreCase))
                ?? throw new UpstreamImportException($"There is no reader for {app} databases in this build");
        }

        // Tables the adapter read that have rows and no stage.
        private List<ReportUnclaimed> Unclaimed(IEnumerable<SourceSnapshot> snapshots)
        {
            var claimed = _stages.SelectMany(s => s.Tables).ToHashSet();

            return snapshots
                .SelectMany(s => s.Tables.Where(t => t.Exists && t.Rows.Count > 0 && !claimed.Contains(new SourceTableRef(s.AppName, t.Name)))
                    .Select(t => new ReportUnclaimed { App = s.AppName, Table = t.Name, Rows = t.Rows.Count }))
                .OrderBy(u => u.App, StringComparer.Ordinal)
                .ThenBy(u => u.Table, StringComparer.Ordinal)
                .ToList();
        }

        private static ImportOptions LoadOptions(string path)
        {
            if (path == null)
            {
                return new ImportOptions();
            }

            if (!File.Exists(path))
            {
                throw new UpstreamImportException($"Options file not found: {path}");
            }

            try
            {
                return JsonSerializer.Deserialize<ImportOptions>(File.ReadAllText(path), OptionsFormat) ?? new ImportOptions();
            }
            catch (JsonException ex)
            {
                throw new UpstreamImportException($"The options file {Path.GetFileName(path)} is not valid: {ex.Message}", ex);
            }
        }
    }
}
