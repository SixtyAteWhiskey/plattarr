using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using Dapper;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.Upstream
{
    // Copy only portable settings that change the media loop. Passphrases, salts, device IDs and
    // other instance-bound values stay with their original installation.
    public class ConfigStage : IImportStage
    {
        public const string Table = "Config";

        private static readonly string[] Apps = { UpstreamApps.Radarr, UpstreamApps.Sonarr };

        private static readonly HashSet<string> BooleanKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AutoUnmonitorPreviouslyDownloadedMovies",
            "AutoUnmonitorPreviouslyDownloadedEpisodes",
            "CleanupMetadataImages",
            "EnableCompletedDownloadHandling"
        };

        private readonly IConfigRepository _configs;

        public ConfigStage(IConfigRepository configs)
        {
            _configs = configs;
        }

        public string Name => "Portable settings";

        public int Order => ImportOrder.Config;

        public IReadOnlyList<SourceTableRef> Tables { get; } = Apps
            .Select(app => new SourceTableRef(app, Table))
            .ToArray();

        public void Run(ImportSession session)
        {
            var selected = new Dictionary<string, ConfigValue>(StringComparer.OrdinalIgnoreCase);
            var accepted = new List<(string App, long SourceId)>();

            // Radarr is first so its value wins if both source apps set a shared key differently.
            foreach (var app in Apps)
            {
                foreach (var row in session.Snapshot(app)?.Rows(Table) ?? Array.Empty<SourceRow>())
                {
                    var id = row.GetLong("Id");
                    var key = row.GetString("Key");

                    if (!IsPortable(key))
                    {
                        session.Skip(new ImportSkip(app, Table, id, key ?? "<missing key>", SkipReason.Instance, "Instance-specific settings are not copied."));
                        continue;
                    }

                    string value;

                    try
                    {
                        value = Normalize(key, row.GetString("Value"));
                    }
                    catch (CorruptSourceDataException ex)
                    {
                        session.SkipCorrupt(app, Table, id, key, ex.Message);
                        continue;
                    }

                    if (selected.TryGetValue(key, out var previous))
                    {
                        if (!string.Equals(previous.Value, value, StringComparison.Ordinal))
                        {
                            session.Skip(new ImportSkip(app, Table, id, key, SkipReason.ConfigConflict, $"{previous.App}'s value was selected for the shared setting."));
                            session.Warn($"Both {previous.App} and {app} set {key} differently; {previous.App}'s value was kept.");
                            continue;
                        }
                    }
                    else
                    {
                        selected.Add(key, new ConfigValue(app, value));
                    }

                    accepted.Add((app, id));
                }
            }

            WriteValues(session, selected);

            foreach (var (app, _) in accepted)
            {
                session.Imported(app, Table);
            }
        }

        private void WriteValues(ImportSession session, IReadOnlyDictionary<string, ConfigValue> values)
        {
            var current = session.Connection.Query<Config>("SELECT \"Id\", \"Key\", \"Value\" FROM \"Config\"", transaction: session.Transaction)
                .ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);
            var updates = new List<Config>();
            var inserts = new List<Config>();

            foreach (var (key, selected) in values)
            {
                if (current.TryGetValue(key, out var existing))
                {
                    existing.Value = selected.Value;
                    updates.Add(existing);
                }
                else
                {
                    inserts.Add(new Config { Key = key, Value = selected.Value });
                }
            }

            _configs.UpdateMany(updates, session.Connection, session.Transaction);
            _configs.InsertMany(inserts, session.Connection, session.Transaction);
        }

        private static bool IsPortable(string key)
        {
            return BooleanKeys.Contains(key ?? string.Empty)
                || string.Equals(key, "MaximumSize", StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string key, string value)
        {
            if (BooleanKeys.Contains(key))
            {
                var text = value?.Trim();

                if (bool.TryParse(text, out var parsedBoolean))
                {
                    return parsedBoolean ? "true" : "false";
                }

                if (text == "1")
                {
                    return "true";
                }

                if (text == "0")
                {
                    return "false";
                }

                throw new CorruptSourceDataException($"Config value for {key} is not a boolean");
            }

            if (string.Equals(key, "MaximumSize", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maximumSize))
            {
                return maximumSize.ToString(CultureInfo.InvariantCulture);
            }

            throw new CorruptSourceDataException($"Config value for {key} is not a supported setting value");
        }

        private sealed record ConfigValue(string App, string Value);
    }
}
