using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NzbDrone.Common.Reflection;
using NzbDrone.Core.Datastore.Converters;
using NzbDrone.Core.IndexerProxies;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Definitions.Cardigann;
using NzbDrone.Core.Indexers.Overrides;
using NzbDrone.Core.Modules;
using NzbDrone.Core.ThingiProvider;

namespace NzbDrone.Core.Upstream
{
    // Prowlarr's indexer proxies and indexers, and what Radarr's and Sonarr's synchronized copies
    // of those indexers add to them (docs/importer.md, "Indexers"). Indexers come from Prowlarr
    // only: an app indexer that Prowlarr synchronized is the same indexer, so it is matched, not
    // written.
    //
    // Settings hold credentials, cookies and API keys. Nothing here logs or reports a settings
    // value; warnings and skips name the indexer, a setting's key or a reason.
    public class IndexersStage : IImportStage
    {
        public const string IndexersTable = "Indexers";
        public const string IndexerProxiesTable = "IndexerProxies";
        public const string SyncProfilesTable = "AppSyncProfiles";
        public const string ApplicationsTable = "Applications";
        public const string MappingTable = "ApplicationIndexerMapping";

        private static readonly ProviderSettingConverter SettingsConverter = new ProviderSettingConverter();

        // The implementations the provider factories can build: the same exported classes the
        // container registers. A row whose implementation is not here would be deleted on the
        // first start (ProviderFactory.RemoveMissingImplementations), so it is skipped instead.
        private static readonly Lazy<HashSet<string>> IndexerImplementations = new(Implementations<IIndexer>);
        private static readonly Lazy<HashSet<string>> ProxyImplementations = new(Implementations<IIndexerProxy>);

        private readonly IIndexerRepository _indexers;
        private readonly IIndexerProxyRepository _proxies;
        private readonly IIndexerOverrideRepository _overrides;
        private readonly IEnumerable<IUpstreamCatalog> _catalogs;

        public IndexersStage(
            IIndexerRepository indexers,
            IIndexerProxyRepository proxies,
            IIndexerOverrideRepository overrides,
            IEnumerable<IUpstreamCatalog> catalogs)
        {
            _indexers = indexers;
            _proxies = proxies;
            _overrides = overrides;
            _catalogs = catalogs;
        }

        public string Name => "Indexers";

        public int Order => ImportOrder.Indexers;

        public IReadOnlyList<SourceTableRef> Tables { get; } = new[]
        {
            new SourceTableRef(UpstreamApps.Prowlarr, IndexerProxiesTable),
            new SourceTableRef(UpstreamApps.Prowlarr, SyncProfilesTable),
            new SourceTableRef(UpstreamApps.Prowlarr, IndexersTable),
            new SourceTableRef(UpstreamApps.Prowlarr, ApplicationsTable),
            new SourceTableRef(UpstreamApps.Prowlarr, MappingTable),
            new SourceTableRef(UpstreamApps.Radarr, IndexersTable),
            new SourceTableRef(UpstreamApps.Sonarr, IndexersTable)
        };

        // The apps whose synchronized copies are matched, in the order their copies speak for a
        // Prowlarr indexer's settings (PrepareSettings).
        private static readonly (string App, MediaKind Kind)[] Apps =
        {
            (UpstreamApps.Radarr, MediaKind.Movies),
            (UpstreamApps.Sonarr, MediaKind.Tv)
        };

