using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Movies.Upstream;
using NzbDrone.Core.Tv.Upstream;

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
        private readonly IMediaCoverBackfillService _coverBackfill;

        public UpstreamImportRunner(IEnumerable<IUpstreamAdapter> adapters,
                                    IEnumerable<IImportStage> stages,
                                    IUpstreamImporter importer,
                                    IMediaCoverBackfillService coverBackfill)
        {
            _adapters = adapters;
            _stages = stages;
            _importer = importer;
            _coverBackfill = coverBackfill;
        }

        public ImportRunResult Run(ImportRequest request)
        {
            var copies = new List<UpstreamSourceCopy>();

            try
            {
                var options = LoadOptions(request.OptionsPath);
                options.SkipTables = request.SkipTables;
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

                RequireExplicitSkips(snapshots, request.SkipTables);

                var result = _importer.Run(snapshots, options, request.DryRun);

                // Persist the committed outcome before network cover downloads. If the process
                // stops during backfill, the report still records the commit and how to retry.
                const string backfillPending = "Cover backfill pending; if interrupted, run BackfillMediaCovers";
                var committed = result.Outcome == ImportOutcome.Committed;

                if (committed)
                {
                    result.Warnings.Add(backfillPending);
                }

                var report = ImportReportWriter.Build(result, sources, request.DryRun, DateTime.UtcNow);
                var files = ImportReportWriter.Write(request.ReportDirectory, report, result);

                if (committed)
                {
                    try
                    {
                        var movieIds = result.IdMap.Entries
                            .Where(e => e.App == UpstreamApps.Radarr && e.Table == MoviesStage.MoviesTable)
                            .Select(e => checked((int)e.TargetId));

                        var seriesIds = result.IdMap.Entries
                            .Where(e => e.App == UpstreamApps.Sonarr && e.Table == SeriesStage.SeriesTable)
                            .Select(e => checked((int)e.TargetId));

                        var covers = _coverBackfill.Backfill(movieIds, seriesIds);

                        if (covers.Missing > 0)
                        {
                            result.Warnings.Add($"Cover backfill: {covers.Missing} of {covers.Checked} imported Movie/TV covers are still missing; the scheduled task will retry");
                        }
                    }
                    catch (Exception)
                    {
                        // The database has already committed. Report an incomplete cover pass and
                        // leave the scheduled task to retry instead of claiming the import rolled back.
                        result.Warnings.Add("Cover backfill did not complete after commit; the scheduled task will retry");
                    }
                    finally
                    {
                        result.Warnings.Remove(backfillPending);
                        ImportReportWriter.Write(request.ReportDirectory, report, result);
                    }
                }

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

            if (request.SonarrPath != null)
            {
                yield return (AdapterFor(UpstreamApps.Sonarr), request.SonarrPath);
            }
        }

        private IUpstreamAdapter AdapterFor(string app)
        {
            return _adapters.SingleOrDefault(a => string.Equals(a.AppName, app, StringComparison.OrdinalIgnoreCase))
                ?? throw new UpstreamImportException($"There is no reader for {app} databases in this build");
        }

        private void RequireExplicitSkips(IEnumerable<SourceSnapshot> snapshots, IReadOnlyList<SourceTableRef> requested)
        {
            var claimed = _stages.SelectMany(s => s.Tables).ToHashSet();
            var missing = snapshots
                .SelectMany(s => s.Tables
                    .Where(t => t.Rows.Count > 0 && !claimed.Contains(new SourceTableRef(s.AppName, t.Name)))
                    .Where(t => !requested.Any(r => string.Equals(r.App, s.AppName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(r.Table, t.Name, StringComparison.OrdinalIgnoreCase)))
                    .Select(t => $"{s.AppName}:{t.Name}"))
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToList();

            if (missing.Count > 0)
            {
                throw new UpstreamImportException("Every non-empty source table without an import stage needs an explicit /skip= entry. Nothing was written. Add: /skip=" + string.Join(",", missing));
            }
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
