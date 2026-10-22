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

        // Null when that app's database was not given.
        public SourceSnapshot Snapshot(string app)
        {
            return Snapshots.SingleOrDefault(s => string.Equals(s.AppName, app, StringComparison.OrdinalIgnoreCase));
        }

        public void Skip(ImportSkip skip)
        {
            _skips.Add(skip);
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

        private static string ImportedKey(string app, string table)
        {
            return string.Concat(app.ToUpperInvariant(), "/", table.ToUpperInvariant());
        }
    }
}