        public void Run(ImportSession session)
        {
            var prowlarr = session.Snapshot(UpstreamApps.Prowlarr);

            if (prowlarr == null)
            {
                // Without Prowlarr there is nothing to match against, and an app's own indexer
                // settings are not Prowlarr's: guessing the translation is how data is lost.
                foreach (var (app, _) in Apps)
                {
                    foreach (var row in session.Snapshot(app)?.Rows(IndexersTable) ?? Array.Empty<SourceRow>())
                    {
                        session.Skip(new ImportSkip(app, IndexersTable, row.GetLong("Id"), row.GetString("Name"), SkipReason.UnmatchedIndexer, "No Prowlarr database was given; indexers are imported from Prowlarr"));
                    }
                }

                return;
            }

            ImportProxies(session, prowlarr);

            var copies = Apps
                .Select(a => new AppCopies(a.App, a.Kind, ApplicationSettings(prowlarr, a.App), MatchAppIndexers(prowlarr, session.Snapshot(a.App), a.App)))
                .ToList();
            var effective = ImportIndexers(session, prowlarr, copies);
            var overrides = new List<IndexerOverride>();

            foreach (var app in copies)
            {
                overrides.AddRange(ImportAppIndexers(session, app, effective));
            }

            _overrides.InsertMany(overrides, session.Connection, session.Transaction);

            // Prowlarr's sync targets and its sync records have no home in Plattarr (there is no
            // sync); they were read only to match the apps' indexers.
            session.NotImportedByDesign(UpstreamApps.Prowlarr, ApplicationsTable, SkipReason.Obsolete);
            session.NotImportedByDesign(UpstreamApps.Prowlarr, MappingTable, SkipReason.Obsolete);
        }

        private void ImportProxies(ImportSession session, SourceSnapshot prowlarr)
        {
            var batch = new RowBatch<IndexerProxyDefinition>();

            foreach (var row in prowlarr.Rows(IndexerProxiesTable))
            {
                var id = row.GetLong("Id");
                var name = row.GetString("Name");
                var implementation = row.GetString("Implementation");

                if (!ProxyImplementations.Value.Contains(implementation ?? string.Empty))
                {
                    session.Skip(new ImportSkip(UpstreamApps.Prowlarr, IndexerProxiesTable, id, name, SkipReason.UnknownProvider, $"Plattarr has no indexer proxy implementation {implementation}"));
                    continue;
                }

                var settingsType = ProviderConfigTypes.Find(row.GetString("ConfigContract"), typeof(IndexerProxyDefinition));

                if (settingsType == null)
                {
                    session.Skip(new ImportSkip(UpstreamApps.Prowlarr, IndexerProxiesTable, id, name, SkipReason.UnknownProvider, "Plattarr has no settings type for this proxy"));
                    continue;
                }

                try
                {
                    var document = SettingsObject(row);
                    var settings = SettingsConverter.Parse(document.ToString(Formatting.None), settingsType);
                    var tags = TagRemapper.Remap(UpstreamApps.Prowlarr, row.GetJson("Tags"), session.IdMap, out var droppedTags);

                    WarnDroppedKeys(session, $"Prowlarr indexer proxy \"{name}\"", document, settings);
                    WarnDroppedTags(session, $"Prowlarr indexer proxy \"{name}\"", droppedTags);

                    batch.Add(id, new IndexerProxyDefinition
                    {
                        Name = name,
                        Implementation = implementation,
                        Settings = settings,
                        Tags = new HashSet<int>(tags)
                    });
                }
                catch (Exception ex) when (SourceDocuments.IsBad(ex))
                {
                    session.SkipCorrupt(UpstreamApps.Prowlarr, IndexerProxiesTable, id, name, "Settings or tags could not be read: " + SourceDocuments.Describe(ex));
                }
            }

            batch.Write(session, UpstreamApps.Prowlarr, IndexerProxiesTable, _proxies);
        }

