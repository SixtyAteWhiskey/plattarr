using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;

namespace NzbDrone.Core.Test.Upstream
{
    // Builds a SQLite file from SQL statements, or from a schema dumped from a real
    // upstream database. Tests that pass their own statements use synthetic schemas
    // to exercise the read layer's mechanics (tolerance, version gate, copying);
    // tests that use CreateFromSchema get the true table and column set.
    public static class UpstreamTestDatabase
    {
        public static void Create(string path, params string[] statements)
        {
            var builder = new SQLiteConnectionStringBuilder
            {
                DataSource = path,
                Pooling = false
            };

            using var connection = new SQLiteConnection(builder.ConnectionString);
            connection.Open();

            foreach (var statement in statements)
            {
                using var command = connection.CreateCommand();
                command.CommandText = statement;
                command.ExecuteNonQuery();
            }
        }

        // Builds a database from a schema file dumped from a real upstream database
        // (Files/Upstream/*.schema.sql). The dump lists sqlite_sequence, which SQLite
        // creates itself for AUTOINCREMENT columns and refuses to create by hand.
        public static void CreateFromSchema(string path, string schemaFile, params string[] extraStatements)
        {
            var schema = File.ReadAllLines(schemaFile)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Where(line => !line.StartsWith("CREATE TABLE sqlite_sequence", StringComparison.Ordinal));

            Create(path, schema.Concat(extraStatements).ToArray());
        }
    }
}
