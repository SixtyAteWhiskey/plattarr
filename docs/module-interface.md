# Plattarr module interface specification

Status: Draft for owner review, 2026-10-20.
Applies to: Phase 1 onward. Supersedes the draft list in
`claude/arr-combo-plan.md` section 3.

This document defines the seams between the Plattarr shared core and the
media modules (Movies, TV, Music). It is derived from how Radarr's movie code
plugs into its core today, checked against the same seams in Sonarr and
Lidarr.

## Sources

| Repo | Branch | Commit | Date |
|---|---|---|---|
| Plattarr (Radarr fork) | `develop` | `cdbf2ad` | 2026-10-20 |
| Sonarr | `develop` | `7ccafd3` | 2026-10-18 |
| Lidarr | `develop` | `aec8802` | 2026-10-16 |
| Prowlarr | `develop` | `26a605e` | 2026-10-18 |

All paths below are relative to `src/NzbDrone.Core/` unless stated.

## Summary

- The three apps grab, track, and import releases through the same pipeline.
  The only structural difference is the entity a release attaches to:
  one movie, several episodes of one series, or several albums of one artist.
  The spec models that as a **parent** plus **items**.
- The core talks to modules through seven contracts: module registration,
  release matching, search translation, decision specifications, download
  import, activity rendering, and notification payloads. Metadata providers,
  monitoring rules, naming, and quality profiles stay inside the module. The
  core never calls them.
- Quality and language IDs collide between apps. Quality and language
  parsing stay in the modules. This revises the plan, which listed them as
  shared-core candidates.
- 52 of Sonarr's 62 `/api/v3` routes collide with Radarr's. The Radarr and
  Sonarr compatibility APIs cannot share `/api/v3` on one port. Each gets its
  own URL base: `/radarr` and `/sonarr`.
- Phase 1 enforces the boundary with a namespace rule and an architecture
  test, not a separate assembly. 177 core files reference movie types today.
  The test starts with that list as an allowlist that may only shrink. The
  Movies module moves into its own assembly when the list is empty, before
  Phase 3.

## Evidence

### The pipeline is the same in all three apps

Each app has the same release wrapper, with a different entity attached:

| Field | `RemoteMovie` (Radarr) | `RemoteEpisode` (Sonarr) | `RemoteAlbum` (Lidarr) |
|---|---|---|---|
| `Release` (`ReleaseInfo`) | yes | yes | yes |
| `DownloadAllowed` | yes | yes | yes |
| `SeedConfiguration` | yes | yes | yes |
| `CustomFormats`, `CustomFormatScore` | yes | yes | yes |
| `ReleaseSource` | yes | yes | yes |
| `Languages` | yes | yes | no |
| Parsed info | `ParsedMovieInfo` | `ParsedEpisodeInfo` | `ParsedAlbumInfo` |
| Parent entity | `Movie` | `Series` | `Artist` |
| Child entities | none | `List<Episode>` | `List<Album>` |
| Recency (drives client priority) | `Movie.MovieMetadata.IsRecentMovie` | `IsRecentEpisode()` | `IsRecentAlbum()` |

The persisted activity records follow the same parent-plus-items shape:

| Record | Radarr | Sonarr | Lidarr |
|---|---|---|---|
| `Blocklist` | `MovieId` | `SeriesId`, `EpisodeIds` | `ArtistId`, `AlbumIds` |
| `PendingRelease` | `MovieId` | `SeriesId` | `ArtistId` |
| `DownloadHistory` | `MovieId` | `SeriesId` | `ArtistId` |
| History | `MovieId` | `SeriesId`, `EpisodeId` | `ArtistId`, `AlbumId`, `TrackId` |
| `Queue` | `Movie` | `Series`, `Episodes` | `Artist`, `Album` |

History is one row per item in Sonarr and Lidarr (one per episode, one per
track or album). Blocklist and pending releases are one row per release.

### Download clients use four movie-specific values