        // Writes Prowlarr's indexers and returns, per Prowlarr indexer ID, the RSS, automatic and
        // interactive search flags it now has (Prowlarr's Enable and the sync profile's flags, as
        // Prowlarr pushed them to the apps), for the comparison with Radarr's copies.
        private Dictionary<long, SearchFlags> ImportIndexers(ImportSession session, SourceSnapshot prowlarr, List<AppCopies> copies)
        {
            var profiles = prowlarr.Rows(SyncProfilesTable).ToDictionary(r => r.GetLong("Id"));
            var usedProfiles = new HashSet<long>();

            // Per app, the copy that speaks for each Prowlarr indexer; the first app with a copy wins.
            var matched = copies.Select(c => (Copies: c, ByProwlarrId: MatchedByProwlarrId(c.Indexers))).ToList();
            var batch = new RowBatch<IndexerDefinition>();
            var effective = new Dictionary<long, SearchFlags>();
            var cardigann = new List<string>();

            foreach (var row in prowlarr.Rows(IndexersTable))
            {
                var id = row.GetLong("Id");
                var name = row.GetString("Name");
                var implementation = row.GetString("Implementation");

                if (!IndexerImplementations.Value.Contains(implementation ?? string.Empty))
                {
                    session.Skip(new ImportSkip(UpstreamApps.Prowlarr, IndexersTable, id, name, SkipReason.UnknownProvider, $"Plattarr has no indexer implementation {implementation}"));
                    continue;
                }

                var settingsType = ProviderConfigTypes.Find(row.GetString("ConfigContract"), typeof(IndexerDefinition));

                if (settingsType == null)
                {
                    session.Skip(new ImportSkip(UpstreamApps.Prowlarr, IndexersTable, id, name, SkipReason.UnknownProvider, "Plattarr has no settings type for this indexer"));
                    continue;
                }

                var profileId = row.GetNullableLong("AppProfileId");
                var profile = profileId.HasValue && profiles.TryGetValue(profileId.Value, out var found) ? found : null;
                var speaking = matched.FirstOrDefault(m => m.ByProwlarrId.ContainsKey(id));
                var appIndexer = speaking.ByProwlarrId?[id];
                var application = speaking.Copies?.Application ?? copies[0].Application;

                IProviderConfig settings;
                List<int> tags;

                try
                {
                    var document = SettingsObject(row);

                    PrepareSettings(document, settingsType, profile, appIndexer?.Settings, application);

                    settings = SettingsConverter.Parse(document.ToString(Formatting.None), settingsType);
                    tags = TagRemapper.Remap(UpstreamApps.Prowlarr, row.GetJson("Tags"), session.IdMap, out var droppedTags);

                    WarnDroppedKeys(session, $"Prowlarr indexer \"{name}\"", document, settings);
                    WarnDroppedTags(session, $"Prowlarr indexer \"{name}\"", droppedTags);
                }
                catch (Exception ex) when (SourceDocuments.IsBad(ex))
                {
                    session.SkipCorrupt(UpstreamApps.Prowlarr, IndexersTable, id, name, "Settings or tags could not be read: " + SourceDocuments.Describe(ex));
                    continue;
                }

                if (profile == null)
                {
                    session.Warn($"Prowlarr indexer \"{name}\" names a sync profile that does not exist. It was imported with RSS, automatic and interactive search on and, unless it sets its own, Plattarr's default minimum seeders.");
                }
                else
                {
                    usedProfiles.Add(profile.GetLong("Id"));
                }

                var sourceClientId = row.GetNullableInt("DownloadClientId") ?? 0;
                var targetClientId = 0;

                if (sourceClientId > 0)
                {
                    if (session.IdMap.TryResolve(UpstreamApps.Prowlarr, DownloadClientsStage.Table, sourceClientId, out var resolvedClientId))
                    {
                        targetClientId = checked((int)resolvedClientId);
                    }
                    else
                    {
                        session.Warn($"Prowlarr indexer \"{name}\" sends its grabs to a chosen download client that was not imported. Pick a download client again on the indexer after the import.");
                    }
                }

                var enable = (row.GetNullableLong("Enable") ?? 0) != 0;
                var flags = new SearchFlags(
                    profile == null || profile.GetBool("EnableRss"),
                    profile == null || profile.GetBool("EnableAutomaticSearch"),
                    profile == null || profile.GetBool("EnableInteractiveSearch"));

                effective[id] = flags.When(enable);

                if (settings is CardigannSettings cardigannSettings)
                {
                    cardigann.Add($"{name} ({cardigannSettings.DefinitionFile})");
                }

                batch.Add(id, new IndexerDefinition
                {
                    Name = name,
                    Implementation = implementation,
                    Settings = settings,
                    Enable = enable,
                    Priority = row.GetNullableInt("Priority") ?? IndexerDefinition.DefaultPriority,
                    Redirect = (row.GetNullableLong("Redirect") ?? 0) != 0,
                    Added = row.GetNullableDateTime("Added") ?? session.ImportedAt,
                    Tags = new HashSet<int>(tags),
                    DownloadClientId = targetClientId,
                    EnableRss = flags.Rss,
                    EnableAutomaticSearch = flags.Automatic,
                    EnableInteractiveSearch = flags.Interactive
                });
            }

            batch.Write(session, UpstreamApps.Prowlarr, IndexersTable, _indexers);

            // A sync profile has no table of its own in Plattarr: its flags and minimum seeders are
            // now on each indexer that used it. One that no imported indexer used is dropped.
            foreach (var profile in prowlarr.Rows(SyncProfilesTable))
            {
                var id = profile.GetLong("Id");

                if (usedProfiles.Contains(id))
                {
                    session.Imported(UpstreamApps.Prowlarr, SyncProfilesTable);
                }
                else
                {
                    session.Skip(new ImportSkip(UpstreamApps.Prowlarr, SyncProfilesTable, id, profile.GetString("Name"), SkipReason.Obsolete, "No imported indexer uses this sync profile; Plattarr keeps these settings on each indexer"));
                }
            }

            if (cardigann.Count > 0)
            {
                session.Warn($"{cardigann.Count} imported indexers are Cardigann definitions, which Plattarr downloads on its first start: {string.Join(", ", cardigann)}. After the first start check Settings > Indexers for any that report a missing definition; a custom definition (a .yml file in Prowlarr's Definitions/Custom folder) must be copied to Plattarr's Definitions/Custom folder.");
            }

            return effective;
        }

