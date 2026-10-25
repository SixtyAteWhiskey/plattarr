using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Upstream
{
    // Converts a Radarr download client's Settings JSON from the old shape (movie
    // named category and priority keys) to the shape migration 003 gave Plattarr
    // (a moduleCategories list). The rules are migration 003's, so a client
    // imported from Radarr and a client that was migrated in place end up with
    // identical settings; ModuleDownloadClientCategoriesFixture and
    // UpstreamClientSettingsFixture pin the two together.
    //
    //   movieCategory         -> moduleCategories[Movies].category
    //   movieImportedCategory -> moduleCategories[Movies].importedCategory
    //   recentMoviePriority   -> moduleCategories[Movies].recentPriority
    //   olderMoviePriority    -> moduleCategories[Movies].olderPriority
    //
    // The user's own category is kept, and a value that is not stored stays
    // unstored. The old keys are removed.
    //
    // Only implementations that exist in this build are listed. Migration 003 also
    // covered the clients pruned on 2026-10-22 (docs/pruned-providers.md); restoring
    // one means adding its row here, with the key names from migration 003.
    public static class DownloadClientSettingsConverter
    {
        private const string ListKey = "moduleCategories";

        private static readonly HashSet<string> MovieNamedClients = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "QBittorrent",
            "Sabnzbd"
        };

        public static bool Handles(string implementation)
        {
            return MovieNamedClients.Contains(implementation);
        }

        // The new Settings JSON, or the input unchanged when it holds nothing to
        // move (empty, or not a JSON object).
        public static string Convert(string implementation, string settings)
        {
            return Convert(implementation, settings, MediaKind.Movies);
        }

        public static string Convert(string implementation, string settings, MediaKind kind)
        {
            if (!Handles(implementation))
            {
                throw new ArgumentException("This download client is not one the importer converts", nameof(implementation));
            }

            if (string.IsNullOrWhiteSpace(settings))
            {
                return settings;
            }

            JsonNode parsed;

            try
            {
                parsed = JsonNode.Parse(settings);
            }
            catch (JsonException ex)
            {
                throw new CorruptSourceDataException("A download client's settings are not valid JSON", ex);
            }

            if (parsed is not JsonObject json)
            {
                return settings;
            }

            var prefix = kind switch
            {
                MediaKind.Movies => "movie",
                MediaKind.Tv => "tv",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only Radarr and Sonarr category settings are imported")
            };
            var category = Take(json, prefix + "Category");
            var imported = Take(json, prefix + "ImportedCategory");
            var recent = Take(json, "recent" + prefix + "Priority");
            var older = Take(json, "older" + prefix + "Priority");

            // Settings already in the new shape keep their list; the old keys are still dropped.
            var hasList = json.Any(p => string.Equals(p.Key, ListKey, StringComparison.OrdinalIgnoreCase));

            if (!hasList)
            {
                var moduleCategory = new JsonObject { ["kind"] = kind.ToString().ToLowerInvariant() };

                SetIfStored(moduleCategory, "category", category);
                SetIfStored(moduleCategory, "importedCategory", imported);
                SetIfStored(moduleCategory, "recentPriority", recent);
                SetIfStored(moduleCategory, "olderPriority", older);

                json[ListKey] = new JsonArray(moduleCategory);
            }

            return json.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        // Removes the key (in any case, as the settings reader is case-insensitive) and returns its value.
        private static JsonNode Take(JsonObject json, string key)
        {
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
    }
}
