using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NzbDrone.Common.Reflection;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Datastore.Converters;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Modules;
using NzbDrone.Core.ThingiProvider;

namespace NzbDrone.Core.Upstream
{
    // Radarr and Sonarr download clients are merged by endpoint. Prowlarr's copy is mapped to an
    // imported app client when it matches; a Prowlarr-only client has no media type and is skipped.
    // Provider settings can contain credentials, so they are never included in logs or reports.
    public class DownloadClientsStage : IImportStage
    {
        public const string Table = "DownloadClients";

        private static readonly ProviderSettingConverter SettingsConverter = new ProviderSettingConverter();
        private static readonly Lazy<HashSet<string>> Implementations = new(AvailableImplementations);
        private static readonly (string App, MediaKind Kind)[] Apps =
        {
            (UpstreamApps.Radarr, MediaKind.Movies),
            (UpstreamApps.Sonarr, MediaKind.Tv)
        };
        private static readonly MediaKind[] MediaKinds = { MediaKind.Movies, MediaKind.Tv, MediaKind.Music };

        private readonly IDownloadClientRepository _clients;

        public DownloadClientsStage(IDownloadClientRepository clients)
        {
            _clients = clients;
        }

        public string Name => "Download clients";

        public int Order => ImportOrder.DownloadClients;

        public IReadOnlyList<SourceTableRef> Tables { get; } = new[]
        {
            new SourceTableRef(UpstreamApps.Radarr, Table),
            new SourceTableRef(UpstreamApps.Sonarr, Table),
            new SourceTableRef(UpstreamApps.Prowlarr, Table)
        };

        public void Run(ImportSession session)
        {
            var planned = new List<PlannedClient>();

            foreach (var (app, kind) in Apps)
            {
                foreach (var row in session.Snapshot(app)?.Rows(Table) ?? Array.Empty<SourceRow>())
                {
                    ImportAppClient(session, planned, app, kind, row);
                }
            }

            foreach (var client in planned)
            {
                if (client.Definition.Settings is IModuleCategorySettings moduleSettings)
                {
                    var priority = client.SettingsType.GetCustomAttributes(typeof(ModuleCategoryFieldsAttribute), true)
                        .OfType<ModuleCategoryFieldsAttribute>()
                        .SingleOrDefault()?.DefaultPriority ?? 0;

                    moduleSettings.EnsureEntries(MediaKinds, priority);
                }
            }

            var models = planned.Select(c => c.Definition).ToList();
            _clients.InsertMany(models, session.Connection, session.Transaction);

            for (var i = 0; i < planned.Count; i++)
            {
                foreach (var source in planned[i].Sources)
                {
                    session.IdMap.Record(source.App, Table, source.Id, models[i].Id);
                    session.Imported(source.App, Table);
                }
            }

            ImportProwlarrClients(session, planned);
        }

        private static void ImportAppClient(ImportSession session, List<PlannedClient> planned, string app, MediaKind kind, SourceRow row)
        {
            var id = row.GetLong("Id");
            var name = row.GetString("Name");
            var implementation = row.GetString("Implementation");

            if (!Implementations.Value.Contains(implementation ?? string.Empty) || !DownloadClientSettingsConverter.Handles(implementation))
            {
                session.Skip(new ImportSkip(app, Table, id, name, SkipReason.UnknownProvider, $"Plattarr has no supported download client implementation {implementation}"));
                return;
            }

            var settingsType = ProviderConfigTypes.Find(row.GetString("ConfigContract"), typeof(DownloadClientDefinition));

            if (settingsType == null)
            {
                session.Skip(new ImportSkip(app, Table, id, name, SkipReason.UnknownProvider, "Plattarr has no settings type for this download client"));
                return;
            }

            try
            {
                var settingsJson = DownloadClientSettingsConverter.Convert(implementation, row.GetString("Settings"), kind);
                var settings = SettingsConverter.Parse(settingsJson, settingsType);

                if (settings is not IModuleCategorySettings)
                {
                    session.Skip(new ImportSkip(app, Table, id, name, SkipReason.UnknownProvider, "This download client's settings do not support media-type categories"));
                    return;
                }

                var tags = TagRemapper.Remap(app, row.GetJson("Tags"), session.IdMap, out var droppedTags);
                var matching = planned.FirstOrDefault(c => !c.Sources.Any(s => s.App == app) && DownloadClientMatcher.AreSame(c.Reference, row));

                if (matching == null)
                {
                    planned.Add(new PlannedClient(
                        row,
                        app,
                        settingsType,
                        new DownloadClientDefinition
                        {
                            Name = name,
                            Implementation = implementation,
                            Enable = row.GetBool("Enable"),
                            Priority = row.GetNullableInt("Priority") ?? 1,
                            RemoveCompletedDownloads = (row.GetNullableLong("RemoveCompletedDownloads") ?? 1) != 0,
                            RemoveFailedDownloads = (row.GetNullableLong("RemoveFailedDownloads") ?? 1) != 0,
                            Tags = new HashSet<int>(tags),
                            Settings = settings
                        },
                        id));
                }
                else
                {
                    MergeSettings(session, matching, settings, app, name);
                    matching.Definition.Tags.UnionWith(tags);
                    matching.Sources.Add(new SourceId(app, id));
                }

                if (droppedTags > 0)
                {
                    session.Warn($"{app} download client \"{name}\": {droppedTags} tag(s) that no longer exist were dropped.");
                }
            }
            catch (Exception ex) when (SourceDocuments.IsBad(ex))
            {
                session.SkipCorrupt(app, Table, id, name, "Settings or tags could not be read: " + SourceDocuments.Describe(ex));
            }
        }