        // An app indexer that Prowlarr synchronized becomes the Plattarr indexer it came from (the
        // IdMap points both at one row, so release profiles and download history can follow it).
        // What the app kept on its copy and Prowlarr has no global setting for goes into an
        // override for the app's media type; differences that have no home are reported.
        private List<IndexerOverride> ImportAppIndexers(ImportSession session, AppCopies copies, Dictionary<long, SearchFlags> effective)
        {
            var overrides = new List<IndexerOverride>();

            if (copies.Indexers.Count == 0)
            {
                return overrides;
            }

            var app = copies.App;
            var catalog = _catalogs.For(app);
            var overridden = new HashSet<int>();
            var standardFormat = new List<string>();

            foreach (var indexer in copies.Indexers)
            {
                if (indexer.Problem != null)
                {
                    session.SkipCorrupt(app, IndexersTable, indexer.Id, indexer.Name, "Settings could not be read: " + indexer.Problem);
                    continue;
                }

                if (!indexer.Match.Matched)
                {
                    session.Skip(new ImportSkip(app, IndexersTable, indexer.Id, indexer.Name, SkipReason.UnmatchedIndexer, indexer.Match.Reason));
                    continue;
                }

                var prowlarrId = indexer.Match.ProwlarrIndexerId.Value;

                if (!session.IdMap.TryResolve(UpstreamApps.Prowlarr, IndexersTable, prowlarrId, out var target))
                {
                    session.Skip(new ImportSkip(app, IndexersTable, indexer.Id, indexer.Name, SkipReason.UnmatchedIndexer, "The Prowlarr indexer it was synchronized from was not imported"));
                    continue;
                }

                session.IdMap.Record(app, IndexersTable, indexer.Id, target);
                session.Imported(app, IndexersTable);

                if (!indexer.Match.Confirmed)
                {
                    session.Warn($"{app} indexer \"{indexer.Name}\" was matched to its Prowlarr indexer by its URL only: Prowlarr kept no sync records.");
                }

                WarnFlagDifferences(session, app, indexer, effective[prowlarrId]);

                if (indexer.HasTags)
                {
                    session.Warn($"{app} indexer \"{indexer.Name}\" has tags, which limited it to {(copies.Kind == MediaKind.Tv ? "TV shows" : "movies")} with those tags. They were not carried: Plattarr's indexer tags are Prowlarr's.");
                }

                if ((indexer.Row.GetNullableLong("DownloadClientId") ?? 0) != 0)
                {
                    session.Warn($"{app} indexer \"{indexer.Name}\" sends its grabs to a chosen download client. The importer does not carry that choice yet: pick the client again on the indexer after the import.");
                }

                IndexerOverride indexerOverride;

                try
                {
                    if (copies.Kind == MediaKind.Tv)
                    {
                        indexerOverride = TvOverride(indexer.Settings, indexer.Row.GetNullableInt("SeasonSearchMaximumSingleEpisodeAge") ?? 0, (int)target, catalog, copies.Application);

                        if (UnsyncedAnimeCategories(indexer.Settings, copies.Application))
                        {
                            session.Warn($"{app} indexer \"{indexer.Name}\" searches anime categories that Prowlarr does not sync. Plattarr has no separate anime categories per indexer, so they were not carried; add them to the indexer's TV categories if anime searches miss releases.");
                        }

                        if (Value(indexer.Settings, "animeStandardFormatSearch") is { Type: JTokenType.Boolean } standard && standard.Value<bool>())
                        {
                            standardFormat.Add(indexer.Name);
                        }
                    }
                    else
                    {
                        indexerOverride = MovieOverride(indexer.Settings, (int)target, catalog, IntList(Value(copies.Application, "syncCategories")));
                    }
                }
                catch (Exception ex) when (SourceDocuments.IsBad(ex))
                {
                    session.Warn($"{app} indexer \"{indexer.Name}\": its per-{(copies.Kind == MediaKind.Tv ? "TV" : "movie")} settings (languages, required flags, categories) could not be read and were not carried: {SourceDocuments.Describe(ex)}");
                    continue;
                }

                if (indexerOverride == null)
                {
                    continue;
                }

                if (!overridden.Add(indexerOverride.IndexerId))
                {
                    session.Warn($"{app} indexer \"{indexer.Name}\" is a second {app} copy of one Prowlarr indexer; the first copy's per-{(copies.Kind == MediaKind.Tv ? "TV" : "movie")} settings were kept and this one's were not carried.");
                    continue;
                }

                overrides.Add(indexerOverride);
            }

            // One setting in Plattarr (Settings > Indexers), not one per indexer as in Sonarr.
            if (standardFormat.Count > 0)
            {
                session.Warn($"{app} indexers search anime by standard episode numbers as well ({string.Join(", ", standardFormat)}). Plattarr has one setting for this: turn on \"Anime Standard Format Search\" in Settings > Indexers after the import.");
            }

            return overrides;
        }

