using System.Collections.Generic;

namespace NzbDrone.Core.Upstream
{
    // Reads sonarr.db (docs/importer.md, "From sonarr.db"). The table list follows the mapping
    // table there. Seasons are not a table: Sonarr keeps them as a JSON document on the series.
    //
    // Indexers, DownloadClients, Notifications and ImportLists settings hold credentials and API
    // keys. They are read into memory for the stages and must never be logged or reported.
    public class SonarrAdapter : UpstreamAdapterBase
    {
        // 217 is Sonarr 4.0.20.3014, the owner's release (schema in Files/Upstream). Older schemas
        // are read with the columns they have; 1 is a floor, not a tested lower bound.
        private static readonly VersionRange Versions = new VersionRange(1, 217);

        private static readonly string[] FullTables =
        {
            "Tags",
            "CustomFormats",
            "QualityProfiles",
            "QualityDefinitions",
            "DelayProfiles",
            "ReleaseProfiles",
            "Indexers",
            "DownloadClients",
            "RemotePathMappings",
            "Notifications",
            "AutoTagging",
            "CustomFilters",
            "RootFolders",
            "NamingConfig",
            "ImportListExclusions",
            "Metadata",
            "ImportLists",
            "Series",
            "Episodes",
            "EpisodeFiles",
            "MetadataFiles",
            "ExtraFiles",
            "SubtitleFiles",
            "History",
            "DownloadHistory",
            "Blocklist",
            "Config"
        };

        // SceneMappings is a cache of services.sonarr.tv, which Plattarr fetches again every
        // three hours; ImportListItems is what the lists last returned.
        private static readonly (string Table, string Reason)[] CountOnlyTables =
        {
            ("PendingReleases", SkipReason.Transient),
            ("SceneMappings", SkipReason.Transient),
            ("ImportListItems", SkipReason.Transient),
            ("Users", SkipReason.Instance),
            ("VersionInfo", SkipReason.Instance),
            ("ScheduledTasks", SkipReason.Transient),
            ("Commands", SkipReason.Transient),
            ("IndexerStatus", SkipReason.Transient),
            ("DownloadClientStatus", SkipReason.Transient),
            ("NotificationStatus", SkipReason.Transient),
            ("ImportListStatus", SkipReason.Transient)
        };

        public override string AppName => UpstreamApps.Sonarr;
        public override string DatabaseFileName => "sonarr.db";
        public override VersionRange SupportedVersions => Versions;

        protected override IReadOnlyList<string> RequiredTables => new[] { "VersionInfo", "Series", "Episodes" };
        protected override IReadOnlyList<string> TablesToRead => FullTables;
        protected override IReadOnlyList<(string Table, string Reason)> TablesToCount => CountOnlyTables;
    }
}