        private static void MergeSettings(ImportSession session, PlannedClient target, IProviderConfig incoming, string app, string name)
        {
            var current = target.Definition.Settings as IModuleCategorySettings;
            var next = incoming as IModuleCategorySettings;

            if (current == null || next == null)
            {
                return;
            }

            if (!SameSettingsExceptCategories(target.Definition.Settings, incoming))
            {
                session.Warn($"{app} download client \"{name}\" shares an endpoint with {target.PrimaryApp}'s client but has different non-category settings; {target.PrimaryApp}'s settings were kept.");
            }

            current.ModuleCategories ??= new List<ModuleCategorySettings>();

            foreach (var category in next.ModuleCategories ?? new List<ModuleCategorySettings>())
            {
                if (current.ModuleCategories.Any(existing => existing.Kind == category.Kind))
                {
                    continue;
                }

                current.ModuleCategories.Add(new ModuleCategorySettings
                {
                    Kind = category.Kind,
                    Category = category.Category,
                    ImportedCategory = category.ImportedCategory,
                    RecentPriority = category.RecentPriority,
                    OlderPriority = category.OlderPriority
                });
            }
        }

        private static bool SameSettingsExceptCategories(IProviderConfig left, IProviderConfig right)
        {
            var leftJson = JObject.Parse(SettingsConverter.Serialize(left));
            var rightJson = JObject.Parse(SettingsConverter.Serialize(right));
            leftJson.Property("moduleCategories", StringComparison.OrdinalIgnoreCase)?.Remove();
            rightJson.Property("moduleCategories", StringComparison.OrdinalIgnoreCase)?.Remove();

            return JToken.DeepEquals(leftJson, rightJson);
        }

        private static void ImportProwlarrClients(ImportSession session, List<PlannedClient> planned)
        {
            foreach (var row in session.Snapshot(UpstreamApps.Prowlarr)?.Rows(Table) ?? Array.Empty<SourceRow>())
            {
                var id = row.GetLong("Id");
                var name = row.GetString("Name");

                try
                {
                    var target = planned.FirstOrDefault(c => DownloadClientMatcher.AreSame(c.Reference, row));

                    if (target == null)
                    {
                        session.Skip(new ImportSkip(UpstreamApps.Prowlarr, Table, id, name, SkipReason.NoMediaType, "Prowlarr does not associate this client with a media type; add or select it in Plattarr after import"));
                        continue;
                    }

                    session.IdMap.Record(UpstreamApps.Prowlarr, Table, id, target.Definition.Id);
                    session.Imported(UpstreamApps.Prowlarr, Table);
                }
                catch (Exception ex) when (SourceDocuments.IsBad(ex))
                {
                    session.SkipCorrupt(UpstreamApps.Prowlarr, Table, id, name, "Client endpoint settings could not be read: " + SourceDocuments.Describe(ex));
                }
            }
        }

        private static HashSet<string> AvailableImplementations()
        {
            return typeof(IDownloadClient).Assembly.ImplementationsOf<IDownloadClient>()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Select(t => t.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class PlannedClient
        {
            public PlannedClient(SourceRow reference, string primaryApp, Type settingsType, DownloadClientDefinition definition, long sourceId)
            {
                Reference = reference;
                PrimaryApp = primaryApp;
                SettingsType = settingsType;
                Definition = definition;
                Sources = new List<SourceId> { new SourceId(primaryApp, sourceId) };
            }

            public SourceRow Reference { get; }
            public string PrimaryApp { get; }
            public Type SettingsType { get; }
            public DownloadClientDefinition Definition { get; }
            public List<SourceId> Sources { get; }
        }

        private sealed record SourceId(string App, long Id);
    }
}