        // Prowlarr's settings document, made into what Plattarr's settings type reads. Prowlarr kept
        // some values off the indexer and pushed them to each app; Plattarr has one global value
        // per indexer, so those values are written into the indexer's own settings here.
        public static void PrepareSettings(JObject document, Type settingsType, SourceRow syncProfile, JObject appIndexerSettings, JObject application)
        {
            if (typeof(ITorrentIndexerSettings).IsAssignableFrom(settingsType))
            {
                var torrent = Child(document, "torrentBaseSettings");

                // Prowlarr's AppMinimumSeeders (written only when set); Prowlarr pushed it, or the sync
                // profile's value when it was not set, to every app.
                var appMinimumSeeders = Take(torrent, "appMinimumSeeders");

                if (Value(torrent, "minimumSeeders") == null)
                {
                    var minimumSeeders = appMinimumSeeders ?? Token(syncProfile?.GetNullableInt("MinimumSeeders"));

                    if (minimumSeeders != null)
                    {
                        torrent["minimumSeeders"] = minimumSeeders;
                    }
                }

                // A per-application setting in Prowlarr, written into each synchronized indexer. The
                // app's copy says what was in effect; without one, the application setting.
                if (Value(torrent, "rejectBlocklistedTorrentHashesWhileGrabbing") == null)
                {
                    var reject = Value(appIndexerSettings, "rejectBlocklistedTorrentHashesWhileGrabbing") ?? Value(application, "syncRejectBlocklistedTorrentHashesWhileGrabbing");

                    if (reject is { Type: JTokenType.Boolean })
                    {
                        torrent["rejectBlocklistedTorrentHashesWhileGrabbing"] = reject.DeepClone();
                    }
                }
            }

            if (typeof(IIndexerSettings).IsAssignableFrom(settingsType))
            {
                // Radarr's own setting (Prowlarr never synchronized it), which Plattarr keeps on the indexer.
                if (Value(appIndexerSettings, "failDownloads") is JArray { Count: > 0 } failDownloads)
                {
                    var baseSettings = Child(document, "baseSettings");

                    if (Value(baseSettings, "failDownloads") is not JArray { Count: > 0 })
                    {
                        baseSettings["failDownloads"] = failDownloads.DeepClone();
                    }
                }
            }
        }