`IDownloadClient.Download` takes the module's release type in each app:
`Download(RemoteMovie, IIndexer)`, `Download(RemoteEpisode, IIndexer)`,
`Download(RemoteAlbum, IIndexer)`. Across Radarr's 18 clients, the client
code reads only these from the release:

- `Release` (URL, title, indexer).
- `SeedConfiguration`.
- The recency flag, to choose `RecentMoviePriority` or `OlderMoviePriority`.

And one value from settings: the category. Each app names it for its media:

| Setting | Radarr | Sonarr | Lidarr |
|---|---|---|---|
| Category | `MovieCategory` | `TvCategory` | `MusicCategory` |
| Post-import category | `MovieImportedCategory` | `TvImportedCategory` | `MusicImportedCategory` |
| Priorities | `RecentMoviePriority`, `OlderMoviePriority` | `RecentTvPriority`, `OlderTvPriority` | `RecentMusicPriority`, `OlderMusicPriority` |

`GetItems()` filters the client's queue by that category. The category is how
each app recognizes its own downloads.

### Decision specifications split cleanly

Of Radarr's 23 download decision specifications, 8 read only
`subject.Release`: `BlockedIndexer`, `MaximumSize`, `MinimumAge`,
`NotSample`, `RawDisk`, `RequiredIndexerFlags`, `Retention`, and
`TorrentSeeding`. Three more read the movie only for its tags or path:
`Protocol` (delay profile by tag), `ReleaseRestrictions` (tags), and
`FreeSpace` (path). The rest depend on the movie, its quality profile, or
its files.

Sonarr's specification set is a superset with TV-only rules (`FullSeason`,
`MultiSeason`, `SeasonPackOnly`, `SceneMapping`, `AnimeVersionUpgrade`,
`SplitEpisode`, `AirDate`). Lidarr adds `Discography`, `EarlyRelease`, and
`YearMatch`, and still uses the older signature
`Decision IsSatisfiedBy(RemoteAlbum, SearchCriteriaBase)`. Radarr and Sonarr
use `DownloadSpecDecision IsSatisfiedBy(subject, ReleaseDecisionInformation)`.
Lidarr's specifications need converting to the newer signature during the
Phase 4 port.

### Quality and language IDs collide

Quality IDs are persisted in profiles, history, and file records.

- Radarr and Sonarr define 21 of the same quality IDs. Three of them mean
  different things: ID 20 is Bluray-480p in Radarr and Bluray-1080p Remux in
  Sonarr; 21 is Bluray-576p and Bluray-2160p Remux; 22 is BR-DISK and
  Bluray-576p.
- Lidarr's 38 qualities are audio formats. 30 of its IDs reuse numbers that
  are video qualities in Radarr (for example, ID 1 is MP3-192 in Lidarr and
  SDTV in Radarr). Lidarr's `QualityParser.cs` shares almost no lines with
  Radarr's.
- Language IDs diverge from ID 26 upward. ID 26 is Hindi in Radarr and
  Arabic in Sonarr. Sonarr's `LanguageParser.cs` differs from Radarr's in
  506 of 596 lines.
- `ReleaseGroupParser.cs` differs by 20 of 87 lines between Radarr and
  Sonarr and carries no IDs.

A shared quality or language parser would need one global ID catalog and a
remap of every imported row. That is not worth doing in v1.

### Indexer settings carry consumer-side fields

Radarr's, Sonarr's, and Lidarr's Newznab and Torznab settings carry fields
that Prowlarr's indexer settings do not, because in Prowlarr they belong to
the app that consumes the indexer (they are pushed through `Applications`):

