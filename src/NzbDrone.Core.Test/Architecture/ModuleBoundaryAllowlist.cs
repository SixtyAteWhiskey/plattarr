namespace NzbDrone.Core.Test.Architecture
{
    // Core types that reference module code today (ModuleBoundaryFixture). This list may only shrink:
    // remove an entry when its type stops referencing a module; never add one. Each group says how it
    // leaves the list. Seeded in step 9a with 159 types; 156 after 9b, 141 after 9c and 9d, 135 after 9e, 129 after 9f, 72 after contract 7 (notifications), 48 after 9h (download clients; 9i changed nothing), 42 after 9j (grab and decision processing).
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

            // Download (3): Clients and their base classes were retyped to RemoteRelease in 9h, the grab and decision processing path (DownloadService, ProcessDownloadDecisions) in 9j, the failure, ignore, redownload, download history and seed configuration services in 9k, and the aggregators (movie parse results) moved into Movies.Aggregation in 9l. ScanWatchFolder left the list when the Blackhole clients were pruned. What is left: tracked downloads.
            "NzbDrone.Core.Download.TrackedDownloads.DownloadMonitoringService",
            "NzbDrone.Core.Download.TrackedDownloads.TrackedDownload",
            "NzbDrone.Core.Download.TrackedDownloads.TrackedDownloadService",

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