        // What Radarr's copy holds that Plattarr keeps per media type. Null when nothing is set.
        public static IndexerOverride MovieOverride(JObject settings, int indexerId, IUpstreamCatalog catalog, List<int> syncCategories)
        {
            var result = new IndexerOverride { IndexerId = indexerId, MediaKind = MediaKind.Movies };
            var any = false;

            var multiLanguages = Value(settings, "multiLanguages");

            if (multiLanguages is JArray { Count: > 0 })
            {
                result.MultiLanguages = IntList(CatalogRemapper.RemapLanguages(multiLanguages, catalog));
                any = true;
            }

            var requiredFlags = IntList(Value(settings, "requiredFlags"));

            if (requiredFlags is { Count: > 0 })
            {
                result.RequiredFlags = requiredFlags;
                any = true;
            }

            if (Value(settings, "removeYear") is { Type: JTokenType.Boolean } removeYear && removeYear.Value<bool>())
            {
                result.RemoveYear = true;
                any = true;
            }

            // Prowlarr writes the indexer's supported part of the application's sync categories on
            // every full sync, which is what Plattarr searches by default. Only a category outside
            // that set was added by hand and needs an override.
            var categories = IntList(Value(settings, "categories"));

            if (categories is { Count: > 0 } && categories.Any(c => !IsSyncCategory(c, syncCategories)))
            {
                result.Categories = categories;
                any = true;
            }

            return any ? result : null;
        }

        // What Sonarr's copy holds that Plattarr keeps per media type. Null when nothing is set.
        public static IndexerOverride TvOverride(JObject settings, int seasonSearchMaximumSingleEpisodeAge, int indexerId, IUpstreamCatalog catalog, JObject application)
        {
            var result = new IndexerOverride { IndexerId = indexerId, MediaKind = MediaKind.Tv };
            var any = false;

            var multiLanguages = Value(settings, "multiLanguages");

            if (multiLanguages is JArray { Count: > 0 })
            {
                result.MultiLanguages = IntList(CatalogRemapper.RemapLanguages(multiLanguages, catalog));
                any = true;
            }

            // Sonarr's flag bits differ from the global (Radarr) ones from bit 8 up (F4).
            var requiredFlags = IntList(Value(settings, "requiredFlags"));

            if (requiredFlags is { Count: > 0 })
            {
                result.RequiredFlags = requiredFlags.Select(f => (int)ProfilesStage.SonarrFlags(f)).ToList();
                any = true;
            }

            var syncCategories = IntList(Value(application, "syncCategories"));
            var categories = IntList(Value(settings, "categories"));

            if (categories is { Count: > 0 } && categories.Any(c => !(syncCategories?.Contains(c) ?? IsTvCategory(c))))
            {
                result.Categories = categories;
                any = true;
            }

            if (seasonSearchMaximumSingleEpisodeAge > 0)
            {
                result.SeasonSearchMaximumSingleEpisodeAge = seasonSearchMaximumSingleEpisodeAge;
                any = true;
            }

            return any ? result : null;
        }

