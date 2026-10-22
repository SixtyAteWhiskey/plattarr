using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NzbDrone.Core.Upstream
{
    public class ReportSource
    {
        public string App { get; set; }

        // The file name only: a path can name the user's home folder.
        public string File { get; set; }
        public long Version { get; set; }
        public string Sha256 { get; set; }
    }

    public class ReportTable
    {
        public string App { get; set; }
        public string Table { get; set; }
        public long Read { get; set; }
        public long Imported { get; set; }
        public long Skipped { get; set; }

        // Reason code to number of rows.
        public Dictionary<string, long> Reasons { get; set; } = new Dictionary<string, long>();
    }

    // A table the adapter read, with rows, that no stage handles yet. Counted so the report says what
    // this build leaves behind; a real run refuses to commit while any exist.
    public class ReportUnclaimed
    {
        public string App { get; set; }
        public string Table { get; set; }
        public long Rows { get; set; }
    }

    public class ImportReport
    {
        public DateTime GeneratedAt { get; set; }
        public string Outcome { get; set; }
        public bool DryRun { get; set; }
        public List<ReportSource> Sources { get; set; } = new List<ReportSource>();
        public List<ReportTable> Tables { get; set; } = new List<ReportTable>();
        public List<ReportUnclaimed> NotYetImported { get; set; } = new List<ReportUnclaimed>();
        public List<UnreadTable> NotRead { get; set; } = new List<UnreadTable>();
        public List<ImportSkip> Skips { get; set; } = new List<ImportSkip>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    // Turns a result into the report files of docs/importer.md ("Report"): JSON, text, and (for a
    // committed run) the ID map. A skip carries a title or provider name by design; nothing else from
    // a source row is written. The console summary leaves even the names out.
    public static class ImportReportWriter
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        public static ImportReport Build(ImportResult result, IReadOnlyList<ReportSource> sources, IReadOnlyList<ReportUnclaimed> unclaimed, bool dryRun, DateTime generatedAt)
        {
            var report = new ImportReport
            {
                GeneratedAt = generatedAt,
                Outcome = result.Outcome.ToString(),
                DryRun = dryRun,
                Sources = sources.ToList(),
                NotYetImported = unclaimed.ToList(),
                NotRead = result.UnreadTables,
                Skips = result.Skips,
                Warnings = result.Warnings
            };

            foreach (var row in result.Tables)
            {
                var reasons = result.Skips
                    .Where(s => s.App == row.App && s.Table == row.Table)
                    .GroupBy(s => s.Reason)
                    .ToDictionary(g => g.Key, g => (long)g.Count());

                report.Tables.Add(new ReportTable
                {
                    App = row.App,
                    Table = row.Table,
                    Read = row.Read,
                    Imported = row.Imported,
                    Skipped = row.Skipped,
                    Reasons = reasons
                });
            }

            return report;
        }

        public static string ToJson(ImportReport report)
        {
            return JsonSerializer.Serialize(report, JsonOptions);
        }

        // Everything the user must look at is at the top; the long lists follow.
        public static string ToText(ImportReport report)
        {
            var text = new StringBuilder();

            text.AppendLine("Plattarr import report, " + report.GeneratedAt.ToString("u", CultureInfo.InvariantCulture));
            text.AppendLine("Outcome: " + Describe(report));
            text.AppendLine();

            text.AppendLine("Needs attention:");

            var attention = Attention(report);

            if (attention.Count == 0)
            {
                text.AppendLine("  Nothing.");
            }

            foreach (var line in attention)
            {
                text.AppendLine("  " + line);
            }

            text.AppendLine();
            text.AppendLine("Sources:");

            foreach (var source in report.Sources)
            {
                text.AppendLine($"  {source.App}: {source.File}, schema version {source.Version}, sha256 {source.Sha256}");
            }

            text.AppendLine();
            text.AppendLine("Tables (read = imported + skipped):");

            foreach (var table in report.Tables)
            {
                var reasons = table.Reasons.Count == 0 ? string.Empty : "  [" + string.Join(", ", table.Reasons.OrderBy(r => r.Key, StringComparer.Ordinal).Select(r => $"{r.Key} {r.Value}")) + "]";

                text.AppendLine($"  {table.App,-9} {table.Table,-22} read {table.Read,6}  imported {table.Imported,6}  skipped {table.Skipped,6}{reasons}");
            }

            if (report.NotYetImported.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Not imported by this version (no stage handles these tables yet):");

                foreach (var table in report.NotYetImported)
                {
                    text.AppendLine($"  {table.App} {table.Table}: {table.Rows} rows");
                }
            }

            if (report.NotRead.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Not read by design:");

                foreach (var table in report.NotRead)
                {
                    text.AppendLine($"  {table.App} {table.Name}: {table.RowCount} rows ({table.Reason})");
                }
            }

            if (report.Skips.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Skipped rows:");

                foreach (var skip in report.Skips)
                {
                    var id = skip.SourceId.HasValue ? skip.SourceId.Value.ToString(CultureInfo.InvariantCulture) : "-";

                    text.AppendLine($"  {skip.App} {skip.Table} #{id} \"{skip.Name}\": {skip.Reason}: {skip.Detail}");
                }
            }

            if (report.Warnings.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Warnings:");

                foreach (var warning in report.Warnings)
                {
                    text.AppendLine("  " + warning);
                }
            }

            return text.ToString();
        }

        // What the console prints: counts and reason codes only, no title, path or name.
        public static string ToSummary(ImportReport report)
        {
            var text = new StringBuilder();

            text.AppendLine("Import " + Describe(report));

            foreach (var table in report.Tables)
            {
                var reasons = table.Reasons.Count == 0 ? string.Empty : " (" + string.Join(", ", table.Reasons.OrderBy(r => r.Key, StringComparer.Ordinal).Select(r => $"{r.Key} {r.Value}")) + ")";

                text.AppendLine($"  {table.App} {table.Table}: read {table.Read}, imported {table.Imported}, skipped {table.Skipped}{reasons}");
            }

            foreach (var line in Attention(report))
            {
                text.AppendLine("  ! " + line);
            }

            return text.ToString();
        }

        public static string ToIdMapCsv(IdMap map)
        {
            var csv = new StringBuilder();

            csv.AppendLine("app,table,sourceId,targetId");

            foreach (var entry in map.Entries)
            {
                csv.AppendLine(string.Join(",", entry.App, entry.Table, entry.SourceId.ToString(CultureInfo.InvariantCulture), entry.TargetId.ToString(CultureInfo.InvariantCulture)));
            }

            return csv.ToString();
        }

        // Writes the files and returns their paths.
        public static IReadOnlyList<string> Write(string directory, ImportReport report, ImportResult result)
        {
            Directory.CreateDirectory(directory);

            var stamp = report.GeneratedAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var written = new List<string>();

            string Save(string name, string content)
            {
                var path = Path.Combine(directory, name);

                File.WriteAllText(path, content);
                written.Add(path);

                return path;
            }

            Save($"import-{stamp}.json", ToJson(report));
            Save($"import-{stamp}.txt", ToText(report));

            // After a rollback the target IDs name rows that do not exist.
            if (result.Outcome == ImportOutcome.Committed)
            {
                Save($"import-idmap-{stamp}.csv", ToIdMapCsv(result.IdMap));
            }

            return written;
        }

        private static string Describe(ImportReport report)
        {
            return report.Outcome switch
            {
                nameof(ImportOutcome.Committed) => "committed",
                nameof(ImportOutcome.DryRun) => "dry run (rolled back, nothing was written)",
                nameof(ImportOutcome.SkippedRows) => "stopped by strict mode: rows were skipped, nothing was written",
                _ => report.Outcome
            };
        }

        private static List<string> Attention(ImportReport report)
        {
            var lines = new List<string>();

            var skipped = report.Skips.Count;

            if (skipped > 0)
            {
                var codes = string.Join(", ", report.Skips.GroupBy(s => s.Reason).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key} {g.Count()}"));

                lines.Add($"{skipped} rows were skipped ({codes}).");
            }

            if (report.NotYetImported.Count > 0)
            {
                lines.Add($"{report.NotYetImported.Count} source tables with rows are not imported by this version.");
            }

            if (report.Warnings.Count > 0)
            {
                lines.Add($"{report.Warnings.Count} warnings.");
            }

            return lines;
        }
    }
}