| Field | Radarr | Sonarr | Lidarr | Prowlarr indexer |
|---|---|---|---|---|
| `Categories` | yes | yes | yes | no (has `Capabilities`) |
| `AnimeCategories`, `AnimeStandardFormatSearch` | no | yes | no | no |
| `MultiLanguages` | yes | yes | no | no |
| `RequiredFlags` | yes | no | no | no |
| `RemoveYear` | yes | no | no | no |
| `MinimumSeeders` | yes | yes | yes | no (`AppMinimumSeeders` is a sync value) |
| `SeedRatio`, `SeedTime` | yes | yes | yes | yes |
| `SeasonPackSeed*` | no | yes | no | `PackSeedTime` |
| `DiscographySeedTime`, `EarlyReleaseLimit` | no | no | yes | no |
| `FailDownloads`, `RejectBlocklistedTorrentHashesWhileGrabbing` | yes | yes | partly | no |
| `EnableRss`, `EnableAutomaticSearch`, `EnableInteractiveSearch` | definition | definition | definition | no (`Enable` plus `AppProfile`) |

Deleting Prowlarr's `Applications` subsystem removes the place these settings
lived. They have to come back as per-module indexer settings (see
[Indexer settings per module](#indexer-settings-per-module)).

### Search criteria names collide

Prowlarr's `IIndexer` already has one `Fetch` overload per media type:
`MovieSearchCriteria`, `TvSearchCriteria`, `MusicSearchCriteria`,
`BookSearchCriteria`, `BasicSearchCriteria`, all in
`NzbDrone.Core.IndexerSearch.Definitions`. Radarr defines its own
`MovieSearchCriteria` in the same namespace with a different shape (it holds
the `Movie` entity and scene titles). Sonarr defines seven criteria types
(single, season, daily, daily season, anime, anime season, special) that
Prowlarr's single `TvSearchCriteria` (`Season`, `Episode` as a string) must
represent.

### The DI container registers by convention

`NzbDrone.Common/Composition/Extensions.cs` `AutoAddServices` registers every
interface implementation in the listed assemblies (`Bootstrap.ASSEMBLIES`) as
a singleton. Several implementations of one interface resolve as
`IEnumerable<T>`. Modules can register by implementing core interfaces; no
explicit registration code is needed. The consequence is that two modules
cannot each define a type with the same full name, so module types must live
in module namespaces.

`Jobs/TaskManager.cs` hardcodes all 12 scheduled tasks, including
`RefreshMovieCommand`, `RefreshCollectionsCommand`, and `ImportListSyncCommand`.
`Datastore/TableMapping.cs` maps all 42 tables in one method.
`Datastore/Migration/Framework/MigrationController.cs` scans only
`Assembly.GetExecutingAssembly()`.

### The compatibility APIs collide

Radarr's `Radarr.Api.V3` has 64 controller routes and Sonarr's
`Sonarr.Api.V3` has 62. 52 routes have the same path, including
`system/status`, `qualityprofile`, `rootfolder`, `tag`, `queue`, `history`,
`command`, and `config/*`. Both cannot serve `/api/v3` on one port.

### Current coupling

177 files outside the module-owned folders reference movie types
(`Movie`, `RemoteMovie`, `LocalMovie`, `MovieFile`, `ParsedMovieInfo`,
`MovieId`, `MovieMetadata`, `IMovieService`):

| Folder | Files | Note |
|---|---|---|
| `Notifications` | 58 | Every provider formats movie messages |
| `Download` | 47 | Clients, tracked downloads, pending, completed and failed handling |
| `Datastore` | 24 | Mostly upstream migrations; removed by `001_initial` |
| `Housekeeping` | 16 | Orphan cleanup per table |
| `Indexers` | 8 | Radarr request generators; replaced by Prowlarr's layer |
| `IndexerSearch` | 4 | |
| `History`, `Blocklisting`, `HealthCheck`, `MovieStats`, `Validation` | 3 each | |
| `Queue` | 2 | |
| `Tags`, `Exceptions`, `DiskSpace` | 1 each | |

Module-owned folders, excluded from the count: `Movies`, `MediaFiles`,
`DecisionEngine`, `ImportLists`, `Extras`, `Organizer`, `CustomFormats`,
`Qualities`, `MetadataSource`, `Profiles`, `AutoTagging`, `RootFolders`,
`Languages`, `MediaCover`, `Parser`.

## Design

### Terms

| Term | Meaning |
|---|---|
| Module | One media type: Movies, TV, or Music. Identified by `MediaKind`. |
| Parent | The top-level library entity: `Movie`, `Series`, or `Artist`. |
| Item | The unit that gets a file: `Movie`, `Episode`, or `Album` (Lidarr tracks are files within an album). For Movies, parent and item are the same entity. |
| Release | One indexer result, wrapped in a module's `RemoteRelease` subclass once matched. |

### Boundary rules

1. Module code lives in `NzbDrone.Core.<Module>.*`: `NzbDrone.Core.Movies`,
   `NzbDrone.Core.Tv`, `NzbDrone.Core.Music`. Sonarr and Lidarr already use
   `Tv` and `Music` as their entity namespaces.
2. Core code never references a module type. It works with the abstract
   types in `NzbDrone.Core.Modules`.
3. A module may reference the core. A module never references another module.
4. Module-owned tables use the module prefix (`Movies_`, `Tv_`, `Music_`),
   as approved in the plan.
5. Rule 2 is enforced by an architecture test in `NzbDrone.Core.Test` that
   fails when a type outside `NzbDrone.Core.<Module>` references a type
   inside it. The test carries an allowlist of files, seeded with the 177
   found today. A change may remove entries; adding one fails review.

### Shared types

```csharp
namespace NzbDrone.Core.Modules
{
    // Stored in shared tables. Values never change once released.
    public enum MediaKind
    {
        Movies = 1,
        Tv = 2,
        Music = 3
    }

    // What the core needs to know about a parent entity.
    public interface IMediaParent
    {
        MediaKind Kind { get; }
        int Id { get; }
        string Title { get; }
        string Path { get; }
        HashSet<int> Tags { get; }
        int QualityProfileId { get; }
        bool Monitored { get; }
    }

    // Base for RemoteMovie, RemoteEpisode, RemoteAlbum.
    public abstract class RemoteRelease
    {
        public ReleaseInfo Release { get; set; }
        public bool DownloadAllowed { get; set; }
        public TorrentSeedConfiguration SeedConfiguration { get; set; }
        public List<CustomFormat> CustomFormats { get; set; }
        public int CustomFormatScore { get; set; }
        public List<Language> Languages { get; set; }
        public ReleaseSourceType ReleaseSource { get; set; }

        public abstract MediaKind Kind { get; }
        public abstract IMediaParent Parent { get; }
        public abstract IReadOnlyList<int> ItemIds { get; }
        public abstract bool IsRecent { get; }
    }
}
```

`ReleaseInfo` is Prowlarr's model plus `PendingReleaseReason`, as already
decided (plan section 3a, finding 5).

`CustomFormat` and `Language` appear on the base class because every
specification and the queue read them. Their IDs are module-scoped (see
[Quality and language](#quality-and-language)). The core treats them as
opaque values and never compares IDs across modules.

### Contract 1: Module registration

```csharp
public interface IMediaModule
{
    MediaKind Kind { get; }
    string Name { get; }               // "Movies"
    string TablePrefix { get; }        // "Movies_"
    string CompatUrlBase { get; }      // "/radarr", "/sonarr", or null
    IReadOnlyList<int> DefaultCategories { get; }  // Newznab: 2000s, 5000s, 3000s

    void MapTables(TableMapper mapper);
    IEnumerable<ScheduledTaskDefinition> ScheduledTasks { get; }
}
```

- `TaskManager` builds its task list from core tasks plus every module's
  `ScheduledTasks`. Movies contributes `RefreshMovieCommand`,
  `RefreshCollectionsCommand`, and `ImportListSyncCommand`.
- `TableMapping` calls `MapTables` on each module after mapping core tables.
- `PLATTARR__MODULES__ENABLED` filters which modules load. A disabled
  module's tables stay in the database untouched.

### Contract 2: Release matching

The core fetches RSS once per indexer and hands each release to the modules.

```csharp
public interface IReleaseMatcher
{
    MediaKind Kind { get; }

    // Parse and map a release to a library entity. Null when it doesn't match.
    RemoteRelease Map(ReleaseInfo release);

    // Rebuild a release for a tracked download from its grab history.
    RemoteRelease MapFromHistory(string sourceTitle, int parentId, IReadOnlyList<int> itemIds);

    // Map by title alone, for downloads with no history.
    RemoteRelease MapFromTitle(string title);
}
```

Routing for RSS results:

1. If the release has categories, offer it to each module whose configured
   categories for that indexer overlap. Usually one module.
2. If the release has no categories, offer it to every enabled module.
3. If more than one module returns a match, log a warning and keep both.
   The decision engine evaluates each independently. A grab by one module
   does not block the other.

Title parsing, scene mapping, and entity lookup stay inside `Map`. The core
does not parse titles.

### Contract 3: Search translation

Prowlarr's criteria types (`MovieSearchCriteria`, `TvSearchCriteria`,
`MusicSearchCriteria`) stay in `NzbDrone.Core.IndexerSearch.Definitions` and
belong to the core. Module search requests are renamed and move into the
module: Radarr's `MovieSearchCriteria` becomes
`NzbDrone.Core.Movies.Search.MovieSearchRequest`, Sonarr's seven criteria
become `Tv.Search.*Request`, and so on.

```csharp
public interface ISearchTranslator<TRequest>
    where TRequest : ModuleSearchRequest
{
    // One request can fan out (scene titles, alternate IDs, anime absolute numbers).
    IEnumerable<SearchCriteriaBase> Translate(TRequest request, IndexerDefinition indexer);
}
```

The core runs the translated criteria through Prowlarr's indexer layer,
applies rate limits and stats, then passes results back through the module's
`IReleaseMatcher.Map`. This is the in-process adapter in Phase 1 step 4.

Open item for Phase 3: Sonarr's daily and anime searches must fit
`TvSearchCriteria.Episode` (a string) without losing Sonarr's request
formats. Confirm against Prowlarr's Newznab generator before porting.

### Contract 4: Decision specifications

```csharp
// Core. Runs for every module.
public interface IReleaseSpecification
{
    SpecificationPriority Priority { get; }
    RejectionType Type { get; }
    DownloadSpecDecision IsSatisfiedBy(RemoteRelease subject, ReleaseDecisionInformation information);
}

// Module. Runs only for its own release type.
public interface IReleaseSpecification<in TRemote>
    where TRemote : RemoteRelease
{
    SpecificationPriority Priority { get; }
    RejectionType Type { get; }
    DownloadSpecDecision IsSatisfiedBy(TRemote subject, ReleaseDecisionInformation information);
}
```

The core `DownloadDecisionMaker` runs both sets, ordered by `Priority`, and
returns `DownloadDecision` with a `RemoteRelease`. `ReleaseDecisionInformation`
replaces the old search-criteria argument and carries a module request as
`ModuleSearchRequest` (null for RSS).

Core specifications (moved out of `DecisionEngine/Specifications`):
`BlockedIndexer`, `MaximumSize`, `MinimumAge`, `NotSample`, `RawDisk`,
`RequiredIndexerFlags`, `Retention`, `TorrentSeeding`, `Protocol`,
`ReleaseRestrictions`, `FreeSpace`. The last three use `IMediaParent.Tags`
and `IMediaParent.Path`.

Everything else stays in the module, including `Blocklist`, `Queue`,
`AlreadyImported`, upgrades, quality and custom format checks, delay, and
history.

Download prioritization (`DownloadDecisionComparer`) stays per module. The
comparison rules differ (Sonarr compares episode counts and season packs).

### Contract 5: Download import

```csharp
public interface IDownloadImporter
{
    MediaKind Kind { get; }

    // Import from the download's output path. Returns one result per file considered.
    List<ImportResult> Import(TrackedDownload trackedDownload, string outputPath, ImportMode mode);

    // True when every expected item has been imported, judged from history.
    bool IsImported(TrackedDownload trackedDownload, IReadOnlyList<HistoryItem> history);
}
```

The core keeps `TrackedDownloadService`, `CompletedDownloadService`,
`FailedDownloadService`, `IgnoredDownloadService`, `RejectedImportService`,
`ProvideImportItemService`, remote path mappings, and the hardlink-or-copy
transfer. The module owns file matching, renaming, extras, and the media file
tables.

`ImportResult` keeps its current shape (`ImportResultType`, errors) and
replaces the movie reference with `ItemIds`.

#### Tracked download routing

A download in the client must be attributed to one module before import:

1. `DownloadHistory` lookup by `DownloadId`. The row records `MediaKind`.
   This covers everything Plattarr grabbed.
2. The download client category. Each category maps to one module (see
   [Download client settings](#download-client-settings)).
3. Neither available (category empty or `*`, no history): ask each enabled
   module's `MapFromTitle`. Exactly one match wins. Zero or several matches
   leave the download in the queue with a `ManualInteractionRequired` warning.

### Contract 6: Activity

History, blocklist, pending releases, and download history are shared tables
so the activity pages can query one place:

| Table | New columns | Replaces |
|---|---|---|
| `History` | `MediaKind`, `ParentId`, `ItemId` | `MovieId` |
| `Blocklist` | `MediaKind`, `ParentId`, `ItemIds` (JSON) | `MovieId` |
| `PendingReleases` | `MediaKind`, `ParentId`, `ItemIds` (JSON) | `MovieId` |
| `DownloadHistory` | `MediaKind`, `ParentId` | `MovieId` |

Module-specific values go in the existing `Data` JSON column, as the apps do
today. `Queue` is built in memory from tracked downloads and needs no table.

```csharp
public interface IActivityRenderer
{
    MediaKind Kind { get; }

    // Display values for activity, queue, and calendar rows.
    ActivitySubject Describe(int parentId, IReadOnlyList<int> itemIds);
}

public class ActivitySubject
{
    public string ParentTitle { get; set; }   // "The Expanse"
    public string ItemLabel { get; set; }     // "S02E03", "(2019)", "Album Name"
    public string ParentPath { get; set; }
}
```

### Contract 7: Notifications

Phase 1 has only the Movies module, so notifications keep Radarr's
movie-typed messages and stay on the allowlist. Before Phase 3, messages
become module-neutral:

```csharp
public class GrabMessage
{
    public MediaKind Kind { get; set; }
    public ActivitySubject Subject { get; set; }
    public RemoteRelease Release { get; set; }
    public string QualityName { get; set; }
    public string DownloadClientName { get; set; }
    public string DownloadId { get; set; }
}
```

Providers that only send text (Discord, Telegram, and most others) format
from `Subject`. Providers that send structured payloads (Webhook, Custom
Script) switch on `Kind` and emit that module's upstream payload shape, so
existing receivers keep working. The Jellyfin library refresh only needs
`Subject.ParentPath`.

Per-module events (`OnMovieAdded`, `OnSeriesAdd`, `OnArtistAdd`) become one
`OnMediaAdded` and similar, each with a `MediaKind`. Notification settings
gain a per-module toggle so a user can, for example, notify on movie grabs
only.

### Download client settings

`IDownloadClient.Download` takes `RemoteRelease`. Client settings replace the
movie-named fields with a per-module list:

```csharp
public class ModuleCategorySettings
{
    public MediaKind Kind { get; set; }
    public string Category { get; set; }
    public string ImportedCategory { get; set; }
    public int RecentPriority { get; set; }
    public int OlderPriority { get; set; }
}
```

Validation: within one client, each module's category must be unique and
non-empty when more than one module is enabled. Routing step 2 depends on
it.

The migration importer maps Radarr's `MovieCategory` to the Movies entry.
When `sonarr.db` is imported in Phase 3 and a client with the same host and
port exists, the importer adds a TV entry to that row instead of creating a
second client.

Owner's setup: qBittorrent behind gluetun with one save path
(`/mnt/share/Media/Importing`). Separate categories per module are required
before TV is enabled, or routing falls to title parsing.

### Indexer settings per module

New core table `IndexerModuleSettings`, one row per indexer and module:

| Column | Notes |
|---|---|
| `IndexerId`, `MediaKind` | Key |
| `EnableRss`, `EnableAutomaticSearch`, `EnableInteractiveSearch` | From Radarr's `IndexerDefinition` |
| `Categories` | Defaults to `IMediaModule.DefaultCategories` filtered by the indexer's capabilities |
| `MinimumSeeders`, `SeedRatio`, `SeedTime` | Override Prowlarr's indexer values when set |
| `Settings` (JSON) | Module-only fields: `MultiLanguages`, `RequiredFlags`, `RemoveYear` (Movies); `AnimeCategories`, `SeasonPack*` (TV); `DiscographySeedTime`, `EarlyReleaseLimit` (Music) |

`FailDownloads` and `RejectBlocklistedTorrentHashesWhileGrabbing` apply to
the indexer as a whole and move to the shared indexer definition.

This replaces what Prowlarr's `Applications` and `AppSyncProfile` did.

### Configuration

The `Config` table is shared. Keys that are global keep their names. Keys
that each app has its own copy of get a module prefix (`Movies.RecycleBin`).

Of the 50 Radarr and 49 Sonarr `ConfigService` keys, 38 have the same name.
Proposed split for those 38:

- **Core:** `CalendarWeekColumnHeader`, `DownloadClientHistoryLimit`,
  `DownloadClientWorkingFolders`, `EnableColorImpairedMode`,
  `EnableCompletedDownloadHandling`, `FirstDayOfWeek`, `LongDateFormat`,
  `MaximumSize`, `MinimumAge`, `Retention`, `RssSyncInterval`,
  `ShortDateFormat`, `ShowRelativeDates`, `TimeFormat`,
  `TrustCgnatIpAddresses`, `UILanguage`.
- **Per module:** `AutoRedownloadFailed`,
  `AutoRedownloadFailedFromInteractiveSearch`, `ChmodFolder`, `ChownGroup`,
  `CleanupMetadataImages`, `CopyUsingHardlinks`, `DeleteEmptyFolders`,
  `DownloadPropersAndRepacks`, `EnableMediaInfo`, `ExtraFileExtensions`,
  `FileDate`, `ImportExtraFiles`, `ListSyncLevel`,
  `MinimumFreeSpaceWhenImporting`, `RecycleBin`, `RecycleBinCleanupDays`,
  `RescanAfterRefresh`, `ScriptImportPath`, `SetPermissionsLinux`,
  `SkipFreeSpaceCheckWhenImporting`, `UseScriptImport`.

`ConfigKey` is a helper, not a setting.

### Quality and language

- Each module owns its quality definitions, quality profiles, languages,
  custom formats, quality parser, and language parser, with upstream IDs
  unchanged. Imported rows keep their IDs; no remapping.
- Shared tables never store a quality or language ID without `MediaKind`
  next to it. `History.Quality` and `Blocklist.Quality` are JSON; readers
  resolve them through the owning module.
- Release group parsing moves to the core.

### Other module-owned tables

Module-owned and prefixed: root folders, delay profiles, release profiles,
quality definitions, quality profiles, custom formats, naming config, import
lists and exclusions, metadata consumers, auto-tagging, and all media and
file tables.

Shared: tags, remote path mappings, indexers and their status, download
clients and their status, notifications and their status, users, config,
commands, logs, scheduled tasks, custom filters (with a `MediaKind` column
added).

### Database migrations

Each module has its own FluentMigrator chain and version table:
`VersionInfo` for the core, `Movies_VersionInfo`, `Tv_VersionInfo`,
`Music_VersionInfo`. The core chain runs first. Module chains may reference
core tables, never another module's. Each chain starts at `001_initial`.

Reason: one numbered chain shared by four authors' worth of history forces
every new migration in one module to take a number after all others. Separate
chains avoid ordering conflicts and let a disabled module skip its
migrations.

Verify in Phase 1 step 5 that `MigrationController` can run several runners
with custom `IVersionTableMetaData` against one SQLite file and one Postgres
database. If it can't, fall back to one chain with reserved version ranges.

### Compatibility APIs

- The Movies module serves Radarr v3 at `/radarr/api/v3`, the TV module serves
  Sonarr v3 at `/sonarr/api/v3`, on port 7880.
- Jellyseerr is configured with URL base `/radarr` or `/sonarr`.
- The existing Radarr UI keeps calling `/api/v3` until the unified frontend
  replaces it. During Phase 1, `/api/v3` and `/radarr/api/v3` serve the same
  controllers.
- Shared resources reached through a shim (`tag`, `system/status`,
  `downloadclient`) answer from the core, shaped as that app would.
- Each shim exposes only the routes Jellyseerr is recorded calling in
  Phase 2. Unknown routes return 404.

### What stays inside a module

The core does not call these, so they are not part of the interface. Each
module keeps its upstream code:

- Metadata provider (TMDB, TheTVDB via Sonarr's proxy, MusicBrainz via
  Lidarr's proxy) and refresh.
- Entity model, monitoring rules, and wanted (missing and cutoff unmet).
- Naming and folder templates.
- Import lists, extras, and media covers.
- The module's own API controllers.

## Phase 1 work this implies

In porting order (plan section 3a):

| Step | Spec work |
|---|---|
| 2. Port Prowlarr indexers | Keep Prowlarr's `IndexerSearch.Definitions` names. Rename Radarr's `MovieSearchCriteria` to `Movies.Search.MovieSearchRequest` first so the port doesn't collide. Add `IndexerModuleSettings`. |
| 3. Swap `ReleaseInfo` | Add `RemoteRelease`, `IMediaParent`, `MediaKind`. Make `RemoteMovie` derive from `RemoteRelease`. |
| 4. Indexer adapter | `IReleaseMatcher` and `ISearchTranslator` for Movies. RSS routing by category. |
| 5. Collapse migrations | Core and `Movies_` chains, separate version tables. Activity tables with `MediaKind`. |
| 6. Module boundary | Move movie code to `NzbDrone.Core.Movies.*`. Add the architecture test and allowlist. Split core specifications, `IDownloadImporter`, `IMediaModule`, client category settings. |

Exit check for the boundary: the allowlist has only `Notifications` entries
left. Notifications move off it before Phase 3.

## Decisions needed

These go beyond what the plan approved. Each needs the owner's sign-off
before Phase 1 work depends on it.

1. **Activity tables are shared**, not module-prefixed: `History`,
   `Blocklist`, `PendingReleases`, `DownloadHistory`, with `MediaKind`.
   Alternative: prefixed per module, joined with `UNION` in the activity API.
2. **Quality and language parsing stay per module.** Revises plan section 3
   and the Architecture row of the handoff.
3. **Compatibility APIs move to `/radarr` and `/sonarr` URL bases.** No
   longer optional (52 route collisions). Jellyseerr's URL base field still
   needs confirming in Phase 2.
4. **Per-module indexer settings table** replaces Prowlarr's app sync.
5. **One migration chain and version table per module.** Revises the
   approved "fresh Plattarr migration chain starting at `001_initial`"
   from one chain to four.
6. **Boundary enforced by test and allowlist in Phase 1**, separate module
   assembly before Phase 3.
