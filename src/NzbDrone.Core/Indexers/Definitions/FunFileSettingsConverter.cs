using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NzbDrone.Core.Indexers.Definitions
{
    // Shared by a future stored-row migration and upstream import conversion.
    // Keep the conversion separate from activation until Cardigann behavior is proved.
    public static class FunFileSettingsConverter
    {
        public static string Convert(string settings)
        {
            if (JsonNode.Parse(settings) is not JsonObject document)
            {
                throw new ArgumentException("FunFile settings must be a JSON object", nameof(settings));
            }

            var username = Take(document, "username");
            var password = Take(document, "password");

            if (username == null || password == null)
            {
                throw new ArgumentException("FunFile settings must contain username and password", nameof(settings));
            }

            if (document.Any(p => string.Equals(p.Key, "definitionFile", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(p.Key, "extraFieldData", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("FunFile settings already contain Cardigann fields", nameof(settings));
            }

            document["definitionFile"] = "funfile";
            document["extraFieldData"] = new JsonObject
            {
                ["username"] = username,
                ["password"] = password
            };

            return document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        private static JsonNode Take(JsonObject document, string key)
        {
            var existing = document.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));

            if (existing.Key == null)
            {
                return null;
            }

            document.Remove(existing.Key);
            return existing.Value;
        }
    }
}
