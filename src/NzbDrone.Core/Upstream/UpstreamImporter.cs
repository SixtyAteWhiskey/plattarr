using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Upstream
{
    public enum ImportOutcome
    {
        // Everything was written and committed.
        Committed,

        // Everything ran against the real schema and was rolled back, as asked.
        DryRun,

        // Strict mode, and at least one row was skipped: nothing was written.
        SkippedRows
    }

    // A table the adapter counted but does not read (instance and runtime state), for the report.
    public class UnreadTable
    {
        public UnreadTable(string app, SkippedTable table)
        {
            App = app;
            Name = table.Name;
            RowCount = table.RowCount;
            Reason = table.Reason;
        }

        public string App { get; }
        public string Name { get; }
        public long RowCount { get; }
        public string Reason { get; }
    }

    public class ImportResult
    {
        public ImportOutcome Outcome { get; set; }
        public List<ReconciliationRow> Tables { get; set; } = new List<ReconciliationRow>();
        public List<UnreadTable> UnreadTables { get; set; } = new List<UnreadTable>();
        public List<ImportSkip> Skips { get; set; } = new List<ImportSkip>();
        public List<string> Warnings { get; set; } = new List<string>();
        public IdMap IdMap { get; set; }
    }

    public interface IUpstreamImporter
    {
        ImportResult Run(IReadOnlyList<SourceSnapshot> snapshots, ImportOptions options, bool dryRun);
    }

    // Runs every registered stage in order on one connection and one transaction, checks that no row
    // was lost, and commits only when everything passed. An exception anywhere, a failed check, strict
    // mode with a skipped row, or a dry run leaves the target as it was.
    public class UpstreamImporter : IUpstreamImporter
    {
        // Written by the importer when it finishes (the settings stage), so a second run is refused.
        private const string ImportedFromKey = "importedfrom";

        // A migrated target that has never run holds none of these. The default delay profile
        // row that migration 001 inserts is not a user's data and is not listed.
        private static readonly string[] TablesThatMustBeEmpty =
        {
            "Movies_Movies",
            "Indexers",
            "DownloadClients",
            "Notifications",
            "QualityProfiles",
            "CustomFormats",
            "Tags"
        };

        private readonly IMainDatabase _database;
        private readonly IEnumerable<IImportStage> _stages;

        public UpstreamImporter(IMainDatabase database, IEnumerable<IImportStage> stages)
        {
            _database = database;
            _stages = stages;
        }

        public ImportResult Run(IReadOnlyList<SourceSnapshot> snapshots, ImportOptions options, bool dryRun)
        {
            var duplicate = snapshots
                .GroupBy(s => s.AppName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate != null)
            {
                throw new UpstreamImportException($"Two {duplicate.Key} databases were given; each app can be imported once");
            }

            var stages = _stages.OrderBy(s => s.Order).ToList();

            // Disposing a transaction that was not committed rolls it back, so every exit
            // below but the commit leaves the target untouched.
            using var connection = _database.OpenConnection();
            using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

            EnsureTargetIsEmpty(connection, transaction);

            var session = new ImportSession(connection, transaction, snapshots, options);

            foreach (var stage in stages)
            {
                stage.Run(session);
            }

            var tables = ImportReconciliation.Check(session, stages);
            var unbalanced = tables.Where(t => !t.Balanced).ToList();

            if (unbalanced.Any())
            {
                throw new ImportVerificationException(unbalanced);
            }

            var result = new ImportResult
            {
                Tables = tables,
                UnreadTables = snapshots.SelectMany(s => s.SkippedTables.Select(t => new UnreadTable(s.AppName, t))).ToList(),
                Skips = session.Skips.ToList(),
                Warnings = snapshots.SelectMany(s => s.Warnings).Concat(session.Warnings).ToList(),
                IdMap = session.IdMap
            };

            if (options.Strict && session.Skips.Count > 0)
            {
                transaction.Rollback();
                result.Outcome = ImportOutcome.SkippedRows;
            }
            else if (dryRun)
            {
                transaction.Rollback();
                result.Outcome = ImportOutcome.DryRun;
            }
            else
            {
                transaction.Commit();
                result.Outcome = ImportOutcome.Committed;
            }

            return result;
        }

        private static void EnsureTargetIsEmpty(IDbConnection connection, IDbTransaction transaction)
        {
            foreach (var table in TablesThatMustBeEmpty)
            {
                if (connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM \"{table}\"", transaction: transaction) > 0)
                {
                    throw new UpstreamImportException($"The target already holds data (table {table}). An import needs an empty Plattarr configuration: delete it and start again.");
                }
            }

            var previous = connection.ExecuteScalar<long>("SELECT COUNT(*) FROM \"Config\" WHERE \"Key\" = @Key", new { Key = ImportedFromKey }, transaction);

            if (previous > 0)
            {
                throw new UpstreamImportException("This configuration already holds an import. To import again, delete the configuration and start again.");
            }
        }
    }
}
