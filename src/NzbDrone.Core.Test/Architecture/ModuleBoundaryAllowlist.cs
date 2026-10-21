namespace NzbDrone.Core.Test.Architecture
{
    // Core types that reference module code today (ModuleBoundaryFixture). This list may only shrink:
    // remove an entry when its type stops referencing a module; never add one. Each group says how it
    // leaves the list. Seeded in step 9a with 159 types; 156 after 9b, 141 after 9c and 9d, 135 after 9e, 129 after 9f, 72 after contract 7 (notifications).
    public static class ModuleBoundaryAllowlist
    {
        public static readonly string[] Types =
        {
            // Analytics (1): Analytics is off in code; drop the history read.
            "NzbDrone.Core.Analytics.AnalyticsService",

            // Configuration (2): Movie config keys (file dates, TMDb certification country) move to module config.
            "NzbDrone.Core.Configuration.ConfigService",
            "NzbDrone.Core.Configuration.IConfigService",

            // CustomFormats (13): Global custom formats read movie input. Generalize CustomFormatInput before Phase 3 (finding 21: IndexerFlags values collide with Sonarr).
            "NzbDrone.Core.CustomFormats.CustomFormatCalculationService",
            "NzbDrone.Core.CustomFormats.CustomFormatInput",
            "NzbDrone.Core.CustomFormats.EditionSpecification",
            "NzbDrone.Core.CustomFormats.ICustomFormatCalculationService",
            "NzbDrone.Core.CustomFormats.IndexerFlagSpecification",
            "NzbDrone.Core.CustomFormats.IndexerFlagSpecificationValidator",
            "NzbDrone.Core.CustomFormats.LanguageSpecification",
            "NzbDrone.Core.CustomFormats.QualityModifierSpecification",
            "NzbDrone.Core.CustomFormats.ReleaseGroupSpecification",
            "NzbDrone.Core.CustomFormats.ReleaseTitleSpecification",
            "NzbDrone.Core.CustomFormats.ResolutionSpecification",
            "NzbDrone.Core.CustomFormats.SourceSpecification",
            "NzbDrone.Core.CustomFormats.YearSpecification",

            // DiskSpace (1): Root folders come from each module.
            "NzbDrone.Core.DiskSpace.DiskSpaceService",

            // Download (43): Contract 2: tracked downloads and clients work on RemoteRelease (contract 5, import, left in 9e; contract 6, the queue and pending releases, in 9f). Clients use three helpers that belong in core (torrent file reader, file name cleaning, blocklist hash check).
            "NzbDrone.Core.Download.Aggregation.Aggregators.AggregateLanguages",
            "NzbDrone.Core.Download.Aggregation.Aggregators.IAggregateRemoteMovie",
            "NzbDrone.Core.Download.Aggregation.IRemoteMovieAggregationService",
            "NzbDrone.Core.Download.Aggregation.RemoteMovieAggregationService",
            "NzbDrone.Core.Download.Clients.Aria2.Aria2",
            "NzbDrone.Core.Download.Clients.Blackhole.ScanWatchFolder",
            "NzbDrone.Core.Download.Clients.Blackhole.TorrentBlackhole",
            "NzbDrone.Core.Download.Clients.Blackhole.UsenetBlackhole",
            "NzbDrone.Core.Download.Clients.Deluge.Deluge",
            "NzbDrone.Core.Download.Clients.DownloadStation.TorrentDownloadStation",
            "NzbDrone.Core.Download.Clients.DownloadStation.UsenetDownloadStation",
            "NzbDrone.Core.Download.Clients.Flood.Flood",
            "NzbDrone.Core.Download.Clients.FreeboxDownload.TorrentFreeboxDownload",
            "NzbDrone.Core.Download.Clients.Hadouken.Hadouken",
            "NzbDrone.Core.Download.Clients.NzbVortex.NzbVortex",
            "NzbDrone.Core.Download.Clients.Nzbget.Nzbget",
            "NzbDrone.Core.Download.Clients.Pneumatic.Pneumatic",
            "NzbDrone.Core.Download.Clients.QBittorrent.QBittorrent",
            "NzbDrone.Core.Download.Clients.RQBit.RQBit",
            "NzbDrone.Core.Download.Clients.RTorrent.RTorrent",
            "NzbDrone.Core.Download.Clients.Sabnzbd.Sabnzbd",
            "NzbDrone.Core.Download.Clients.Transmission.Transmission",
            "NzbDrone.Core.Download.Clients.Transmission.TransmissionBase",
            "NzbDrone.Core.Download.Clients.UTorrent.UTorrent",
            "NzbDrone.Core.Download.Clients.Vuze.Vuze",
            "NzbDrone.Core.Download.DownloadClientBase`1",
            "NzbDrone.Core.Download.DownloadSeedConfigProvider",
            "NzbDrone.Core.Download.DownloadService",
            "NzbDrone.Core.Download.FailedDownloadService",
            "NzbDrone.Core.Download.History.DownloadHistoryService",
            "NzbDrone.Core.Download.IDownloadClient",
            "NzbDrone.Core.Download.IDownloadService",
            "NzbDrone.Core.Download.IProcessDownloadDecisions",
            "NzbDrone.Core.Download.IgnoredDownloadService",
            "NzbDrone.Core.Download.MovieGrabbedEvent",
            "NzbDrone.Core.Download.ProcessDownloadDecisions",
            "NzbDrone.Core.Download.ProcessedDecisions",
            "NzbDrone.Core.Download.RedownloadFailedDownloadService",
            "NzbDrone.Core.Download.TorrentClientBase`1",
            "NzbDrone.Core.Download.TrackedDownloads.DownloadMonitoringService",
            "NzbDrone.Core.Download.TrackedDownloads.TrackedDownload",
            "NzbDrone.Core.Download.TrackedDownloads.TrackedDownloadService",
            "NzbDrone.Core.Download.UsenetClientBase`1",

            // HealthCheck (5): Core checks that read movie root folders, paths, or import events; they ask every module.
            "NzbDrone.Core.HealthCheck.Checks.DownloadClientRemovesCompletedDownloadsCheck",
            "NzbDrone.Core.HealthCheck.Checks.DownloadClientRootFolderCheck",
            "NzbDrone.Core.HealthCheck.Checks.DownloadClientSortingCheck",
            "NzbDrone.Core.HealthCheck.Checks.MountCheck",
            "NzbDrone.Core.HealthCheck.Checks.RemotePathMappingCheck",

            // Housekeeping (1): Tag cleanup asks each module which tags it uses.
            "NzbDrone.Core.Housekeeping.Housekeepers.CleanupUnusedTags",

            // Parser (1): ReleaseGroupParser strips media file extensions, which each module defines (MediaFileExtensions maps them to video qualities).
            "NzbDrone.Core.Parser.ReleaseGroupParser",

            // Profiles (1): Profile in-use check asks each module.
            "NzbDrone.Core.Profiles.Qualities.QualityProfileService",

            // Tags (1): Tag usage details come from each module.
            "NzbDrone.Core.Tags.TagService",

            // Validation (3): Root folder validators check every module's root folders.
            "NzbDrone.Core.Validation.Paths.RootFolderAncestorValidator",
            "NzbDrone.Core.Validation.Paths.RootFolderExistsValidator",
            "NzbDrone.Core.Validation.Paths.RootFolderValidator"
        };
    }
}
