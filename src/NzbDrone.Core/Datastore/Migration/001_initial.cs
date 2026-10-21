using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    // Plattarr's schema (porting step 8, docs/module-interface.md "Database
    // migrations"). One chain and one VersionInfo, starting here. It replaces
    // Radarr's migrations 1 to 242 and Plattarr's 243 and 244: the tables are the
    // ones those produced on Postgres, with Radarr's legacy indexer tables dropped
    // and Prowlarr's indexer tables under their plain names (Indexers,
    // IndexerStatus). Existing Radarr and Prowlarr data comes in through the
    // importer, not through migrations; PreviousSchemaCheck refuses an upstream
    // database.
    //
    // Changes from the upstream chain's result:
    // - AlternativeTitles.SourceType, ImportExclusions.TmdbId and MovieYear are
    //   32-bit, matching their models (Postgres had them as bigint).
    // - QualityProfiles.FormatItems defaults to "[]" (Postgres had a pre-custom-format
    //   shape left over from an early migration; every insert supplies the value).
    // - Index names follow their tables (IX_ImportLists_Name,
    //   IX_ImportListStatus_ProviderId, IX_QualityProfiles_Name).
    // - History, DownloadHistory, Blocklist, and PendingReleases carry MediaKind
    //   (step 8c, docs/module-interface.md contract 6); their MovieId indexes
    //   lead with it.
    // - SQLite gets the column defaults and unique constraints Postgres had;
    //   upstream table rebuilds on SQLite had dropped some defaults, and SQLite
    //   kept unique constraints Postgres had dropped.
    [Migration(1)]
    public class plattarr_initial : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // FluentMigrator creates VersionInfo itself, without a time zone; every other
            // timestamp column on Postgres has one (upstream migration 212 did the same).
            IfDatabase(ProcessorIdConstants.PostgreSQL).Alter.Table("VersionInfo").AlterColumn("AppliedOn").AsDateTimeOffset().Nullable();

            // Core: configuration, users, scheduling
            Create.TableForModel("Config")
                .WithColumn("Key").AsString().Unique()
                .WithColumn("Value").AsString();

            Create.TableForModel("Users")
                .WithColumn("Identifier").AsString().Unique()
                .WithColumn("Username").AsString().Unique()
                .WithColumn("Password").AsString()
                .WithColumn("Salt").AsString().Nullable()
                .WithColumn("Iterations").AsInt32().Nullable();

            Create.TableForModel("Tags")
                .WithColumn("Label").AsString().Unique();

            Create.TableForModel("CustomFilters")
                .WithColumn("Type").AsString()
                .WithColumn("Label").AsString()
                .WithColumn("Filters").AsString();

            Create.TableForModel("ScheduledTasks")
                .WithColumn("TypeName").AsString().Unique()
                .WithColumn("Interval").AsDouble()
                .WithColumn("LastExecution").AsDateTimeOffset()
                .WithColumn("LastStartTime").AsDateTimeOffset().Nullable();

            Create.TableForModel("Commands")
                .WithColumn("Name").AsString()
                .WithColumn("Body").AsString()
                .WithColumn("Priority").AsInt32()
                .WithColumn("Status").AsInt32()
                .WithColumn("QueuedAt").AsDateTimeOffset()
                .WithColumn("StartedAt").AsDateTimeOffset().Nullable()
                .WithColumn("EndedAt").AsDateTimeOffset().Nullable()
                .WithColumn("Duration").AsString().Nullable()
                .WithColumn("Exception").AsString().Nullable()
                .WithColumn("Trigger").AsInt32()
                .WithColumn("Result").AsInt32().WithDefaultValue(1);

            Create.TableForModel("RemotePathMappings")
                .WithColumn("Host").AsString()
                .WithColumn("RemotePath").AsString()
                .WithColumn("LocalPath").AsString();

            // Core: indexers (Prowlarr's layer)
            Create.TableForModel("Indexers")
                .WithColumn("Name").AsString().Unique()
                .WithColumn("Implementation").AsString()
                .WithColumn("Settings").AsString().Nullable()
                .WithColumn("ConfigContract").AsString().Nullable()
                .WithColumn("Enable").AsBoolean().Nullable()
                .WithColumn("Priority").AsInt32().WithDefaultValue(25)
                .WithColumn("Added").AsDateTimeOffset()
                .WithColumn("Redirect").AsBoolean().WithDefaultValue(false)
                .WithColumn("Tags").AsString().Nullable()
                .WithColumn("DownloadClientId").AsInt32().WithDefaultValue(0)
                .WithColumn("EnableRss").AsBoolean().WithDefaultValue(true)
                .WithColumn("EnableAutomaticSearch").AsBoolean().WithDefaultValue(true)
                .WithColumn("EnableInteractiveSearch").AsBoolean().WithDefaultValue(true);

            Create.TableForModel("IndexerStatus")
                .WithColumn("ProviderId").AsInt32().Unique()
                .WithColumn("InitialFailure").AsDateTimeOffset().Nullable()
                .WithColumn("MostRecentFailure").AsDateTimeOffset().Nullable()
                .WithColumn("EscalationLevel").AsInt32()
                .WithColumn("DisabledTill").AsDateTimeOffset().Nullable()
                .WithColumn("LastRssSyncReleaseInfo").AsString().Nullable()
                .WithColumn("Cookies").AsString().Nullable()
                .WithColumn("CookiesExpirationDate").AsDateTimeOffset().Nullable();

            Create.TableForModel("IndexerProxies")
                .WithColumn("Name").AsString()
                .WithColumn("Settings").AsString()
                .WithColumn("Implementation").AsString()
                .WithColumn("ConfigContract").AsString().Nullable()
                .WithColumn("Tags").AsString().Nullable();

            Create.TableForModel("IndexerHistory")
                .WithColumn("IndexerId").AsInt32()
                .WithColumn("Date").AsDateTimeOffset()
                .WithColumn("Data").AsString()
                .WithColumn("EventType").AsInt32().Nullable()
                .WithColumn("DownloadId").AsString().Nullable()
                .WithColumn("Successful").AsBoolean().WithDefaultValue(true);

            Create.Index("IX_IndexerHistory_DownloadId").OnTable("IndexerHistory").OnColumn("DownloadId").Ascending();

            Create.Index("IX_IndexerHistory_IndexerId_Date").OnTable("IndexerHistory").OnColumn("IndexerId").Ascending().OnColumn("Date").Ascending();

            Create.TableForModel("IndexerDefinitionVersions")
                .WithColumn("DefinitionId").AsString().Unique()
                .WithColumn("File").AsString().Unique()
                .WithColumn("Sha").AsString().Nullable()
                .WithColumn("LastUpdated").AsDateTimeOffset().Nullable();

            Create.TableForModel("IndexerOverrides")
                .WithColumn("IndexerId").AsInt32()
                .WithColumn("MediaKind").AsInt32()
                .WithColumn("MultiLanguages").AsString().WithDefaultValue("[]")
                .WithColumn("RequiredFlags").AsString().WithDefaultValue("[]")
                .WithColumn("RemoveYear").AsBoolean().Nullable()
                .WithColumn("Categories").AsString().WithDefaultValue("[]");

            Create.Index("IX_IndexerOverrides_IndexerId_MediaKind").OnTable("IndexerOverrides").OnColumn("IndexerId").Ascending().OnColumn("MediaKind").Ascending().WithOptions().Unique();

            // Core: download clients and notifications
            Create.TableForModel("DownloadClients")
                .WithColumn("Enable").AsBoolean()
                .WithColumn("Name").AsString()
                .WithColumn("Implementation").AsString()
                .WithColumn("Settings").AsString()
                .WithColumn("ConfigContract").AsString()
                .WithColumn("Priority").AsInt32().WithDefaultValue(1)
                .WithColumn("RemoveCompletedDownloads").AsBoolean().WithDefaultValue(true)
                .WithColumn("RemoveFailedDownloads").AsBoolean().WithDefaultValue(true)
                .WithColumn("Tags").AsString().Nullable();

            Create.TableForModel("DownloadClientStatus")
                .WithColumn("ProviderId").AsInt32().Unique()
                .WithColumn("InitialFailure").AsDateTimeOffset().Nullable()
                .WithColumn("MostRecentFailure").AsDateTimeOffset().Nullable()
                .WithColumn("EscalationLevel").AsInt32()
                .WithColumn("DisabledTill").AsDateTimeOffset().Nullable();

            Create.TableForModel("Notifications")
                .WithColumn("Name").AsString()
                .WithColumn("OnGrab").AsBoolean()
                .WithColumn("OnDownload").AsBoolean()
                .WithColumn("Settings").AsString()
                .WithColumn("Implementation").AsString()
                .WithColumn("ConfigContract").AsString().Nullable()
                .WithColumn("OnUpgrade").AsBoolean().Nullable()
                .WithColumn("Tags").AsString().Nullable()
                .WithColumn("OnRename").AsBoolean()
                .WithColumn("OnHealthIssue").AsBoolean().WithDefaultValue(false)
                .WithColumn("IncludeHealthWarnings").AsBoolean().WithDefaultValue(false)
                .WithColumn("OnMovieDelete").AsBoolean().WithDefaultValue(false)
                .WithColumn("OnMovieFileDelete").AsBoolean().WithDefaultValue(false)
                .WithColumn("OnMovieFileDeleteForUpgrade").AsBoolean().WithDefaultValue(false)
                .WithColumn("OnApplicationUpdate").AsBoolean().WithDefaultValue(false)
                .WithColumn("OnMovieAdded").AsBoolean().WithDefaultValue(false)
                .WithColumn("OnHealthRestored").AsBoolean().WithDefaultValue(false)
                .WithColumn("OnManualInteractionRequired").AsBoolean().WithDefaultValue(false);

            Create.TableForModel("NotificationStatus")
                .WithColumn("ProviderId").AsInt32().Unique()
                .WithColumn("InitialFailure").AsDateTimeOffset().Nullable()
                .WithColumn("MostRecentFailure").AsDateTimeOffset().Nullable()
                .WithColumn("EscalationLevel").AsInt32()
                .WithColumn("DisabledTill").AsDateTimeOffset().Nullable();

            // Core: profiles and custom formats
            Create.TableForModel("QualityDefinitions")
                .WithColumn("Quality").AsInt32().Unique()
                .WithColumn("Title").AsString().Unique()
                .WithColumn("MinSize").AsDouble().Nullable()
                .WithColumn("MaxSize").AsDouble().Nullable()
                .WithColumn("PreferredSize").AsDouble().Nullable();

            Create.TableForModel("QualityProfiles")
                .WithColumn("Name").AsString().Unique()
                .WithColumn("Cutoff").AsInt32()
                .WithColumn("Items").AsString()
                .WithColumn("Language").AsInt32().Nullable()
                .WithColumn("FormatItems").AsString().WithDefaultValue("[]")
                .WithColumn("UpgradeAllowed").AsBoolean().Nullable()
                .WithColumn("MinFormatScore").AsInt32().WithDefaultValue(0)
                .WithColumn("CutoffFormatScore").AsInt32().WithDefaultValue(0)
                .WithColumn("MinUpgradeFormatScore").AsInt32().WithDefaultValue(1);

            Create.TableForModel("CustomFormats")
                .WithColumn("Name").AsString().Unique()
                .WithColumn("Specifications").AsString().WithDefaultValue("[]")
                .WithColumn("IncludeCustomFormatWhenRenaming").AsBoolean().WithDefaultValue(false);

            Create.TableForModel("DelayProfiles")
                .WithColumn("EnableUsenet").AsBoolean()
                .WithColumn("EnableTorrent").AsBoolean()
                .WithColumn("PreferredProtocol").AsInt32()
                .WithColumn("UsenetDelay").AsInt32()
                .WithColumn("TorrentDelay").AsInt32()
                .WithColumn("Order").AsInt32()
                .WithColumn("Tags").AsString()
                .WithColumn("BypassIfHighestQuality").AsBoolean().WithDefaultValue(false)
                .WithColumn("BypassIfAboveCustomFormatScore").AsBoolean().WithDefaultValue(false)
                .WithColumn("MinimumCustomFormatScore").AsInt32().Nullable();

            // The default delay profile applies to untagged media; it must always exist.
            Insert.IntoTable("DelayProfiles").Row(new
            {
                EnableUsenet = true,
                EnableTorrent = true,
                PreferredProtocol = 1,
                UsenetDelay = 0,
                TorrentDelay = 0,
                Order = int.MaxValue,
                Tags = "[]",
                BypassIfHighestQuality = true,
                BypassIfAboveCustomFormatScore = false
            });

            Create.TableForModel("ReleaseProfiles")
                .WithColumn("Required").AsString().Nullable()
                .WithColumn("Ignored").AsString().Nullable()
                .WithColumn("Tags").AsString()
                .WithColumn("Name").AsString().Nullable()
                .WithColumn("Enabled").AsBoolean().WithDefaultValue(true)
                .WithColumn("IndexerId").AsInt32().WithDefaultValue(0);

            Create.TableForModel("AutoTagging")
                .WithColumn("Name").AsString().Unique()
                .WithColumn("Specifications").AsString().WithDefaultValue("[]")
                .WithColumn("RemoveTagsAutomatically").AsBoolean().WithDefaultValue(false)
                .WithColumn("Tags").AsString().WithDefaultValue("[]");

            // Activity
            // Activity tables are shared by every media type (module spec, contract 6).
            // MediaKind has no default; MediaKindRepository refuses rows without one.
            Create.TableForModel("History")
                .WithColumn("MediaKind").AsInt32()
                .WithColumn("SourceTitle").AsString()
                .WithColumn("Date").AsDateTimeOffset()
                .WithColumn("Quality").AsString()
                .WithColumn("Data").AsString()
                .WithColumn("EventType").AsInt32().Nullable()
                .WithColumn("DownloadId").AsString().Nullable()
                .WithColumn("MovieId").AsInt32().WithDefaultValue(0)
                .WithColumn("Languages").AsString().WithDefaultValue("[]");

            Create.Index("IX_History_Date").OnTable("History").OnColumn("Date").Ascending();

            Create.Index("IX_History_DownloadId_Date").OnTable("History").OnColumn("DownloadId").Ascending().OnColumn("Date").Descending();

            Create.Index("IX_History_MediaKind_MovieId_Date").OnTable("History").OnColumn("MediaKind").Ascending().OnColumn("MovieId").Ascending().OnColumn("Date").Descending();

            Create.TableForModel("DownloadHistory")
                .WithColumn("MediaKind").AsInt32()
                .WithColumn("EventType").AsInt32()
                .WithColumn("MovieId").AsInt32()
                .WithColumn("DownloadId").AsString()
                .WithColumn("SourceTitle").AsString()
                .WithColumn("Date").AsDateTimeOffset()
                .WithColumn("Protocol").AsInt32().Nullable()
                .WithColumn("IndexerId").AsInt32().Nullable()
                .WithColumn("DownloadClientId").AsInt32().Nullable()
                .WithColumn("Release").AsString().Nullable()
                .WithColumn("Data").AsString().Nullable();

            Create.Index("IX_DownloadHistory_DownloadId").OnTable("DownloadHistory").OnColumn("DownloadId").Ascending();

            Create.Index("IX_DownloadHistory_EventType").OnTable("DownloadHistory").OnColumn("EventType").Ascending();

            Create.Index("IX_DownloadHistory_MediaKind_MovieId").OnTable("DownloadHistory").OnColumn("MediaKind").Ascending().OnColumn("MovieId").Ascending();

            Create.TableForModel("Blocklist")
                .WithColumn("MediaKind").AsInt32()
                .WithColumn("SourceTitle").AsString()
                .WithColumn("Quality").AsString()
                .WithColumn("Date").AsDateTimeOffset()
                .WithColumn("PublishedDate").AsDateTimeOffset().Nullable()
                .WithColumn("Size").AsInt64().Nullable()
                .WithColumn("Protocol").AsInt32().Nullable()
                .WithColumn("Indexer").AsString().Nullable()
                .WithColumn("Message").AsString().Nullable()
                .WithColumn("TorrentInfoHash").AsString().Nullable()
                .WithColumn("MovieId").AsInt32().Nullable().WithDefaultValue(0)
                .WithColumn("Languages").AsString().WithDefaultValue("[]")
                .WithColumn("IndexerFlags").AsInt32().WithDefaultValue(0);

            Create.Index("IX_Blocklist_Date").OnTable("Blocklist").OnColumn("Date").Ascending();

            Create.Index("IX_Blocklist_MediaKind_MovieId").OnTable("Blocklist").OnColumn("MediaKind").Ascending().OnColumn("MovieId").Ascending();

            Create.TableForModel("PendingReleases")
                .WithColumn("MediaKind").AsInt32()
                .WithColumn("Title").AsString()
                .WithColumn("Added").AsDateTimeOffset()
                .WithColumn("Release").AsString()
                .WithColumn("MovieId").AsInt32().WithDefaultValue(0)
                .WithColumn("ParsedMovieInfo").AsString().Nullable()
                .WithColumn("Reason").AsInt32().WithDefaultValue(0)
                .WithColumn("AdditionalInfo").AsString().Nullable();

            // Movies: module-owned tables carry the module prefix (module spec, boundary rule 4)
            Create.TableForModel("Movies_MovieMetadata")
                .WithColumn("TmdbId").AsInt32().Unique()
                .WithColumn("ImdbId").AsString().Nullable()
                .WithColumn("Images").AsString()
                .WithColumn("Genres").AsString().Nullable()
                .WithColumn("Title").AsString()
                .WithColumn("SortTitle").AsString().Nullable()
                .WithColumn("CleanTitle").AsString().Nullable()
                .WithColumn("OriginalTitle").AsString().Nullable()
                .WithColumn("CleanOriginalTitle").AsString().Nullable()
                .WithColumn("OriginalLanguage").AsInt32()
                .WithColumn("Status").AsInt32()
                .WithColumn("LastInfoSync").AsDateTimeOffset().Nullable()
                .WithColumn("Runtime").AsInt32()
                .WithColumn("InCinemas").AsDateTimeOffset().Nullable()
                .WithColumn("PhysicalRelease").AsDateTimeOffset().Nullable()
                .WithColumn("DigitalRelease").AsDateTimeOffset().Nullable()
                .WithColumn("Year").AsInt32().Nullable()
                .WithColumn("SecondaryYear").AsInt32().Nullable()
                .WithColumn("Ratings").AsString().Nullable()
                .WithColumn("Recommendations").AsString()
                .WithColumn("Certification").AsString().Nullable()
                .WithColumn("YouTubeTrailerId").AsString().Nullable()
                .WithColumn("Studio").AsString().Nullable()
                .WithColumn("Overview").AsString().Nullable()
                .WithColumn("Website").AsString().Nullable()
                .WithColumn("Popularity").AsFloat().Nullable()
                .WithColumn("CollectionTmdbId").AsInt32().Nullable()
                .WithColumn("CollectionTitle").AsString().Nullable()
                .WithColumn("Keywords").AsString().Nullable();

            Create.Index("IX_Movies_MovieMetadata_CleanOriginalTitle").OnTable("Movies_MovieMetadata").OnColumn("CleanOriginalTitle").Ascending();

            Create.Index("IX_Movies_MovieMetadata_CleanTitle").OnTable("Movies_MovieMetadata").OnColumn("CleanTitle").Ascending();

            Create.Index("IX_Movies_MovieMetadata_CollectionTmdbId").OnTable("Movies_MovieMetadata").OnColumn("CollectionTmdbId").Ascending();

            Create.TableForModel("Movies_Movies")
                .WithColumn("Path").AsString()
                .WithColumn("Monitored").AsBoolean()
                .WithColumn("QualityProfileId").AsInt32()
                .WithColumn("Added").AsDateTimeOffset().Nullable()
                .WithColumn("Tags").AsString().Nullable()
                .WithColumn("AddOptions").AsString().Nullable()
                .WithColumn("MovieFileId").AsInt32().WithDefaultValue(0)
                .WithColumn("MinimumAvailability").AsInt32().WithDefaultValue(3)
                .WithColumn("MovieMetadataId").AsInt32().WithDefaultValue(0).Unique()
                .WithColumn("LastSearchTime").AsDateTimeOffset().Nullable();

            Create.Index("IX_Movies_Movies_MovieFileId").OnTable("Movies_Movies").OnColumn("MovieFileId").Ascending();

            Create.Index("IX_Movies_Movies_Path").OnTable("Movies_Movies").OnColumn("Path").Ascending();

            Create.TableForModel("Movies_MovieFiles")
                .WithColumn("MovieId").AsInt32()
                .WithColumn("Quality").AsString()
                .WithColumn("Size").AsInt64()
                .WithColumn("DateAdded").AsDateTimeOffset()
                .WithColumn("SceneName").AsString().Nullable()
                .WithColumn("MediaInfo").AsString().Nullable()
                .WithColumn("ReleaseGroup").AsString().Nullable()
                .WithColumn("RelativePath").AsString().Nullable()
                .WithColumn("Edition").AsString().Nullable()
                .WithColumn("Languages").AsString().WithDefaultValue("[]")
                .WithColumn("IndexerFlags").AsInt32().WithDefaultValue(0)
                .WithColumn("OriginalFilePath").AsString().Nullable();

            Create.Index("IX_Movies_MovieFiles_MovieId").OnTable("Movies_MovieFiles").OnColumn("MovieId").Ascending();

            Create.TableForModel("Movies_AlternativeTitles")
                .WithColumn("Title").AsString()
                .WithColumn("CleanTitle").AsString()
                .WithColumn("SourceType").AsInt32().WithDefaultValue(0)
                .WithColumn("MovieMetadataId").AsInt32().WithDefaultValue(0);

            Create.Index("IX_Movies_AlternativeTitles_CleanTitle").OnTable("Movies_AlternativeTitles").OnColumn("CleanTitle").Ascending();

            Create.Index("IX_Movies_AlternativeTitles_MovieMetadataId").OnTable("Movies_AlternativeTitles").OnColumn("MovieMetadataId").Ascending();

            Create.TableForModel("Movies_MovieTranslations")
                .WithColumn("Title").AsString().Nullable()
                .WithColumn("CleanTitle").AsString().Nullable()
                .WithColumn("Overview").AsString().Nullable()
                .WithColumn("Language").AsInt32()
                .WithColumn("MovieMetadataId").AsInt32().WithDefaultValue(0);

            Create.Index("IX_Movies_MovieTranslations_CleanTitle").OnTable("Movies_MovieTranslations").OnColumn("CleanTitle").Ascending();

            Create.Index("IX_Movies_MovieTranslations_Language").OnTable("Movies_MovieTranslations").OnColumn("Language").Ascending();

            Create.Index("IX_Movies_MovieTranslations_MovieMetadataId").OnTable("Movies_MovieTranslations").OnColumn("MovieMetadataId").Ascending();

            Create.TableForModel("Movies_Credits")
                .WithColumn("CreditTmdbId").AsString().Unique()
                .WithColumn("PersonTmdbId").AsInt32()
                .WithColumn("Name").AsString()
                .WithColumn("Images").AsString()
                .WithColumn("Character").AsString().Nullable()
                .WithColumn("Order").AsInt32()
                .WithColumn("Job").AsString().Nullable()
                .WithColumn("Department").AsString().Nullable()
                .WithColumn("Type").AsInt32()
                .WithColumn("MovieMetadataId").AsInt32().WithDefaultValue(0);

            Create.Index("IX_Movies_Credits_MovieMetadataId").OnTable("Movies_Credits").OnColumn("MovieMetadataId").Ascending();

            Create.TableForModel("Movies_Collections")
                .WithColumn("TmdbId").AsInt32().Unique()
                .WithColumn("QualityProfileId").AsInt32()
                .WithColumn("RootFolderPath").AsString()
                .WithColumn("MinimumAvailability").AsInt32()
                .WithColumn("SearchOnAdd").AsBoolean()
                .WithColumn("Title").AsString()
                .WithColumn("SortTitle").AsString().Nullable()
                .WithColumn("CleanTitle").AsString()
                .WithColumn("Overview").AsString().Nullable()
                .WithColumn("Images").AsString().WithDefaultValue("[]")
                .WithColumn("Monitored").AsBoolean().WithDefaultValue(false)
                .WithColumn("LastInfoSync").AsDateTimeOffset().Nullable()
                .WithColumn("Added").AsDateTimeOffset().Nullable()
                .WithColumn("Tags").AsString().Nullable();

            Create.TableForModel("Movies_ImportLists")
                .WithColumn("Enabled").AsBoolean()
                .WithColumn("Name").AsString().Unique()
                .WithColumn("Implementation").AsString()
                .WithColumn("ConfigContract").AsString().Nullable()
                .WithColumn("Settings").AsString().Nullable()
                .WithColumn("EnableAuto").AsBoolean()
                .WithColumn("RootFolderPath").AsString()
                .WithColumn("QualityProfileId").AsInt32()
                .WithColumn("MinimumAvailability").AsInt32().WithDefaultValue(3)
                .WithColumn("Tags").AsString().Nullable()
                .WithColumn("SearchOnAdd").AsBoolean().WithDefaultValue(false)
                .WithColumn("Monitor").AsInt32();

            Create.TableForModel("Movies_ImportListStatus")
                .WithColumn("ProviderId").AsInt32().Unique()
                .WithColumn("InitialFailure").AsDateTimeOffset().Nullable()
                .WithColumn("MostRecentFailure").AsDateTimeOffset().Nullable()
                .WithColumn("EscalationLevel").AsInt32()
                .WithColumn("DisabledTill").AsDateTimeOffset().Nullable()
                .WithColumn("LastInfoSync").AsDateTimeOffset().Nullable();

            Create.TableForModel("Movies_ImportListMovies")
                .WithColumn("ListId").AsInt32()
                .WithColumn("MovieMetadataId").AsInt32().WithDefaultValue(0);

            Create.Index("IX_Movies_ImportListMovies_MovieMetadataId").OnTable("Movies_ImportListMovies").OnColumn("MovieMetadataId").Ascending();

            Create.TableForModel("ImportExclusions")
                .WithColumn("TmdbId").AsInt32().Unique()
                .WithColumn("MovieTitle").AsString().Nullable()
                .WithColumn("MovieYear").AsInt32().Nullable().WithDefaultValue(0);

            Create.TableForModel("Movies_RootFolders")
                .WithColumn("Path").AsString().Unique();

            Create.TableForModel("Movies_NamingConfig")
                .WithColumn("ReplaceIllegalCharacters").AsBoolean().WithDefaultValue(true)
                .WithColumn("StandardMovieFormat").AsString().Nullable()
                .WithColumn("MovieFolderFormat").AsString().Nullable()
                .WithColumn("ColonReplacementFormat").AsInt32().WithDefaultValue(0)
                .WithColumn("RenameMovies").AsBoolean().WithDefaultValue(false);

            Create.TableForModel("Movies_Metadata")
                .WithColumn("Enable").AsBoolean()
                .WithColumn("Name").AsString()
                .WithColumn("Implementation").AsString()
                .WithColumn("Settings").AsString()
                .WithColumn("ConfigContract").AsString();

            Create.TableForModel("Movies_MetadataFiles")
                .WithColumn("MovieId").AsInt32()
                .WithColumn("Consumer").AsString()
                .WithColumn("Type").AsInt32()
                .WithColumn("RelativePath").AsString()
                .WithColumn("LastUpdated").AsDateTimeOffset()
                .WithColumn("MovieFileId").AsInt32().Nullable()
                .WithColumn("Hash").AsString().Nullable()
                .WithColumn("Added").AsDateTimeOffset().Nullable()
                .WithColumn("Extension").AsString();

            Create.TableForModel("Movies_ExtraFiles")
                .WithColumn("MovieId").AsInt32()
                .WithColumn("MovieFileId").AsInt32()
                .WithColumn("RelativePath").AsString()
                .WithColumn("Extension").AsString()
                .WithColumn("Added").AsDateTimeOffset()
                .WithColumn("LastUpdated").AsDateTimeOffset();

            Create.TableForModel("Movies_SubtitleFiles")
                .WithColumn("MovieId").AsInt32()
                .WithColumn("MovieFileId").AsInt32()
                .WithColumn("RelativePath").AsString()
                .WithColumn("Extension").AsString()
                .WithColumn("Added").AsDateTimeOffset()
                .WithColumn("LastUpdated").AsDateTimeOffset()
                .WithColumn("Language").AsInt32()
                .WithColumn("LanguageTags").AsString().Nullable()
                .WithColumn("Title").AsString().Nullable()
                .WithColumn("Copy").AsInt32().WithDefaultValue(0);
        }

        protected override void LogDbUpgrade()
        {
            IfDatabase(ProcessorIdConstants.PostgreSQL).Alter.Table("VersionInfo").AlterColumn("AppliedOn").AsDateTimeOffset().Nullable();

            // Log database
            Create.TableForModel("Logs")
                .WithColumn("Message").AsString()
                .WithColumn("Time").AsDateTimeOffset()
                .WithColumn("Logger").AsString()
                .WithColumn("Exception").AsString().Nullable()
                .WithColumn("ExceptionType").AsString().Nullable()
                .WithColumn("Level").AsString();

            Create.Index("IX_Logs_Time").OnTable("Logs").OnColumn("Time").Ascending();

            Create.TableForModel("UpdateHistory")
                .WithColumn("Date").AsDateTimeOffset()
                .WithColumn("Version").AsString()
                .WithColumn("EventType").AsInt32();

            Create.Index("IX_UpdateHistory_Date").OnTable("UpdateHistory").OnColumn("Date").Ascending();
        }
    }
}
