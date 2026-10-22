using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NzbDrone.Core.Upstream
{
    public class IdMapEntry
    {
        public IdMapEntry(string app, string table, long sourceId, long targetId)
        {
            App = app;
            Table = table;
            SourceId = sourceId;
            TargetId = targetId;
        }

        public string App { get; }
        public string Table { get; }
        public long SourceId { get; }
        public long TargetId { get; }
    }

    // Source row to target row, per source app and table. Target IDs are assigned
    // by the database when the writer inserts a row (BasicRepository.Insert
    // refuses a row that already has an ID), so the map is filled stage by stage
    // as the writer goes, and each stage resolves the references it needs from
    // the stages before it.
    //
    // Several source rows may share one target (tags merge by label). One source
    // row never has two targets.
    public class IdMap
    {
        private readonly Dictionary<string, IdMapEntry> _entries = new Dictionary<string, IdMapEntry>(StringComparer.Ordinal);

        public int Count => _entries.Count;

        public IEnumerable<IdMapEntry> Entries => _entries.Values
            .OrderBy(e => e.App, StringComparer.Ordinal)
            .ThenBy(e => e.Table, StringComparer.Ordinal)
            .ThenBy(e => e.SourceId);

        public void Record(string app, string table, long sourceId, long targetId)
        {
            var key = Key(app, table, sourceId);

            if (_entries.TryGetValue(key, out var existing))
            {
                if (existing.TargetId == targetId)
                {
                    return;
                }

                throw new InvalidOperationException($"{app} {table} row {sourceId} is already mapped to a different target row");
            }

            _entries[key] = new IdMapEntry(app, table, sourceId, targetId);
        }

        public bool TryResolve(string app, string table, long sourceId, out long targetId)
        {
            if (_entries.TryGetValue(Key(app, table, sourceId), out var entry))
            {
                targetId = entry.TargetId;

                return true;
            }

            targetId = 0;

            return false;
        }

        // For a reference that must resolve: a miss means the stage order or the
        // planner is wrong, not that the data is odd.
        public long Resolve(string app, string table, long sourceId)
        {
            if (TryResolve(app, table, sourceId, out var targetId))
            {
                return targetId;
            }

            throw new UpstreamImportException($"No imported row for {app} {table} ID {sourceId}");
        }

        // The report's import-idmap file. App and table names are the importer's
        // own constants, so no field needs quoting.
        public string ToCsv()
        {
            var builder = new StringBuilder("app,table,source_id,target_id\n");

            foreach (var entry in Entries)
            {
                builder.Append(entry.App).Append(',')
                    .Append(entry.Table).Append(',')
                    .Append(entry.SourceId.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(entry.TargetId.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }

            return builder.ToString();
        }

        private static string Key(string app, string table, long sourceId)
        {
            return string.Concat(app.ToUpperInvariant(), "/", table.ToUpperInvariant(), "/", sourceId.ToString(CultureInfo.InvariantCulture));
        }
    }
}
