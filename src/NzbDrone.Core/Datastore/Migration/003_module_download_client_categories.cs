using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    // Porting step 9i (docs/module-interface.md, "Download client settings"): a download client
    // routes by category per media type. The movie-named category and priority settings move
    // into a list with one entry per media type, `moduleCategories`, and the Movies entry takes
    // the stored values, so no client changes behavior.
    //
    //   movieCategory         -> moduleCategories[Movies].category
    //   movieImportedCategory -> moduleCategories[Movies].importedCategory
    //   recentMoviePriority   -> moduleCategories[Movies].recentPriority
    //   olderMoviePriority    -> moduleCategories[Movies].olderPriority
    //
    // Four clients stored the category under another name, and one stored its priorities under
    // another: NZBVortex and both Download Station clients kept it in tvCategory (the upstream
    // name for the field in Sonarr's copy of the code), Freebox Download and Hadouken in category,
    // and Freebox Download kept its priorities in recentPriority and olderPriority.
    //
    // A value that is not stored stays unstored; the settings class fills the gap with its default
    // when the row is read. An empty category stays empty: an empty category means the client takes
    // every download, and the migration does not change that into the new default category.
    //
    // The old keys are removed. Only the Settings column changes, so the migration is the same on
    // SQLite and Postgres.
    [Migration(3)]
    public class module_download_client_categories : NzbDroneMigrationBase
    {
        private const string ListKey = "moduleCategories";

        private static readonly Dictionary<string, OldNames> Clients = new Dictionary<string, OldNames>(StringComparer.OrdinalIgnoreCase)
        {
            { "QBittorrent", OldNames.Movie },
            { "Deluge", OldNames.Movie },
            { "RTorrent", OldNames.Movie },
            { "UTorrent", OldNames.Movie },
            { "Transmission", OldNames.Movie },
            { "Vuze", OldNames.Movie },
            { "Sabnzbd", OldNames.Movie },
            { "Nzbget", OldNames.Movie },
            { "NzbVortex", new OldNames("tvCategory", null, "recentMoviePriority", "olderMoviePriority") },
            { "TorrentDownloadStation", new OldNames("tvCategory", null, null, null) },
            { "UsenetDownloadStation", new OldNames("tvCategory", null, null, null) },
            { "TorrentFreeboxDownload", new OldNames("category", null, "recentPriority", "olderPriority") },
            { "Hadouken", new OldNames("category", null, null, null) }
        };

        protected override void MainDbUpgrade()
        {
            Execute.WithConnection(MigrateSettings);
        }

        private static void MigrateSettings(IDbConnection conn, IDbTransaction tran)
        {
            var rows = new List<(long Id, string Implementation, string Settings)>();

            using (var select = conn.CreateCommand())
            {
                select.Transaction = tran;
                select.CommandText = "SELECT \"Id\", \"Implementation\", \"Settings\" FROM \"DownloadClients\"";

                using var reader = select.ExecuteReader();

                while (reader.Read())
                {
                    rows.Add((reader.GetInt64(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
                }
            }

            foreach (var (id, implementation, settings) in rows)
            {
                if (!Clients.TryGetValue(implementation, out var names) || string.IsNullOrWhiteSpace(settings))
                {
                    continue;
                }

                var migrated = Migrate(settings, names);

                if (migrated == null)
                {
                    continue;
                }

                using var update = conn.CreateCommand();
                update.Transaction = tran;
                update.CommandText = "UPDATE \"DownloadClients\" SET \"Settings\" = @settings WHERE \"Id\" = @id";
                AddParameter(update, "@settings", migrated);
                AddParameter(update, "@id", id);
                update.ExecuteNonQuery();
            }
        }

        // Returns the new Settings JSON, or null when the row has nothing to move.
        private static string Migrate(string settings, OldNames names)
        {
            if (JsonNode.Parse(settings) is not JsonObject json)
            {
                return null;
            }

            var category = Take(json, names.Category);
            var imported = Take(json, names.ImportedCategory);
            var recent = Take(json, names.RecentPriority);
            var older = Take(json, names.OlderPriority);

            // Settings already in the new shape keep their list; the old keys are still dropped.
            var hasList = json.Any(p => string.Equals(p.Key, ListKey, StringComparison.OrdinalIgnoreCase));

            if (!hasList)
            {
                var movies = new JsonObject { ["kind"] = "movies" };

                SetIfStored(movies, "category", category);
                SetIfStored(movies, "importedCategory", imported);
                SetIfStored(movies, "recentPriority", recent);
                SetIfStored(movies, "olderPriority", older);

                json[ListKey] = new JsonArray(movies);
            }

            return json.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        // Removes the key (in any case, as the settings reader is case-insensitive) and returns its value.
        private static JsonNode Take(JsonObject json, string key)
        {
            if (key == null)
            {
                return null;
            }

            var existing = json.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));

            if (existing.Key == null)
            {
                return null;
            }

            json.Remove(existing.Key);

            return existing.Value;
        }

        private static void SetIfStored(JsonObject target, string key, JsonNode value)
        {
            if (value != null)
            {
                target[key] = value.DeepClone();
            }
        }

        private static void AddParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        private sealed class OldNames
        {
            public static readonly OldNames Movie = new OldNames("movieCategory", "movieImportedCategory", "recentMoviePriority", "olderMoviePriority");

            public OldNames(string category, string importedCategory, string recentPriority, string olderPriority)
            {
                Category = category;
                ImportedCategory = importedCategory;
                RecentPriority = recentPriority;
                OlderPriority = olderPriority;
            }

            public string Category { get; }
            public string ImportedCategory { get; }
            public string RecentPriority { get; }
            public string OlderPriority { get; }
        }
    }
}
