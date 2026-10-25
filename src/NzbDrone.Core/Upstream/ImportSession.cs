using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace NzbDrone.Core.Upstream
{
    // The state one import run shares between its stages. Everything is written on Connection
    // and Transaction, which the importer owns and commits only after the checks pass. A
    // repository call (All, Count, Get) opens another connection that cannot see these
    // uncommitted rows, so a stage reads the target with plain SQL on this connection.
    public class ImportSession
    {
        private readonly List<ImportSkip> _skips = new List<ImportSkip>();
        private readonly List<string> _warnings = new List<string>();
        private readonly Dictionary<string, long> _imported = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly List<(string App, string Table, string Reason)> _notImported = new List<(string App, string Table, string Reason)>();
        private readonly List<DeferredSourceTable> _deferredTables = new List<DeferredSourceTable>();

        public ImportSession(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<SourceSnapshot> snapshots, ImportOptions options)
        {
            Connection = connection;
            Transaction = transaction;
            Snapshots = snapshots;
            Options = options;
            Paths = new PathMapper(options.PathMap);
        }

        public IDbConnection Connection { get; }
        public IDbTransaction Transaction { get; }
        public IReadOnlyList<SourceSnapshot> Snapshots { get; }
        public ImportOptions Options { get; }
        public IdMap IdMap { get; } = new IdMap();
        public PathMapper Paths { get; }
        public IReadOnlyList<ImportSkip> Skips => _skips;
        public IReadOnlyList<string> Warnings => _warnings;
        public IReadOnlyList<DeferredSourceTable> DeferredTables => _deferredTables;

        // One moment for the whole run. A required date the source row does not have (a movie
        // with no Added date) gets this one: it is when the row entered this database.
        public DateTime ImportedAt { get; } = DateTime.UtcNow;

        // Null when that app's database was not given.
        public SourceSnapshot Snapshot(string app)
        {
            return Snapshots.SingleOrDefault(s => string.Equals(s.AppName, app, StringComparison.OrdinalIgnoreCase));
        }

        public void Skip(ImportSkip skip)
        {
            _skips.Add(skip);
        }

        // The row cannot be written as it is: a document the target cannot read, or a reference
        // to a row that was not imported. The detail names a column or a table, never a value.
        public void SkipCorrupt(string app, string table, long? sourceId, string name, string detail)
        {
            _skips.Add(new ImportSkip(app, table, sourceId, name, SkipReason.Corrupt, detail));
        }

        public void Warn(string warning)
        {
            _warnings.Add(warning);
        }

        // Source rows now represented in the target. Rows merged into one target row (tags with the
        // same label) all count, so the reconciliation works in source rows.
        public void Imported(string app, string table, long rows = 1)
        {
            var key = ImportedKey(app, table);

            _imported[key] = ImportedRows(app, table) + rows;
        }

        public long ImportedRows(string app, string table)
        {
            return _imported.TryGetValue(ImportedKey(app, table), out var rows) ? rows : 0;
        }

        // A table the stage read whole for its own lookups (Prowlarr's sync targets, read to match
        // Radarr's indexers) and that has no home in Plattarr. None of its rows is imported or
        // skipped: the report lists the table with the tables the adapters only count, and strict
        // mode does not fail on it, because nothing the user set is lost.
        public void NotImportedByDesign(string app, string table, string reason)
        {
            _notImported.Add((app, table, reason));
        }

        public IReadOnlyList<(string App, string Table, string Reason)> TablesNotImportedByDesign => _notImported;

        public bool IsNotImportedByDesign(string app, string table)
        {
            return _notImported.Any(t => string.Equals(t.App, app, StringComparison.OrdinalIgnoreCase) && string.Equals(t.Table, table, StringComparison.OrdinalIgnoreCase));
        }

        public void DeferTable(string app, string table, long rows)
        {
            _deferredTables.Add(new DeferredSourceTable { App = app, Table = table, Rows = rows });
        }

        public DeferredSourceTable DeferredTable(string app, string table)
        {
            return _deferredTables.SingleOrDefault(t => string.Equals(t.App, app, StringComparison.OrdinalIgnoreCase)
                && string.Equals(t.Table, table, StringComparison.OrdinalIgnoreCase));
        }

        private static string ImportedKey(string app, string table)
        {
            return string.Concat(app.ToUpperInvariant(), "/", table.ToUpperInvariant());
        }
    }
}
