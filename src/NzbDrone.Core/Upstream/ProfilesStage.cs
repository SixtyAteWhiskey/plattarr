using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore.Converters;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Upstream
{
    // Radarr's custom formats, quality profiles, quality definitions and delay profiles
    // (docs/importer.md, "Profiles and custom formats"). They are one stage because each reads
    // what the one before it wrote: profiles score custom formats, and nothing else in the
    // import needs them in between.
    //
    // Every quality, language and group ID in a stored document goes through the app's catalog,
    // and the document is then parsed by the same converters the repositories use, so a shape
    // the target cannot read fails here, on one row, not later on a page load.
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

        public IReadOnlyList<SourceTableRef> Tables { get; } = new[]
        {
            new SourceTableRef(UpstreamApps.Radarr, CustomFormatsTable),
            new SourceTableRef(UpstreamApps.Radarr, QualityProfilesTable),
            new SourceTableRef(UpstreamApps.Radarr, QualityDefinitionsTable),
            new SourceTableRef(UpstreamApps.Radarr, DelayProfilesTable)
        };

        public void Run(ImportSession session)
        {
            var snapshot = session.Snapshot(UpstreamApps.Radarr);

            if (snapshot == null)
            {
                return;
            }

            var catalog = _catalogs.Single(c => string.Equals(c.AppName, UpstreamApps.Radarr, StringComparison.OrdinalIgnoreCase));

            ImportCustomFormats(session, snapshot, catalog);
            ImportQualityProfiles(session, snapshot, catalog);
            ImportQualityDefinitions(session, snapshot, catalog);
            ImportDelayProfiles(session, snapshot);
        }

        private void ImportCustomFormats(ImportSession session, SourceSnapshot snapshot, IUpstreamCatalog catalog)
        {
            var models = new List<CustomFormat>();
            var sourceIds = new List<long>();

            foreach (var row in snapshot.Rows(CustomFormatsTable))
            {
                var id = row.GetLong("Id");
                var name = row.GetString("Name");

                try
                {
                    var specifications = ParseSpecifications(row.GetJson("Specifications"), catalog);

                    models.Add(new CustomFormat
                    {
                        Name = name,
                        IncludeCustomFormatWhenRenaming = row.GetBool("IncludeCustomFormatWhenRenaming"),
                        Specifications = specifications,
                        AppliesTo = new List<MediaKind> { MediaKind.Movies }
                    });
                    sourceIds.Add(id);
                }
                catch (Exception ex) when (IsBadDocument(ex))
                {
                    session.Skip(new ImportSkip(UpstreamApps.Radarr, CustomFormatsTable, id, name, SkipReason.Corrupt, "Specifications could not be read: " + Describe(ex)));
                }
            }

            _customFormats.InsertMany(models, session.Connection, session.Transaction);

            for (var i = 0; i < models.Count; i++)
            {
                session.IdMap.Record(UpstreamApps.Radarr, CustomFormatsTable, sourceIds[i], models[i].Id);
                session.Imported(UpstreamApps.Radarr, CustomFormatsTable);
            }
        }

        // Only a language condition holds an ID that differs between apps. The other
        // specifications use Radarr's vocabulary, which is the global one, so they are copied.
        private static List<ICustomFormatSpecification> ParseSpecifications(JToken specifications, IUpstreamCatalog catalog)
        {
            if (specifications is not JArray array)
            {
                throw new CorruptSourceDataException("A specifications column does not hold a JSON array");
            }

            var clone = (JArray)array.DeepClone();

            foreach (var item in clone.OfType<JObject>())
            {
                if (!string.Equals(item.Value<string>("type"), nameof(LanguageSpecification), StringComparison.Ordinal))
                {
                    continue;
                }

                var value = (item["body"] as JObject)?.Property("value", StringComparison.OrdinalIgnoreCase);

                if (value == null || value.Value.Type != JTokenType.Integer)
                {
                    throw new CorruptSourceDataException("A language condition has no integer language ID");
                }

                value.Value = CatalogRemapper.LanguageId(value.Value.Value<int>(), catalog);
            }

            return SpecificationsParser.Parse(clone.ToString(Formatting.None));
        }

        private void ImportQualityProfiles(ImportSession session, SourceSnapshot snapshot, IUpstreamCatalog catalog)
        {
            var models = new List<QualityProfile>();
            var sourceIds = new List<long>();

            foreach (var row in snapshot.Rows(QualityProfilesTable))
            {
                var id = row.GetLong("Id");
                var name = row.GetString("Name");

                try
                {
                    var items = RemapItems(row.GetJson("Items"), catalog, out var groups);

                    models.Add(new QualityProfile
                    {
                        Name = name,
                        Cutoff = RemapCutoff(row.GetInt("Cutoff"), groups, catalog),
                        Items = ItemsParser.Parse(items.ToString(Formatting.None)),
                        Language = RemapProfileLanguage(row.GetNullableInt("Language"), catalog),
                        FormatItems = RemapFormatItems(session, id, name, row.GetJson("FormatItems")),
                        UpgradeAllowed = row.GetBool("UpgradeAllowed"),
                        MinFormatScore = row.GetInt("MinFormatScore"),
                        CutoffFormatScore = row.GetInt("CutoffFormatScore"),
                        MinUpgradeFormatScore = row.GetInt("MinUpgradeFormatScore"),
                        AppliesTo = new List<MediaKind> { MediaKind.Movies }
                    });
                    sourceIds.Add(id);
                }
                catch (Exception ex) when (IsBadDocument(ex))
                {
                    session.Skip(new ImportSkip(UpstreamApps.Radarr, QualityProfilesTable, id, name, SkipReason.Corrupt, "Profile could not be read: " + Describe(ex)));
                }
            }

            _qualityProfiles.InsertMany(models, session.Connection, session.Transaction);

            for (var i = 0; i < models.Count; i++)
            {
                session.IdMap.Record(UpstreamApps.Radarr, QualityProfilesTable, sourceIds[i], models[i].Id);
                session.Imported(UpstreamApps.Radarr, QualityProfilesTable);
            }
        }

        // A leaf item is {"quality": id, "items": [], "allowed": bool}; a group is
        // {"id": 1000+, "name": ..., "items": [leaf, ...], "allowed": bool}.
        private static JArray RemapItems(JToken items, IUpstreamCatalog catalog, out HashSet<int> groups)
        {
            groups = new HashSet<int>();

            if (items is not JArray array)
            {
                throw new CorruptSourceDataException("A profile's items column does not hold a JSON array");
            }

            var clone = (JArray)array.DeepClone();

            foreach (var item in clone.OfType<JObject>())
            {
                RemapItemQuality(item, catalog);

                if (item["items"] is JArray members)
                {
                    foreach (var member in members.OfType<JObject>())
                    {
                        RemapItemQuality(member, catalog);
                    }
                }

                if (item["id"]?.Type == JTokenType.Integer)
                {
                    groups.Add(item.Value<int>("id"));
                }
            }

            return clone;
        }

        private static void RemapItemQuality(JObject item, IUpstreamCatalog catalog)
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

            quality.Value = CatalogRemapper.QualityId(quality.Value.Value<int>(), catalog);
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

        private static Language RemapProfileLanguage(int? language, IUpstreamCatalog catalog)
        {
            return language.HasValue ? (Language)CatalogRemapper.LanguageId(language.Value, catalog) : Language.Unknown;
        }

        // A score for a custom format that was not imported (deleted upstream, or skipped as
        // corrupt) has nothing to apply to; it is dropped and counted in one warning.
        private static List<ProfileFormatItem> RemapFormatItems(ImportSession session, long profileId, string profileName, JToken formatItems)
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

                if (!session.IdMap.TryResolve(UpstreamApps.Radarr, CustomFormatsTable, format.Value.Value<long>(), out var target))
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
                session.Warn($"Quality profile \"{profileName}\" (Radarr {profileId}): dropped {dropped} score(s) for custom formats that were not imported");
            }

            return FormatItemsParser.Parse(clone.ToString(Formatting.None));
        }

        private void ImportQualityDefinitions(ImportSession session, SourceSnapshot snapshot, IUpstreamCatalog catalog)
        {
            var models = new List<QualityDefinition>();
            var sourceIds = new List<long>();

            foreach (var row in snapshot.Rows(QualityDefinitionsTable))
            {
                var id = row.GetLong("Id");
                var title = row.GetString("Title");

                try
                {
                    var quality = CatalogRemapper.QualityId(row.GetInt("Quality"), catalog);

                    models.Add(new QualityDefinition
                    {
                        Quality = (Quality)quality,
                        Title = title,
                        MinSize = row.GetNullableDouble("MinSize"),
                        MaxSize = row.GetNullableDouble("MaxSize"),
                        PreferredSize = row.GetNullableDouble("PreferredSize")
                    });
                    sourceIds.Add(id);
                }
                catch (Exception ex) when (IsBadDocument(ex))
                {
                    session.Skip(new ImportSkip(UpstreamApps.Radarr, QualityDefinitionsTable, id, title, SkipReason.Corrupt, "Quality definition could not be read: " + Describe(ex)));
                }
            }

            _qualityDefinitions.InsertMany(models, session.Connection, session.Transaction);

            for (var i = 0; i < models.Count; i++)
            {
                session.IdMap.Record(UpstreamApps.Radarr, QualityDefinitionsTable, sourceIds[i], models[i].Id);
                session.Imported(UpstreamApps.Radarr, QualityDefinitionsTable);
            }
        }

        // The default profile (Order = int.MaxValue) already exists in a migrated target and is
        // updated in place; every other row is inserted.
        private void ImportDelayProfiles(ImportSession session, SourceSnapshot snapshot)
        {
            var inserts = new List<DelayProfile>();
            var insertSourceIds = new List<long>();
            var updates = new List<DelayProfile>();
            var updateSourceIds = new List<long>();

            var targetDefaultId = FindTargetDefaultDelayProfile(session.Connection, session.Transaction);

            foreach (var row in snapshot.Rows(DelayProfilesTable))
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
                        Tags = new HashSet<int>(TagRemapper.Remap(UpstreamApps.Radarr, row.GetJson("Tags"), session.IdMap, out var dropped))
                    };

                    if (dropped > 0)
                    {
                        session.Warn($"Delay profile (Radarr {id}): dropped {dropped} tag(s) that were not imported");
                    }

                    if (model.Order == DefaultDelayOrder && targetDefaultId.HasValue && updates.Count == 0)
                    {
                        model.Id = targetDefaultId.Value;
                        updates.Add(model);
                        updateSourceIds.Add(id);
                    }
                    else
                    {
                        inserts.Add(model);
                        insertSourceIds.Add(id);
                    }
                }
                catch (Exception ex) when (IsBadDocument(ex))
                {
                    session.Skip(new ImportSkip(UpstreamApps.Radarr, DelayProfilesTable, id, null, SkipReason.Corrupt, "Delay profile could not be read: " + Describe(ex)));
                }
            }

            _delayProfiles.UpdateMany(updates, session.Connection, session.Transaction);
            _delayProfiles.InsertMany(inserts, session.Connection, session.Transaction);

            foreach (var (sourceId, model) in updateSourceIds.Zip(updates, (s, m) => (s, m)).Concat(insertSourceIds.Zip(inserts, (s, m) => (s, m))))
            {
                session.IdMap.Record(UpstreamApps.Radarr, DelayProfilesTable, sourceId, model.Id);
                session.Imported(UpstreamApps.Radarr, DelayProfilesTable);
            }
        }

        private static int? FindTargetDefaultDelayProfile(IDbConnection connection, IDbTransaction transaction)
        {
            return connection.QueryFirstOrDefault<int?>(
                "SELECT \"Id\" FROM \"DelayProfiles\" WHERE \"Order\" = @Order ORDER BY \"Id\"",
                new { Order = DefaultDelayOrder },
                transaction);
        }

        private static bool IsBadDocument(Exception ex)
        {
            return ex is CorruptSourceDataException or System.Text.Json.JsonException or TypeLoadException or InvalidCastException or FormatException or OverflowException;
        }

        // The exception type and its message name a column or a JSON path, never a stored value;
        // the report is shared.
        private static string Describe(Exception ex)
        {
            return ex is CorruptSourceDataException ? ex.Message : ex.GetType().Name;
        }
    }
}
