using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using Dapper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Converters;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Upstream;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Upstream
{
    // Custom formats, quality profiles, quality definitions and delay profiles of Radarr and Sonarr
    // (docs/importer.md, "Profiles and custom formats"). They are one stage because each reads
    // what the one before it wrote: profiles score custom formats, and nothing else in the
    // import needs them in between.
    //
    // Every quality, language and group ID in a stored document goes through the app's catalog,
    // and the document is then parsed by the same converters the repositories use, so a shape
    // the target cannot read fails here, on one row, not later on a page load.
    //
    // Two sources (docs/importer.md, "Merging identical items"): Radarr's rows are planned first,
    // then Sonarr's. A Sonarr custom format or profile identical to a Radarr one (same name, same
    // content once both are in catalog IDs) becomes that row, which then applies to both media
    // types; one with a Radarr name and other content is written as "<name> (Sonarr)" and
    // reported. Quality definitions are one row per catalog quality (QualityDefinitionService
    // reads them with SingleOrDefault), so Radarr's sizes win and a Sonarr difference is
    // reported. Delay profiles are global: Sonarr's default row has no home when Radarr's exists.
    public class ProfilesStage : IImportStage
    {
        public const string CustomFormatsTable = "CustomFormats";
        public const string QualityProfilesTable = "QualityProfiles";
        public const string QualityDefinitionsTable = "QualityDefinitions";
        public const string DelayProfilesTable = "DelayProfiles";

        // Profile-local quality group IDs start here upstream and keep their value.
        private const int FirstGroupId = 1000;

        // The default delay profile's Order (migration 001 inserts the target's copy).
        private const int DefaultDelayOrder = int.MaxValue;

        // Planned in this order; the first app's row wins a merge.
        private static readonly (string App, MediaKind Kind)[] Sources =
        {
            (UpstreamApps.Radarr, MediaKind.Movies),
            (UpstreamApps.Sonarr, MediaKind.Tv)
        };

        private static readonly EmbeddedDocumentConverter<List<ICustomFormatSpecification>> SpecificationsParser =
            new EmbeddedDocumentConverter<List<ICustomFormatSpecification>>(new CustomFormatSpecificationListConverter());

        private static readonly EmbeddedDocumentConverter<List<QualityProfileQualityItem>> ItemsParser =
            new EmbeddedDocumentConverter<List<QualityProfileQualityItem>>(new QualityIntConverter());

        private static readonly EmbeddedDocumentConverter<List<ProfileFormatItem>> FormatItemsParser =
            new EmbeddedDocumentConverter<List<ProfileFormatItem>>(new CustomFormatIntConverter());

        private readonly ICustomFormatRepository _customFormats;
        private readonly IQualityProfileRepository _qualityProfiles;
        private readonly IQualityDefinitionRepository _qualityDefinitions;
        private readonly IDelayProfileRepository _delayProfiles;
        private readonly IEnumerable<IUpstreamCatalog> _catalogs;

        public ProfilesStage(
            ICustomFormatRepository customFormats,
            IQualityProfileRepository qualityProfiles,
            IQualityDefinitionRepository qualityDefinitions,
            IDelayProfileRepository delayProfiles,
            IEnumerable<IUpstreamCatalog> catalogs)
        {
            _customFormats = customFormats;
            _qualityProfiles = qualityProfiles;
            _qualityDefinitions = qualityDefinitions;
            _delayProfiles = delayProfiles;
            _catalogs = catalogs;
        }

        public string Name => "Profiles and custom formats";

        public int Order => ImportOrder.Profiles;

        public IReadOnlyList<SourceTableRef> Tables { get; } = Sources
            .SelectMany(s => new[]
            {
                new SourceTableRef(s.App, CustomFormatsTable),
                new SourceTableRef(s.App, QualityProfilesTable),
                new SourceTableRef(s.App, QualityDefinitionsTable),
                new SourceTableRef(s.App, DelayProfilesTable)
            })
            .ToArray();

        public void Run(ImportSession session)
        {
            var sources = Sources
                .Select(s => (s.App, s.Kind, Snapshot: session.Snapshot(s.App)))
                .Where(s => s.Snapshot != null)
                .Select(s => new Source(s.App, s.Kind, s.Snapshot, _catalogs.For(s.App)))
                .ToList();

            if (sources.Count == 0)
            {
                return;
            }

            ImportCustomFormats(session, sources);
            ImportQualityProfiles(session, sources);
            ImportQualityDefinitions(session, sources);
            ImportDelayProfiles(session, sources);
        }

        private void ImportCustomFormats(ImportSession session, List<Source> sources)
        {
            var planned = new Planned<CustomFormat>();

            foreach (var source in sources)
            {
                foreach (var row in source.Snapshot.Rows(CustomFormatsTable))
                {
                    var id = row.GetLong("Id");
                    var name = row.GetString("Name");

                    try
                    {
                        var model = new CustomFormat
                        {
                            Name = name,
                            IncludeCustomFormatWhenRenaming = row.GetBool("IncludeCustomFormatWhenRenaming"),
                            Specifications = ParseSpecifications(row.GetJson("Specifications"), source),
                            AppliesTo = new List<MediaKind> { source.Kind }
                        };

                        planned.Add(session, source, CustomFormatsTable, id, model, m => m.Name, (m, n) => m.Name = n, CustomFormatContent);
                    }
                    catch (Exception ex) when (SourceDocuments.IsBad(ex))
                    {
                        session.Skip(new ImportSkip(source.App, CustomFormatsTable, id, name, SkipReason.Corrupt, "Specifications could not be read: " + SourceDocuments.Describe(ex)));
                    }
                }
            }

            planned.Write(session, CustomFormatsTable, _customFormats);
        }

        // Only a language condition and, for Sonarr, an indexer flag condition hold a value that
        // differs between apps. The other specifications use Radarr's vocabulary, which is the
        // global one (Sonarr's are the same), so they are copied. A Sonarr condition Plattarr does
        // not have (release type) fails the parse and skips the format.
        private static List<ICustomFormatSpecification> ParseSpecifications(JToken specifications, Source source)
        {
            if (specifications is not JArray array)
            {
                throw new CorruptSourceDataException("A specifications column does not hold a JSON array");
            }

            var clone = (JArray)array.DeepClone();

            foreach (var item in clone.OfType<JObject>())
            {
                var type = item.Value<string>("type");

                if (string.Equals(type, nameof(LanguageSpecification), StringComparison.Ordinal))
                {
                    var value = IntegerValue(item, "A language condition has no integer language ID");

                    value.Value = CatalogRemapper.LanguageId(value.Value.Value<int>(), source.Catalog);
                }
                else if (string.Equals(type, nameof(IndexerFlagSpecification), StringComparison.Ordinal) && source.App == UpstreamApps.Sonarr)
                {
                    // Sonarr's flag bits differ from the global (Radarr) ones from bit 8 up (F4).
                    var value = IntegerValue(item, "An indexer flag condition has no integer flag");

                    value.Value = (int)SonarrFlags(value.Value.Value<int>());
                }
            }

            return SpecificationsParser.Parse(clone.ToString(Formatting.None));
        }

        private static JProperty IntegerValue(JObject specification, string problem)
        {
            var value = (specification["body"] as JObject)?.Property("value", StringComparison.OrdinalIgnoreCase);

            if (value == null || value.Value.Type != JTokenType.Integer)
            {
                throw new CorruptSourceDataException(problem);
            }

            return value;
        }

        public static IndexerFlags SonarrFlags(int upstreamFlags)
        {
            try
            {
                return SonarrIndexerFlags.ToGlobal(upstreamFlags);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new CorruptSourceDataException("A stored indexer flag is not one Sonarr has", ex);
            }
        }

        private static string CustomFormatContent(CustomFormat format)
        {
            return string.Concat(format.IncludeCustomFormatWhenRenaming, "|", Serialize(SpecificationsParser, format.Specifications));
        }

        private void ImportQualityProfiles(ImportSession session, List<Source> sources)
        {
            var planned = new Planned<QualityProfile>();

            foreach (var source in sources)
            {
                foreach (var row in source.Snapshot.Rows(QualityProfilesTable))
                {
                    var id = row.GetLong("Id");
                    var name = row.GetString("Name");

                    try
                    {
                        var items = RemapItems(row.GetJson("Items"), source.Catalog, out var groups, out var qualities);

                        // Sonarr's profiles list Sonarr's qualities only. A TV profile in Plattarr
                        // ranks catalog qualities, so the others are added, not allowed, at the
                        // bottom (the TV parser never produces them).
                        if (source.App == UpstreamApps.Sonarr)
                        {
                            AddMissingQualities(items, qualities);
                        }

                        var model = new QualityProfile
                        {
                            Name = name,
                            Cutoff = RemapCutoff(row.GetInt("Cutoff"), groups, source.Catalog),
                            Items = ItemsParser.Parse(items.ToString(Formatting.None)),
                            Language = RemapProfileLanguage(row.GetNullableInt("Language"), source.Catalog),
                            FormatItems = RemapFormatItems(session, source, id, name, row.GetJson("FormatItems")),
                            UpgradeAllowed = row.GetBool("UpgradeAllowed"),
                            MinFormatScore = row.GetInt("MinFormatScore"),
                            CutoffFormatScore = row.GetInt("CutoffFormatScore"),
                            MinUpgradeFormatScore = row.GetInt("MinUpgradeFormatScore"),
                            AppliesTo = new List<MediaKind> { source.Kind }
                        };

                        planned.Add(session, source, QualityProfilesTable, id, model, m => m.Name, (m, n) => m.Name = n, ProfileContent);
                    }
                    catch (Exception ex) when (SourceDocuments.IsBad(ex))
                    {
                        session.Skip(new ImportSkip(source.App, QualityProfilesTable, id, name, SkipReason.Corrupt, "Profile could not be read: " + SourceDocuments.Describe(ex)));
                    }
                }
            }

            planned.Write(session, QualityProfilesTable, _qualityProfiles);
        }

        private static string ProfileContent(QualityProfile profile)
        {
            var formats = string.Join(",", profile.FormatItems.Select(f => $"{f.Format?.Id}:{f.Score}"));

            return string.Join(
                "|",
                profile.Cutoff,
                profile.Language?.Id,
                profile.UpgradeAllowed,
                profile.MinFormatScore,
                profile.CutoffFormatScore,
                profile.MinUpgradeFormatScore,
                formats,
                Serialize(ItemsParser, profile.Items));
        }

        // A leaf item is {"quality": id, "items": [], "allowed": bool}; a group is
        // {"id": 1000+, "name": ..., "items": [leaf, ...], "allowed": bool}.
        private static JArray RemapItems(JToken items, IUpstreamCatalog catalog, out HashSet<int> groups, out HashSet<int> qualities)
        {
            groups = new HashSet<int>();
            qualities = new HashSet<int>();

            if (items is not JArray array)
            {
                throw new CorruptSourceDataException("A profile's items column does not hold a JSON array");
            }

            var clone = (JArray)array.DeepClone();

            foreach (var item in clone.OfType<JObject>())
            {
                RemapItemQuality(item, catalog, qualities);

                if (item["items"] is JArray members)
                {
                    foreach (var member in members.OfType<JObject>())
                    {
                        RemapItemQuality(member, catalog, qualities);
                    }
                }

                if (item["id"]?.Type == JTokenType.Integer)
                {
                    groups.Add(item.Value<int>("id"));
                }
            }

            return clone;
        }

        private static void RemapItemQuality(JObject item, IUpstreamCatalog catalog, HashSet<int> qualities)
        {
            var quality = item.Property("quality", StringComparison.OrdinalIgnoreCase);

            if (quality == null || quality.Value.Type == JTokenType.Null)
            {
                return;
            }

            if (quality.Value.Type != JTokenType.Integer)
            {
                throw new CorruptSourceDataException("A profile item has a quality ID that is not an integer");
            }

            var catalogId = CatalogRemapper.QualityId(quality.Value.Value<int>(), catalog);

            quality.Value = catalogId;
            qualities.Add(catalogId);
        }

        // Lowest weight first, inserted before the profile's own items (the first item ranks lowest).
        private static void AddMissingQualities(JArray items, HashSet<int> present)
        {
            var missing = Quality.DefaultQualityDefinitions
                .Where(d => !present.Contains(d.Quality.Id))
                .OrderByDescending(d => d.Weight)
                .ThenByDescending(d => d.Quality.Id);

            foreach (var definition in missing)
            {
                items.Insert(0, new JObject
                {
                    ["quality"] = definition.Quality.Id,
                    ["items"] = new JArray(),
                    ["allowed"] = false
                });
            }
        }

        // The cutoff is a quality ID, or the ID of one of the profile's own groups.
        private static int RemapCutoff(int cutoff, HashSet<int> groups, IUpstreamCatalog catalog)
        {
            if (cutoff >= FirstGroupId)
            {
                if (!groups.Contains(cutoff))
                {
                    throw new CorruptSourceDataException("A profile's cutoff names a group the profile does not have");
                }

                return cutoff;
            }

            return CatalogRemapper.QualityId(cutoff, catalog);
        }

        // Sonarr's profiles have no language (Sonarr 4 moved it to custom formats): Unknown, as for
        // a Radarr profile without one.
        private static Language RemapProfileLanguage(int? language, IUpstreamCatalog catalog)
        {
            return language.HasValue ? (Language)CatalogRemapper.LanguageId(language.Value, catalog) : Language.Unknown;
        }

        // A score for a custom format that was not imported (deleted upstream, or skipped as
        // corrupt) has nothing to apply to; it is dropped and counted in one warning.
        private static List<ProfileFormatItem> RemapFormatItems(ImportSession session, Source source, long profileId, string profileName, JToken formatItems)
        {
            if (formatItems == null || formatItems.Type == JTokenType.Null)
            {
                return new List<ProfileFormatItem>();
            }

            if (formatItems is not JArray array)
            {
                throw new CorruptSourceDataException("A profile's format items column does not hold a JSON array");
            }

            var clone = new JArray();
            var dropped = 0;

            foreach (var item in array.OfType<JObject>())
            {
                var format = item.Property("format", StringComparison.OrdinalIgnoreCase);

                if (format == null || format.Value.Type != JTokenType.Integer)
                {
                    throw new CorruptSourceDataException("A profile's format item has no integer custom format ID");
                }

                if (!session.IdMap.TryResolve(source.App, CustomFormatsTable, format.Value.Value<long>(), out var target))
                {
                    dropped++;
                    continue;
                }

                var mapped = (JObject)item.DeepClone();

                mapped.Property("format", StringComparison.OrdinalIgnoreCase).Value = target;
                clone.Add(mapped);
            }

            if (dropped > 0)
            {
                session.Warn($"Quality profile \"{profileName}\" ({source.App} {profileId}): dropped {dropped} score(s) for custom formats that were not imported");
            }

            return FormatItemsParser.Parse(clone.ToString(Formatting.None));
        }

        private void ImportQualityDefinitions(ImportSession session, List<Source> sources)
        {
            var models = new List<QualityDefinition>();
            var byQuality = new Dictionary<int, int>();
            var rows = new List<(string App, long SourceId, int Index)>();
            var differing = new List<string>();

            foreach (var source in sources)
            {
                foreach (var row in source.Snapshot.Rows(QualityDefinitionsTable))
                {
                    var id = row.GetLong("Id");
                    var title = row.GetString("Title");

                    try
                    {
                        var quality = CatalogRemapper.QualityId(row.GetInt("Quality"), source.Catalog);
                        var model = new QualityDefinition
                        {
                            Quality = (Quality)quality,
                            Title = title,
                            MinSize = row.GetNullableDouble("MinSize"),
                            MaxSize = row.GetNullableDouble("MaxSize"),
                            PreferredSize = row.GetNullableDouble("PreferredSize")
                        };

                        if (byQuality.TryGetValue(quality, out var index))
                        {
                            if (!SameSizes(models[index], model))
                            {
                                differing.Add(title);
                            }
                        }
                        else
                        {
                            index = models.Count;
                            byQuality[quality] = index;
                            models.Add(model);
                        }

                        rows.Add((source.App, id, index));
                    }
                    catch (Exception ex) when (SourceDocuments.IsBad(ex))
                    {
                        session.Skip(new ImportSkip(source.App, QualityDefinitionsTable, id, title, SkipReason.Corrupt, "Quality definition could not be read: " + SourceDocuments.Describe(ex)));
                    }
                }
            }

            _qualityDefinitions.InsertMany(models, session.Connection, session.Transaction);

            foreach (var (app, sourceId, index) in rows)
            {
                session.IdMap.Record(app, QualityDefinitionsTable, sourceId, models[index].Id);
                session.Imported(app, QualityDefinitionsTable);
            }

            if (differing.Count > 0)
            {
                session.Warn($"Size limits are one setting per quality in Plattarr. Sonarr's differ from Radarr's for {differing.Count} quality definitions and Radarr's were kept: {string.Join(", ", differing)}. Check Settings > Quality after the import.");
            }
        }

        private static bool SameSizes(QualityDefinition left, QualityDefinition right)
        {
            return Nullable.Equals(left.MinSize, right.MinSize) &&
                   Nullable.Equals(left.MaxSize, right.MaxSize) &&
                   Nullable.Equals(left.PreferredSize, right.PreferredSize);
        }

        // The default profile (Order = int.MaxValue) already exists in a migrated target and is
        // updated in place by the first source's default; every other row is inserted. Sonarr's
        // profiles go after Radarr's (their Order is shifted past Radarr's highest), and one that
        // matches a Radarr profile exactly becomes that profile.
        private void ImportDelayProfiles(ImportSession session, List<Source> sources)
        {
            var models = new List<DelayProfile>();
            var modelApps = new List<string>();
            var updated = new HashSet<int>();
            var rows = new List<(string App, long SourceId, int Index)>();

            var targetDefaultId = FindTargetDefaultDelayProfile(session.Connection, session.Transaction);
            var defaultIndex = (int?)null;
            var orderOffset = 0;

            foreach (var source in sources)
            {
                var highestOrder = 0;

                foreach (var row in source.Snapshot.Rows(DelayProfilesTable))
                {
                    var id = row.GetLong("Id");

                    try
                    {
                        var model = new DelayProfile
                        {
                            EnableUsenet = row.GetBool("EnableUsenet"),
                            EnableTorrent = row.GetBool("EnableTorrent"),
                            PreferredProtocol = (DownloadProtocol)row.GetInt("PreferredProtocol"),
                            UsenetDelay = row.GetInt("UsenetDelay"),
                            TorrentDelay = row.GetInt("TorrentDelay"),
                            Order = row.GetInt("Order"),
                            BypassIfHighestQuality = row.GetBool("BypassIfHighestQuality"),
                            BypassIfAboveCustomFormatScore = row.GetBool("BypassIfAboveCustomFormatScore"),
                            MinimumCustomFormatScore = row.GetNullableInt("MinimumCustomFormatScore") ?? 0,
                            Tags = new HashSet<int>(TagRemapper.Remap(source.App, row.GetJson("Tags"), session.IdMap, out var dropped))
                        };

                        if (dropped > 0)
                        {
                            session.Warn($"Delay profile ({source.App} {id}): dropped {dropped} tag(s) that were not imported");
                        }

                        if (model.Order == DefaultDelayOrder)
                        {
                            if (defaultIndex.HasValue)
                            {
                                var kept = models[defaultIndex.Value];

                                if (SameDelay(kept, model))
                                {
                                    rows.Add((source.App, id, defaultIndex.Value));
                                }
                                else
                                {
                                    session.Skip(new ImportSkip(source.App, DelayProfilesTable, id, null, SkipReason.ConfigConflict, $"The default delay profile is one setting in Plattarr; the {sources[0].App} one was kept"));
                                }

                                continue;
                            }

                            if (targetDefaultId.HasValue)
                            {
                                model.Id = targetDefaultId.Value;
                                updated.Add(models.Count);
                            }

                            defaultIndex = models.Count;
                            rows.Add((source.App, id, models.Count));
                            models.Add(model);
                            modelApps.Add(source.App);
                            continue;
                        }

                        highestOrder = Math.Max(highestOrder, model.Order);

                        // Only another app's profile can be the same row; two of one app stay two.
                        var others = Enumerable.Range(0, models.Count).Where(i => modelApps[i] != source.App && models[i].Order != DefaultDelayOrder).ToList();
                        var same = others.FirstOrDefault(i => SameDelay(models[i], model) && models[i].Tags.SetEquals(model.Tags), -1);

                        if (same >= 0)
                        {
                            rows.Add((source.App, id, same));
                            continue;
                        }

                        if (others.Any(i => models[i].Tags.Overlaps(model.Tags)))
                        {
                            session.Warn($"Delay profile ({source.App} {id}) shares a tag with a delay profile of another app. Delay profiles apply to every media type in Plattarr, so the first in order wins for that tag; check Settings > Profiles after the import.");
                        }

                        model.Order += orderOffset;
                        rows.Add((source.App, id, models.Count));
                        models.Add(model);
                        modelApps.Add(source.App);
                    }
                    catch (Exception ex) when (SourceDocuments.IsBad(ex))
                    {
                        session.Skip(new ImportSkip(source.App, DelayProfilesTable, id, null, SkipReason.Corrupt, "Delay profile could not be read: " + SourceDocuments.Describe(ex)));
                    }
                }

                orderOffset += highestOrder;
            }

            _delayProfiles.UpdateMany(models.Where((_, i) => updated.Contains(i)).ToList(), session.Connection, session.Transaction);
            _delayProfiles.InsertMany(models.Where((_, i) => !updated.Contains(i)).ToList(), session.Connection, session.Transaction);

            foreach (var (app, sourceId, index) in rows)
            {
                session.IdMap.Record(app, DelayProfilesTable, sourceId, models[index].Id);
                session.Imported(app, DelayProfilesTable);
            }
        }

        private static bool SameDelay(DelayProfile left, DelayProfile right)
        {
            return left.EnableUsenet == right.EnableUsenet &&
                   left.EnableTorrent == right.EnableTorrent &&
                   left.PreferredProtocol == right.PreferredProtocol &&
                   left.UsenetDelay == right.UsenetDelay &&
                   left.TorrentDelay == right.TorrentDelay &&
                   left.BypassIfHighestQuality == right.BypassIfHighestQuality &&
                   left.BypassIfAboveCustomFormatScore == right.BypassIfAboveCustomFormatScore &&
                   left.MinimumCustomFormatScore == right.MinimumCustomFormatScore;
        }

        private static int? FindTargetDefaultDelayProfile(IDbConnection connection, IDbTransaction transaction)
        {
            return connection.QueryFirstOrDefault<int?>(
                "SELECT \"Id\" FROM \"DelayProfiles\" WHERE \"Order\" = @Order ORDER BY \"Id\"",
                new { Order = DefaultDelayOrder },
                transaction);
        }

        // A document as the repository would store it, to compare two rows by content.
        private static string Serialize<T>(EmbeddedDocumentConverter<T> converter, T value)
        {
            var parameter = new SQLiteParameter();

            converter.SetValue(parameter, value);

            return (string)parameter.Value;
        }

        private sealed class Source
        {
            public Source(string app, MediaKind kind, SourceSnapshot snapshot, IUpstreamCatalog catalog)
            {
                App = app;
                Kind = kind;
                Snapshot = snapshot;
                Catalog = catalog;
            }

            public string App { get; }
            public MediaKind Kind { get; }
            public SourceSnapshot Snapshot { get; }
            public IUpstreamCatalog Catalog { get; }
        }

        // The rows of one table from every source, merged where they are the same row.
        private sealed class Planned<TModel>
            where TModel : ModelBase, IMediaKindScoped, new()
        {
            private readonly List<TModel> _models = new List<TModel>();
            private readonly List<(string App, long SourceId, int Index)> _rows = new List<(string App, long SourceId, int Index)>();

            public void Add(ImportSession session, Source source, string table, long sourceId, TModel model, Func<TModel, string> name, Action<TModel, string> rename, Func<TModel, string> content)
            {
                var sameName = _models.FindIndex(m => string.Equals(name(m), name(model), StringComparison.OrdinalIgnoreCase));

                if (sameName >= 0 && !_models[sameName].AppliesToKind(source.Kind))
                {
                    var existing = _models[sameName];

                    if (string.Equals(content(existing), content(model), StringComparison.Ordinal))
                    {
                        existing.AppliesTo.Add(source.Kind);
                        _rows.Add((source.App, sourceId, sameName));
                        return;
                    }

                    var original = name(model);

                    rename(model, $"{original} ({source.App})");
                    session.Warn($"{TableLabel(table)} \"{original}\" exists in more than one app with different settings. The {source.App} one was imported as \"{name(model)}\".");
                }

                _rows.Add((source.App, sourceId, _models.Count));
                _models.Add(model);
            }

            public void Write(ImportSession session, string table, IBasicRepository<TModel> repository)
            {
                repository.InsertMany(_models, session.Connection, session.Transaction);

                foreach (var (app, sourceId, index) in _rows)
                {
                    session.IdMap.Record(app, table, sourceId, _models[index].Id);
                    session.Imported(app, table);
                }
            }

            private static string TableLabel(string table)
            {
                return table == CustomFormatsTable ? "Custom format" : "Quality profile";
            }
        }
    }
}
