using System;
using System.Collections.Generic;

namespace NzbDrone.Core.Upstream
{
    // All rows of one table, as found.
    public class SourceTable
    {
        public SourceTable(string name, IReadOnlyList<string> columns, IReadOnlyList<SourceRow> rows)
        {
            Name = name;
            Columns = columns;
            Rows = rows;
            Exists = true;
        }

        private SourceTable(string name)
        {
            Name = name;
            Columns = Array.Empty<string>();
            Rows = Array.Empty<SourceRow>();
        }

        public string Name { get; }

        // False when the source database has no such table (an older schema).
        public bool Exists { get; }
        public IReadOnlyList<string> Columns { get; }
        public IReadOnlyList<SourceRow> Rows { get; }

        public static SourceTable Missing(string name)
        {
            return new SourceTable(name);
        }
    }

    // A table the importer does not read. Only the row count is taken, so the
    // reconciliation (rows read = rows written + rows skipped) covers every table.
    public class SkippedTable
    {
        public SkippedTable(string name, long rowCount, string reason)
        {
            Name = name;
            RowCount = rowCount;
            Reason = reason;
        }

        public string Name { get; }
        public long RowCount { get; }
        public string Reason { get; }
    }

    // Everything an adapter read from one upstream database: typed by table, not
    // yet remapped. The planner turns snapshots into target rows.
    public class SourceSnapshot
    {
        private readonly Dictionary<string, SourceTable> _tables = new Dictionary<string, SourceTable>(StringComparer.OrdinalIgnoreCase);

        public SourceSnapshot(string appName, long version)
        {
            AppName = appName;
            Version = version;
        }

        public string AppName { get; }
        public long Version { get; }
        public IReadOnlyCollection<SourceTable> Tables => _tables.Values;
        public List<SkippedTable> SkippedTables { get; } = new List<SkippedTable>();
        public List<string> Warnings { get; } = new List<string>();

        public void Add(SourceTable table)
        {
            _tables[table.Name] = table;
        }

        // An unread or absent table is an empty one, so callers loop without a
        // null check. Table.Exists tells the two cases from "present and empty".
        public SourceTable Table(string name)
        {
            return _tables.TryGetValue(name, out var table) ? table : SourceTable.Missing(name);
        }

        public IReadOnlyList<SourceRow> Rows(string name)
        {
            return Table(name).Rows;
        }
    }
}
