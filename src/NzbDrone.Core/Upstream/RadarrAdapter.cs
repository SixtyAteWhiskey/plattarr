using System.Collections.Generic;

namespace NzbDrone.Core.Upstream
{
    // Reads radarr.db (docs/importer.md, "From radarr.db"). Table list and
    // reasons follow the mapping table there; PendingReleases is counted, not
    // read, because pending releases return on the next RSS sync.
    public class RadarrAdapter : UpstreamAdapterBase
    {
        // First supported version is the newest one checked against the owner's
        // database (docs/importer.md, "Dump findings"). Older schemas are read with
        // the columns they have; 1 is a floor, not a tested lower bound.
        private static readonly VersionRange Versions = new VersionRange(1, 242);

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
            "ImportExclusions",
            "Metadata",
            "ImportLists",
            "MovieMetadata",
            "AlternativeTitles",
            "MovieTranslations",
            "Credits",
            "Collections",
            "Movies",
            "MovieFiles",
            "ImportListMovies",
            "MetadataFiles",
            "ExtraFiles",
            "SubtitleFiles",
            "History",
            "DownloadHistory",
            "Blocklist",
            "Config"
        };

        private static readonly (string Table, string Reason)[] CountOnlyTables =
        {
            ("PendingReleases", SkipReason.Transient),
            ("Users", SkipReason.Instance),
            ("VersionInfo", SkipReason.Instance),
            ("ScheduledTasks", SkipReason.Transient),
            ("Commands", SkipReason.Transient),
            ("Logs", SkipReason.Transient),
            ("UpdateHistory", SkipReason.Transient),
            ("IndexerStatus", SkipReason.Transient),
            ("DownloadClientStatus", SkipReason.Transient),
            ("NotificationStatus", SkipReason.Transient),
            ("ImportListStatus", SkipReason.Transient)
        };

        public override string AppName => "Radarr";
        public override string DatabaseFileName => "radarr.db";
        public override VersionRange SupportedVersions => Versions;

        protected override IReadOnlyList<string> RequiredTables => new[] { "VersionInfo", "Movies", "MovieMetadata" };
        protected override IReadOnlyList<string> TablesToRead => FullTables;
        protected override IReadOnlyList<(string Table, string Reason)> TablesToCount => CountOnlyTables;
    }
}