        // True when Sonarr's copy searches anime categories outside what Prowlarr syncs for anime
        // (5070 when the application row has none) and outside the TV range Plattarr searches.
        private static bool UnsyncedAnimeCategories(JObject settings, JObject application)
        {
            var anime = IntList(Value(settings, "animeCategories"));
            var synced = IntList(Value(application, "animeSyncCategories")) ?? new List<int> { 5070 };

            return anime is { Count: > 0 } && anime.Any(c => !synced.Contains(c) && !IsTvCategory(c));
        }

        private static bool IsTvCategory(int category)
        {
            // No application settings: Prowlarr's default TV categories are the 5000 range.
            return category >= 5000 && category < 6000;
        }

        private static bool IsSyncCategory(int category, List<int> syncCategories)
        {
            // No application settings: Prowlarr's default movie categories are the 2000 range.
            return syncCategories?.Contains(category) ?? (category >= 2000 && category < 3000);
        }

        private static void WarnFlagDifferences(ImportSession session, string app, AppIndexer indexer, SearchFlags prowlarr)
        {
            var differences = new List<string>();

            Compare(differences, app, "RSS", indexer.Row.GetNullableLong("EnableRss"), prowlarr.Rss);
            Compare(differences, app, "automatic search", indexer.Row.GetNullableLong("EnableAutomaticSearch"), prowlarr.Automatic);
            Compare(differences, app, "interactive search", indexer.Row.GetNullableLong("EnableInteractiveSearch"), prowlarr.Interactive);

            if (differences.Count > 0)
            {
                session.Warn($"{app} indexer \"{indexer.Name}\" differs from Prowlarr ({string.Join(", ", differences)}). Plattarr has one setting per indexer and keeps Prowlarr's.");
            }
        }

        private static void Compare(List<string> differences, string app, string label, long? copy, bool prowlarr)
        {
            if (copy.HasValue && (copy.Value != 0) != prowlarr)
            {
                differences.Add($"{label} {(prowlarr ? "off" : "on")} in {app}, {(prowlarr ? "on" : "off")} in Prowlarr");
            }
        }

        private static List<AppIndexer> MatchAppIndexers(SourceSnapshot prowlarr, SourceSnapshot app, string application)
        {
            if (app == null)
            {
                return new List<AppIndexer>();
            }

            var matcher = new IndexerMatcher(prowlarr, application);
            var result = new List<AppIndexer>();

            foreach (var row in app.Rows(IndexersTable))
            {
                var indexer = new AppIndexer { Row = row, Id = row.GetLong("Id"), Name = row.GetString("Name") };

                try
                {
                    indexer.Settings = row.GetJson("Settings") as JObject;
                    indexer.HasTags = row.GetJson("Tags") is JArray { Count: > 0 };
                    indexer.Match = matcher.Match(row);
                }
                catch (Exception ex) when (SourceDocuments.IsBad(ex))
                {
                    indexer.Problem = SourceDocuments.Describe(ex);
                }

                result.Add(indexer);
            }

            return result;
        }

