using System;
using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Upstream
{
    // One source table's account: rows read = rows imported + rows skipped.
    public class ReconciliationRow
    {
        public ReconciliationRow(string app, string table, long read, long imported, long skipped)
        {
            App = app;
            Table = table;
            Read = read;
            Imported = imported;
            Skipped = skipped;
        }

        public string App { get; }
        public string Table { get; }
        public long Read { get; }
        public long Imported { get; }
        public long Skipped { get; }

        public bool Balanced => Read == Imported + Skipped;
    }

    // Thrown when a table's rows do not add up. The message names the table and the counts, never a value.
    public class ImportVerificationException : UpstreamImportException
    {
        public ImportVerificationException(IReadOnlyList<ReconciliationRow> unbalanced)
            : base("Rows were lost or counted twice, so nothing was written: " + string.Join("; ", unbalanced.Select(Describe)))
        {
            Unbalanced = unbalanced;
        }

        public IReadOnlyList<ReconciliationRow> Unbalanced { get; }

        private static string Describe(ReconciliationRow row)
        {
            return $"{row.App} {row.Table} (read {row.Read}, imported {row.Imported}, skipped {row.Skipped})";
        }
    }

    public static class ImportReconciliation
    {
        // Only tables a stage claims are checked: tables the adapter merely counts (instance and
        // runtime state) are reported as skipped by their reason and are never written.
        public static List<ReconciliationRow> Check(ImportSession session, IEnumerable<IImportStage> stages)
        {
            var claimed = stages.SelectMany(s => s.Tables).ToHashSet();
            var rows = new List<ReconciliationRow>();

            foreach (var snapshot in session.Snapshots)
            {
                foreach (var table in snapshot.Tables)
                {
                    if (!claimed.Contains(new SourceTableRef(snapshot.AppName, table.Name)))
                    {
                        var deferred = session.DeferredTable(snapshot.AppName, table.Name);

                        if (deferred != null)
                        {
                            rows.Add(new ReconciliationRow(snapshot.AppName, table.Name, table.Rows.Count, 0, table.Rows.Count));
                        }

                        continue;
                    }

                    var skipped = session.Skips.Count(s => s.App == snapshot.AppName && s.Table == table.Name);
                    var imported = session.ImportedRows(snapshot.AppName, table.Name);

                    if (session.IsNotImportedByDesign(snapshot.AppName, table.Name))
                    {
                        if (imported > 0 || skipped > 0)
                        {
                            throw new UpstreamImportException($"{snapshot.AppName} {table.Name} is marked as not imported by design, but rows of it were counted (imported {imported}, skipped {skipped})");
                        }

                        continue;
                    }

                    rows.Add(new ReconciliationRow(
                        snapshot.AppName,
                        table.Name,
                        table.Rows.Count,
                        imported,
                        skipped));
                }
            }

            return rows.OrderBy(r => r.App, StringComparer.Ordinal).ThenBy(r => r.Table, StringComparer.Ordinal).ToList();
        }
    }
}
