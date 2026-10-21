namespace NzbDrone.Core.Test.Architecture
{
    // Core types that reference module code today (ModuleBoundaryFixture). This list may only shrink:
    // remove an entry when its type stops referencing a module; never add one. Each group says how it
    // leaves the list. Seeded in step 9a with 159 types; 156 after 9b.
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

            // Download (54): Contracts 2 and 5: tracked downloads, pending releases, and clients work on RemoteRelease. Clients use three helpers that belong in core (torrent file reader, file name cleaning, blocklist hash check).
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
            "NzbDrone.Core.Download.CompletedDownloadService",
            "NzbDrone.Core.Download.DownloadClientBase`1",
            "NzbDrone.Core.Download.DownloadSeedConfigProvider",
            "NzbDrone.Core.Download.DownloadService",
            "NzbDrone.Core.Download.FailedDownloadService",
            "NzbDrone.Core.Download.History.DownloadHistoryService",
            "NzbDrone.Core.Download.ICompletedDownloadService",
            "NzbDrone.Core.Download.IDownloadClient",
            "NzbDrone.Core.Download.IDownloadService",
            "NzbDrone.Core.Download.IProcessDownloadDecisions",
            "NzbDrone.Core.Download.IRejectedImportService",
            "NzbDrone.Core.Download.IgnoredDownloadService",
            "NzbDrone.Core.Download.ManualInteractionRequiredEvent",
            "NzbDrone.Core.Download.MovieGrabbedEvent",
            "NzbDrone.Core.Download.Pending.IPendingReleaseService",
            "NzbDrone.Core.Download.Pending.PendingRelease",
            "NzbDrone.Core.Download.Pending.PendingReleaseRepository",
            "NzbDrone.Core.Download.Pending.PendingReleaseService",
            "NzbDrone.Core.Download.ProcessDownloadDecisions",
            "NzbDrone.Core.Download.ProcessedDecisions",
            "NzbDrone.Core.Download.RedownloadFailedDownloadService",
            "NzbDrone.Core.Download.RejectedImportService",
            "NzbDrone.Core.Download.TorrentClientBase`1",
            "NzbDrone.Core.Download.TrackedDownloads.DownloadMonitoringService",
            "NzbDrone.Core.Download.TrackedDownloads.ITrackedDownloadAlreadyImported",
            "NzbDrone.Core.Download.TrackedDownloads.TrackedDownload",
            "NzbDrone.Core.Download.TrackedDownloads.TrackedDownloadAlreadyImported",
            "NzbDrone.Core.Download.TrackedDownloads.TrackedDownloadService",
            "NzbDrone.Core.Download.UsenetClientBase`1",

            // HealthCheck (13): Movie checks move into the module; download client checks read root folders from every module.
            "NzbDrone.Core.HealthCheck.Checks.DownloadClientRemovesCompletedDownloadsCheck",
            "NzbDrone.Core.HealthCheck.Checks.DownloadClientRootFolderCheck",
            "NzbDrone.Core.HealthCheck.Checks.DownloadClientSortingCheck",
            "NzbDrone.Core.HealthCheck.Checks.ImportListRootFolderCheck",
            "NzbDrone.Core.HealthCheck.Checks.ImportListStatusCheck",
            "NzbDrone.Core.HealthCheck.Checks.MetadataCheck",
            "NzbDrone.Core.HealthCheck.Checks.MountCheck",
            "NzbDrone.Core.HealthCheck.Checks.MovieCollectionRootFolderCheck",
            "NzbDrone.Core.HealthCheck.Checks.NamingConfigCheck",
            "NzbDrone.Core.HealthCheck.Checks.RecyclingBinCheck",
            "NzbDrone.Core.HealthCheck.Checks.RemotePathMappingCheck",
            "NzbDrone.Core.HealthCheck.Checks.RemovedMovieCheck",
            "NzbDrone.Core.HealthCheck.Checks.RootFolderCheck",

            // Housekeeping (4): Movie housekeepers move into the module.
            "NzbDrone.Core.Housekeeping.Housekeepers.CleanupUnusedTags",
            "NzbDrone.Core.Housekeeping.Housekeepers.DeleteBadMediaCovers",
            "NzbDrone.Core.Housekeeping.Housekeepers.FixFutureImportListStatusTimes",
            "NzbDrone.Core.Housekeeping.Housekeepers.UpdateCleanTitleForMovies",

            // Notifications (56): Contract 7, before Phase 3.
            "NzbDrone.Core.Notifications.Apprise.Apprise",
            "NzbDrone.Core.Notifications.CustomScript.CustomScript",
            "NzbDrone.Core.Notifications.DeleteMessage",
            "NzbDrone.Core.Notifications.Discord.Discord",
            "NzbDrone.Core.Notifications.DownloadMessage",
            "NzbDrone.Core.Notifications.Email.Email",
            "NzbDrone.Core.Notifications.Emby.IMediaBrowserService",
            "NzbDrone.Core.Notifications.Emby.MediaBrowser",
            "NzbDrone.Core.Notifications.Emby.MediaBrowserProxy",
            "NzbDrone.Core.Notifications.Emby.MediaBrowserService",
            "NzbDrone.Core.Notifications.Gotify.Gotify",
            "NzbDrone.Core.Notifications.GrabMessage",
            "NzbDrone.Core.Notifications.INotification",
            "NzbDrone.Core.Notifications.Join.Join",
            "NzbDrone.Core.Notifications.Mailgun.MailGun",
            "NzbDrone.Core.Notifications.ManualInteractionRequiredMessage",
            "NzbDrone.Core.Notifications.MediaServerUpdateQueue`2",
            "NzbDrone.Core.Notifications.MovieDeleteMessage",
            "NzbDrone.Core.Notifications.MovieFileDeleteMessage",
            "NzbDrone.Core.Notifications.Notifiarr.Notifiarr",
            "NzbDrone.Core.Notifications.NotificationBase`1",
            "NzbDrone.Core.Notifications.NotificationMetadataLinkGenerator",
            "NzbDrone.Core.Notifications.NotificationService",
            "NzbDrone.Core.Notifications.Ntfy.Ntfy",
            "NzbDrone.Core.Notifications.Plex.Server.IPlexServerService",
            "NzbDrone.Core.Notifications.Plex.Server.PlexServer",
            "NzbDrone.Core.Notifications.Plex.Server.PlexServerService",
            "NzbDrone.Core.Notifications.Prowl.Prowl",
            "NzbDrone.Core.Notifications.PushBullet.PushBullet",
            "NzbDrone.Core.Notifications.Pushcut.Pushcut",
            "NzbDrone.Core.Notifications.Pushover.Pushover",
            "NzbDrone.Core.Notifications.Pushsafer.Pushsafer",
            "NzbDrone.Core.Notifications.SendGrid.SendGrid",
            "NzbDrone.Core.Notifications.Signal.Signal",
            "NzbDrone.Core.Notifications.Simplepush.Simplepush",
            "NzbDrone.Core.Notifications.Slack.Slack",
            "NzbDrone.Core.Notifications.Synology.SynologyIndexer",
            "NzbDrone.Core.Notifications.Telegram.Telegram",
            "NzbDrone.Core.Notifications.Trakt.Trakt",
            "NzbDrone.Core.Notifications.Twitter.Twitter",
            "NzbDrone.Core.Notifications.UpdateQueueItem`1",
            "NzbDrone.Core.Notifications.Webhook.Webhook",
            "NzbDrone.Core.Notifications.Webhook.WebhookAddedPayload",
            "NzbDrone.Core.Notifications.Webhook.WebhookBase`1",
            "NzbDrone.Core.Notifications.Webhook.WebhookGrabbedRelease",
            "NzbDrone.Core.Notifications.Webhook.WebhookImage",
            "NzbDrone.Core.Notifications.Webhook.WebhookMovie",
            "NzbDrone.Core.Notifications.Webhook.WebhookMovieFile",
            "NzbDrone.Core.Notifications.Webhook.WebhookMovieFileDeletePayload",
            "NzbDrone.Core.Notifications.Webhook.WebhookMovieFileMediaInfo",
            "NzbDrone.Core.Notifications.Webhook.WebhookRelease",
            "NzbDrone.Core.Notifications.Webhook.WebhookRemoteMovie",
            "NzbDrone.Core.Notifications.Webhook.WebhookRenamedMovieFile",
            "NzbDrone.Core.Notifications.Xbmc.IXbmcService",
            "NzbDrone.Core.Notifications.Xbmc.Xbmc",
            "NzbDrone.Core.Notifications.Xbmc.XbmcService",

            // Parser (2): Core parsers use two module helpers; move the helpers to core.
            "NzbDrone.Core.Parser.IsoLanguages",
            "NzbDrone.Core.Parser.ReleaseGroupParser",

            // Profiles (1): Profile in-use check asks each module.
            "NzbDrone.Core.Profiles.Qualities.QualityProfileService",

            // Queue (2): Contract 6 (activity rendering).
            "NzbDrone.Core.Queue.Queue",
            "NzbDrone.Core.Queue.QueueService",

            // Tags (1): Tag usage details come from each module.
            "NzbDrone.Core.Tags.TagService",

            // Validation (6): Movie path validators move into the module; root folder validators read every module.
            "NzbDrone.Core.Validation.Paths.MovieAncestorValidator",
            "NzbDrone.Core.Validation.Paths.MovieExistsValidator",
            "NzbDrone.Core.Validation.Paths.MoviePathValidator",
            "NzbDrone.Core.Validation.Paths.RootFolderAncestorValidator",
            "NzbDrone.Core.Validation.Paths.RootFolderExistsValidator",
            "NzbDrone.Core.Validation.Paths.RootFolderValidator"
        };
    }
}
