using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NzbDrone.Core.Profiles.Releases;

namespace NzbDrone.Core.Upstream
{
    // Release profiles affect whether a release may be grabbed. Copy them after tags and indexers
    // so both references can be remapped before the profile is written.
    public class ReleaseProfilesStage : IImportStage
    {
        public const string Table = "ReleaseProfiles";

        private static readonly string[] Apps = { UpstreamApps.Radarr, UpstreamApps.Sonarr };

        private readonly IReleaseProfileRepository _profiles;

        public ReleaseProfilesStage(IReleaseProfileRepository profiles)
        {
            _profiles = profiles;
        }

        public string Name => "Release profiles";

        public int Order => ImportOrder.ReleaseProfiles;

        public IReadOnlyList<SourceTableRef> Tables { get; } = Apps
            .Select(app => new SourceTableRef(app, Table))
            .ToArray();

        public void Run(ImportSession session)
        {
            foreach (var app in Apps)
            {
                var batch = new RowBatch<ReleaseProfile>();

                foreach (var row in session.Snapshot(app)?.Rows(Table) ?? Array.Empty<SourceRow>())
                {
                    var id = row.GetLong("Id");
                    var name = row.GetString("Name");

                    try
                    {
                        var indexerId = row.GetNullableInt("IndexerId") ?? 0;
                        long mappedIndexerId = 0;

                        if (indexerId > 0 && !session.IdMap.TryResolve(app, IndexersStage.IndexersTable, indexerId, out mappedIndexerId))
                        {
                            session.Skip(new ImportSkip(app, Table, id, name, SkipReason.UnmatchedIndexer, "The profile's assigned indexer was not imported."));
                            continue;
                        }

                        var tags = TagRemapper.Remap(app, row.GetJson("Tags"), session.IdMap, out var droppedTags);

                        if (droppedTags > 0)
                        {
                            session.Warn($"{app} release profile \"{name}\": {droppedTags} tag(s) that no longer exist were dropped.");
                        }

                        batch.Add(id, new ReleaseProfile
                        {
                            Name = name,
                            Enabled = row.GetBool("Enabled"),
                            Required = Terms(row, "Required"),
                            Ignored = Terms(row, "Ignored"),
                            IndexerId = indexerId > 0 ? checked((int)mappedIndexerId) : 0,
                            Tags = new HashSet<int>(tags),
                            AirDateRestriction = (row.GetNullableInt("AirDateRestriction") ?? 0) != 0,
                            AirDateGracePeriod = row.GetNullableInt("AirDateGracePeriod") ?? 0,
                            AllowSeasonPackWithoutAllEpisodesAired = (row.GetNullableInt("AllowSeasonPackWithoutAllEpisodesAired") ?? 0) != 0
                        });
                    }
                    catch (Exception ex) when (SourceDocuments.IsBad(ex))
                    {
                        session.SkipCorrupt(app, Table, id, name, "Release profile could not be read: " + SourceDocuments.Describe(ex));
                    }
                }

                batch.Write(session, app, Table, _profiles);
            }
        }

        private static List<string> Terms(SourceRow row, string column)
        {
            var text = row.GetString(column);

            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            var trimmed = text.Trim();

            if (trimmed.StartsWith("[", StringComparison.Ordinal) || trimmed.StartsWith("\"", StringComparison.Ordinal))
            {
                var document = row.GetJson(column);

                if (document is JArray array)
                {
                    return array.Select(item => item.Type == JTokenType.String
                            ? item.Value<string>()
                            : throw new CorruptSourceDataException($"Column {column} must contain strings"))
                        .ToList();
                }

                if (document?.Type == JTokenType.String)
                {
                    return SplitTerms(document.Value<string>());
                }

                throw new CorruptSourceDataException($"Column {column} must contain a string array");
            }

            // Older release profiles stored comma-separated text.
            return SplitTerms(trimmed);
        }

        private static List<string> SplitTerms(string text)
        {
            return text.Split(new[] { ',' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        }
    }
}