        // The app's copy that speaks for each Prowlarr indexer: a match Prowlarr's sync records
        // confirm before one by URL only, then the lowest app indexer ID.
        private static Dictionary<long, AppIndexer> MatchedByProwlarrId(IEnumerable<AppIndexer> appIndexers)
        {
            return appIndexers
                .Where(i => i.Problem == null && i.Match.Matched)
                .GroupBy(i => i.Match.ProwlarrIndexerId.Value)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.Match.Confirmed).ThenBy(i => i.Id).First());
        }

        private static JObject ApplicationSettings(SourceSnapshot prowlarr, string implementation)
        {
            var row = prowlarr.Rows(ApplicationsTable)
                .Where(r => string.Equals(r.GetString("Implementation"), implementation, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.GetLong("Id"))
                .FirstOrDefault();

            try
            {
                return row?.GetJson("Settings") as JObject;
            }
            catch (CorruptSourceDataException)
            {
                // Read only for two defaults; an unreadable row means "no application settings".
                return null;
            }
        }

        private static JObject SettingsObject(SourceRow row)
        {
            var settings = row.GetJson("Settings");

            return settings switch
            {
                null => new JObject(),
                JObject document => document,
                _ => throw new CorruptSourceDataException("Column Settings does not hold a JSON object")
            };
        }

        // Keys of the source document that the target's settings type did not keep. Names only.
        public static List<string> DroppedKeys(JObject source, IProviderConfig parsed)
        {
            var kept = LeafPaths(JObject.Parse(SettingsConverter.Serialize(parsed))).ToHashSet(StringComparer.OrdinalIgnoreCase);

            return LeafPaths(source).Where(p => !kept.Contains(p)).ToList();
        }

        private static IEnumerable<string> LeafPaths(JObject document, string prefix = "")
        {
            foreach (var property in document.Properties())
            {
                var path = prefix + property.Name;

                if (property.Value is JObject child)
                {
                    foreach (var nested in LeafPaths(child, path + "."))
                    {
                        yield return nested;
                    }
                }
                else if (property.Value.Type != JTokenType.Null)
                {
                    yield return path;
                }
            }
        }

        private static void WarnDroppedKeys(ImportSession session, string subject, JObject document, IProviderConfig settings)
        {
            var dropped = DroppedKeys(document, settings);

            if (dropped.Count > 0)
            {
                session.Warn($"{subject}: settings Plattarr does not have were not carried: {string.Join(", ", dropped)}.");
            }
        }

        private static void WarnDroppedTags(ImportSession session, string subject, int dropped)
        {
            if (dropped > 0)
            {
                session.Warn($"{subject}: {dropped} tag(s) that no longer exist in the source were dropped.");
            }
        }

        private static HashSet<string> Implementations<T>()
        {
            return typeof(T).Assembly.ImplementationsOf<T>()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Select(t => t.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static JToken Value(JObject document, string key)
        {
            var value = document?.GetValue(key, StringComparison.OrdinalIgnoreCase);

            return value == null || value.Type == JTokenType.Null ? null : value;
        }

        private static JToken Take(JObject document, string key)
        {
            var property = document.Property(key, StringComparison.OrdinalIgnoreCase);

            property?.Remove();

            return property == null || property.Value.Type == JTokenType.Null ? null : property.Value;
        }

        private static JObject Child(JObject document, string key)
        {
            var property = document.Property(key, StringComparison.OrdinalIgnoreCase);

            if (property == null || property.Value.Type == JTokenType.Null)
            {
                var created = new JObject();
                document[property?.Name ?? key] = created;

                return created;
            }

            return property.Value as JObject ?? throw new CorruptSourceDataException($"Setting {key} does not hold a JSON object");
        }

        private static JToken Token(int? value)
        {
            return value.HasValue ? new JValue(value.Value) : null;
        }

        private static List<int> IntList(JToken token)
        {
            if (token == null)
            {
                return null;
            }

            if (token is not JArray array || array.Any(i => i.Type != JTokenType.Integer))
            {
                throw new CorruptSourceDataException("A setting that should hold a list of integers does not");
            }

            return array.Select(i => i.Value<int>()).ToList();
        }

        // One app's synchronized copies and the Prowlarr application row they were synchronized
        // through (null when Prowlarr has none for the app).
        private sealed class AppCopies
        {
            public AppCopies(string app, MediaKind kind, JObject application, List<AppIndexer> indexers)
            {
                App = app;
                Kind = kind;
                Application = application;
                Indexers = indexers;
            }

            public string App { get; }
            public MediaKind Kind { get; }
            public JObject Application { get; }
            public List<AppIndexer> Indexers { get; }
        }

        private sealed class AppIndexer
        {
            public SourceRow Row { get; set; }
            public long Id { get; set; }
            public string Name { get; set; }
            public JObject Settings { get; set; }
            public IndexerMatch Match { get; set; }
            public bool HasTags { get; set; }

            // Set when the row's settings could not be read; the row is skipped as CORRUPT.
            public string Problem { get; set; }
        }

        private readonly record struct SearchFlags(bool Rss, bool Automatic, bool Interactive)
        {
            public SearchFlags When(bool enabled)
            {
                return new SearchFlags(Rss && enabled, Automatic && enabled, Interactive && enabled);
            }
        }
    }
}
