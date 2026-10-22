using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;

namespace NzbDrone.Core.Upstream
{
    // Shared mechanics of an adapter: the version gate, the tolerant table reader
    // and the counts for tables that are not read. A subclass lists tables.
    //
    // Reading is tolerant by design: a table the source lacks comes back as an
    // empty SourceTable (Exists is false) and a column it lacks is simply not in
    // the row. Only the identity tables (RequiredTables) are mandatory, because
    // without them the file is a different app's database.
    public abstract class UpstreamAdapterBase : IUpstreamAdapter
    {
        public abstract string AppName { get; }
        public abstract string DatabaseFileName { get; }
        public abstract VersionRange SupportedVersions { get; }

        // Tables whose absence means this is not this app's database.
        protected abstract IReadOnlyList<string> RequiredTables { get; }

        // Tables read in full.
        protected abstract IReadOnlyList<string> TablesToRead { get; }

        // Tables not read; only their row count is taken.
        protected abstract IReadOnlyList<(string Table, string Reason)> TablesToCount { get; }

        public SourceSnapshot Read(IDbConnection source, ImportOptions options)
        {
            try
            {
                if (source.State != ConnectionState.Open)
                {
                    source.Open();
                }

                var existing = ListTables(source);

                foreach (var required in RequiredTables)
                {
                    if (!existing.Contains(required))
                    {
                        throw new UpstreamImportException($"This does not look like a {AppName} database: table {required} is missing");
                    }
                }

                var version = ReadVersion(source);
                var snapshot = new SourceSnapshot(AppName, version);

                CheckVersion(version, options, snapshot);

                foreach (var table in TablesToRead)
                {
                    snapshot.Add(existing.Contains(table) ? ReadTable(source, table) : SourceTable.Missing(table));
                }

                foreach (var (table, reason) in TablesToCount)
                {
                    if (existing.Contains(table))
                    {
                        snapshot.SkippedTables.Add(new SkippedTable(table, CountRows(source, table), reason));
                    }
                }

                return snapshot;
            }
            catch (DbException ex)
            {
                throw new UpstreamImportException($"The {AppName} database could not be read: {ex.Message}", ex);
            }
        }

        private void CheckVersion(long version, ImportOptions options, SourceSnapshot snapshot)
        {
            if (SupportedVersions.Contains(version))
            {
                return;
            }

            var supported = $"{SupportedVersions.Min} to {SupportedVersions.Max}";

            if (version < SupportedVersions.Min)
            {
                throw new UpstreamImportException($"The {AppName} database is at schema version {version}, older than the oldest this importer supports ({supported}). Upgrade it by starting the current {AppName} release once, then export it again.");
            }

            if (options.AllowNewerSource)
            {
                snapshot.Warnings.Add($"The {AppName} database is at schema version {version}, newer than the newest this importer was tested with ({SupportedVersions.Max}). It was read anyway because allowNewerSource is set; check the report for skipped rows.");
                return;
            }

            throw new UpstreamImportException($"The {AppName} database is at schema version {version}, newer than the newest this importer supports ({SupportedVersions.Max}). Set allowNewerSource to try it anyway.");
        }

        private long ReadVersion(IDbConnection source)
        {
            using var command = source.CreateCommand();
            command.CommandText = "SELECT MAX(Version) FROM VersionInfo";

            var result = command.ExecuteScalar();

            if (result == null || result is DBNull)
            {
                throw new UpstreamImportException($"The {AppName} database has no schema version recorded");
            }

            return Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }

        private static HashSet<string> ListTables(IDbConnection source)
        {
            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var command = source.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }

            return tables;
        }

        // Table names come from the adapters' own constant lists, never from input.
        private static SourceTable ReadTable(IDbConnection source, string table)
        {
            using var command = source.CreateCommand();
            command.CommandText = $"SELECT * FROM \"{table}\"";

            using var reader = command.ExecuteReader();

            var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
            var rows = new List<SourceRow>();

            while (reader.Read())
            {
                var values = new Dictionary<string, object>(columns.Count, StringComparer.OrdinalIgnoreCase);

                for (var i = 0; i < columns.Count; i++)
                {
                    values[columns[i]] = reader.GetValue(i);
                }

                rows.Add(new SourceRow(values));
            }

            return new SourceTable(table, columns, rows);
        }

        private static long CountRows(IDbConnection source, string table)
        {
            using var command = source.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";

            return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
    }
}
