using System;
using System.Data;
using FluentMigrator;
using NzbDrone.Common.Exceptions;

namespace NzbDrone.Core.Datastore.Migration
{
    // Plattarr's chain restarted at 1 in porting step 8. A database from Radarr (or
    // any other *arr app), or from a Plattarr build before step 8, already has a
    // version 1 under another name (Radarr's is "InitialSetup"), so the runner would
    // apply nothing and the app would start against the wrong schema. Refuse any
    // version 1 that isn't Plattarr's. Upstream data comes in through the importer.
    [Maintenance(MigrationStage.BeforeAll, TransactionBehavior.None)]
    public class PreviousSchemaCheck : ForwardOnlyMigration
    {
        public static readonly string PlattarrInitialDescription = nameof(plattarr_initial);

        public override void Up()
        {
            IfDatabase(ProcessorIdConstants.SQLite).Execute.WithConnection((conn, tran) =>
                Check(conn, tran, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'VersionInfo'"));

            IfDatabase(ProcessorIdConstants.PostgreSQL).Execute.WithConnection((conn, tran) =>
                Check(conn, tran, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = current_schema() AND table_name = 'VersionInfo'"));
        }

        private static void Check(IDbConnection conn, IDbTransaction tran, string versionTableExistsQuery)
        {
            if (Count(conn, tran, versionTableExistsQuery) == 0)
            {
                return;
            }

            var foreignRows = Count(conn, tran, $"SELECT COUNT(*) FROM \"VersionInfo\" WHERE \"Version\" = 1 AND (\"Description\" IS NULL OR \"Description\" <> '{PlattarrInitialDescription}')");

            if (foreignRows > 0)
            {
                var latest = Count(conn, tran, "SELECT MAX(\"Version\") FROM \"VersionInfo\"");

                throw new PreviousSchemaException(latest);
            }
        }

        private static long Count(IDbConnection conn, IDbTransaction tran, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tran;
            cmd.CommandText = sql;

            var result = cmd.ExecuteScalar();

            return result == null || result is DBNull ? 0 : Convert.ToInt64(result);
        }
    }

    public class PreviousSchemaException : NzbDroneException
    {
        public PreviousSchemaException(long version)
            : base("This database was created by Radarr or by a Plattarr build from before the schema reset (schema version {0}). " +
                   "Plattarr can't upgrade it. Start Plattarr with an empty config folder; existing Radarr and Prowlarr data will come in through the importer.",
                   version)
        {
        }
    }
}
