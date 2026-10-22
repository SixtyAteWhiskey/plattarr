using System.Collections.Generic;

namespace NzbDrone.Core.Upstream
{
    // Reads prowlarr.db (docs/importer.md, "From prowlarr.db").
    //
    // Indexers, Applications and DownloadClients settings hold credentials and API
    // keys. They are read into memory for the planner and must never be logged or
    // written to the report.
    //
    // Config is counted, not read: the dump showed it holds secrets under keys that
    // are not readable names (docs/importer.md, "Dump findings", item 4). That
    // conflicts with the mapping table, which imports global settings from it; until
    // the design settles which keys are wanted, the adapter takes none.
    public class ProwlarrAdapter : UpstreamAdapterBase
    {
        private static readonly VersionRange Versions = new VersionRange(1, 44);

        private static readonly string[] FullTables =
        {
            "Tags",
            "AppSyncProfiles",
            "Indexers",
            "IndexerProxies",
            "Applications",
            "ApplicationIndexerMapping",
            "DownloadClients",
            "Notifications",
            "CustomFilters"
        };

        private static readonly (string Table, string Reason)[] CountOnlyTables =
        {
            ("Config", SkipReason.Instance),
            ("History", SkipReason.Stats),
            ("Users", SkipReason.Instance),
            ("VersionInfo", SkipReason.Instance),
            ("IndexerStatus", SkipReason.Transient),
            ("IndexerDefinitionVersions", SkipReason.Transient),
            ("ApplicationStatus", SkipReason.Transient),
            ("DownloadClientStatus", SkipReason.Transient),
            ("NotificationStatus", SkipReason.Transient),
            ("Commands", SkipReason.Transient),
            ("ScheduledTasks", SkipReason.Transient),
            ("UpdateHistory", SkipReason.Transient),
            ("Logs", SkipReason.Transient)
        };

        public override string AppName => "Prowlarr";
        public override string DatabaseFileName => "prowlarr.db";
        public override VersionRange SupportedVersions => Versions;

        protected override IReadOnlyList<string> RequiredTables => new[] { "VersionInfo", "Indexers", "AppSyncProfiles" };
        protected override IReadOnlyList<string> TablesToRead => FullTables;
        protected override IReadOnlyList<(string Table, string Reason)> TablesToCount => CountOnlyTables;
    }
}
