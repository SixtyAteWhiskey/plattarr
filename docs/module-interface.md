# Plattarr module interface specification

Status: Approved by the owner 2026-10-20, revision 2. See
[Decision record](#decision-record).
Applies to: Phase 1 onward.

How to read this: each contract has its original design followed by "As built"
notes. Where they differ, the as-built note is correct. Since 2026-10-22 the
code behind many of the examples (download clients other than qBittorrent and
SABnzbd, most notification providers, most import lists) is pruned; the design
text still holds for what remains. See `docs/pruned-providers.md`. The
compatibility APIs and the owner's Jellyseerr plan below are deferred until a
user asks for them; they are not part of the near-term plan.

This document defines the seams between the Plattarr shared core and the
media modules (Movies, TV, Music). It is derived from how Radarr's movie code
plugs into its core today, checked against the same seams in Sonarr and
Lidarr.

"Module" is an internal code term. Users never see it. See
[Single pane of glass](#single-pane-of-glass).

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
- Qualities, languages, quality profiles, and custom formats are global.
  Upstream quality and language IDs collide between apps, so Plattarr has its
  own ID catalog and the importers remap. Each item can apply to all media
  types (the default) or be scoped to some.
- Indexers have one global setting each. Search categories are derived from
  the media type being searched; the user doesn't configure them.
- 52 of Sonarr's 62 `/api/v3` routes collide with Radarr's. The compatibility
  APIs (deferred, built on request) get their own URL bases: `/radarr`,
  `/sonarr`, `/lidarr`. They are full-fidelity copies of the upstream APIs, not a
  captured subset, so third-party tools work unchanged.
- One schema, one migration chain, one version table.
- The module boundary is a namespace rule enforced by an architecture test.
  The test started with 159 allowlisted core types; the list may only shrink and
  is frozen at 32 until TV code needs the rest. No separate module assemblies.

## Single pane of glass

Owner's goal: the user picks what they want to download, and Plattarr does
the rest. The user never chooses or configures a "module".

Rules for every user-facing surface:

- **One library, one search.** The search bar takes a title and an optional
  media type filter: All (default), Movies, TV, Anime, Music. Results from
  every media type appear together, labeled by type. Adding an item asks only
  for what that item needs (monitoring, profile, folder) with global defaults
  pre-filled.
- **Anime is a filter, not a module.** Anime series are TV series with
  Sonarr's `Anime` series type, which changes episode numbering and search.
  Anime movies are movies. The Anime filter shows both. The series type is
  set automatically from metadata and can be changed per series.
- **Shared pages are shared.** Activity, queue, history, blocklist,
  calendar, wanted, indexers, download clients, notifications, and system
  are one page each, with a media type filter.
- **Settings are global first.** Quality profiles, custom formats, delay and
  release profiles, and media management settings apply to all media types
  unless the user scopes them. Per-type settings appear only where values
  can't be shared: root folders and naming formats.
- **Defaults remove decisions.** Download client categories, indexer
  categories, and the media type of a release are set automatically.
  Manual control sits behind "Advanced".
- **"Module" stays in the code.** UI text says "media type", or names the
  type.

Phase 1 ships with Radarr's UI, which already fits these rules for one
media type. The unified frontend (plan section 3, layer 4) is built against
these rules once TV exists, in Phase 3.

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

Shared qualities and languages therefore need one Plattarr ID catalog and a
remap of every imported row. The catalogs are small: by name, Radarr and
Sonarr together define 33 video qualities, two of which are aliases
(Sonarr's "Bluray-1080p Remux" and "Bluray-2160p Remux" are Radarr's
"Remux-1080p" and "Remux-2160p"), so 31 distinct video qualities plus
Lidarr's 38 audio qualities. Radarr's 60 and Sonarr's 55 languages make 64
distinct names.

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

As built in porting step 9a (`ModuleBoundaryFixture` in `NzbDrone.Core.Test`):

- The test reads compiled IL, not source: signatures, attributes, locals, and
  the type, method, and field tokens in method bodies. Lambdas and async state
  machines count toward their declaring type.
- The allowlist holds type names, not files: 159 core types at 9a, 156 after
  9b. It must match the violations exactly, so a new reference fails and so
  does an entry that no longer violates.
- Until step 11 the boundary was declared in its final shape (`ModuleBoundary`):
  the Radarr namespaces that are movie code (`MediaFiles`, `DecisionEngine`,
  `History`, `Blocklisting`, `ImportLists`, `Extras`, `Organizer`, and others)
  and Radarr's title parser types counted as Movies before they moved under
  `NzbDrone.Core.Movies`. Moving them changed nothing the test reports (step 11).
- Rule 3 (no module references another) is checked with no allowlist.

As built in porting step 11 (Radarr code moved under `NzbDrone.Core.Movies`):

- The lists of "movie code not yet moved" are gone: `ModuleBoundary` is the
  namespace rule only. The allowlist stayed at 32 and the test report did not
  change, which is the check that the move was boundary-neutral.
- Thirteen namespaces moved whole, `NzbDrone.Core.<X>` to
  `NzbDrone.Core.Movies.<X>` with their folders: `AutoTagging`, `Blocklisting`,
  `DecisionEngine.Specifications`, `Extras`, `History`, `ImportLists`,
  `MediaCover`, `MediaFiles`, `MetadataSource`, `MovieStats`, `Organizer`,
  `Parser.RomanNumerals`, `RootFolders` (322 files with the type-level moves). Movie types in the shared
  `Parser`, `Parser.Model` and `DecisionEngine` namespaces moved to
  `Movies.Parser`, `Movies.Parser.Model` and `Movies.DecisionEngine`
  (`Parser`, `QualityParser`, `LanguageParser`, `SceneChecker`, `ParsingService`,
  `ReleaseComparer`, `ParsedMovieInfo`, `LocalMovie`, `RemoteMovie`,
  `IndexerFlags` and its mapper, and the download decision types).
- `Resolution` and `ReleaseSourceType` shared a file with a moved type but are
  used by core code, so each got its own file and stays in `NzbDrone.Core.Parser`
  and `NzbDrone.Core.Parser.Model`.
- Types that look shared (identical to Sonarr, or no movie reference) moved
  too. Many are subclasses over a base that differs between the apps, so
  identical text proves nothing. A type is promoted back to core when TV code
  needs it, designed against both modules.
- Name resolution traps. Members of an enclosing namespace win over `using`
  imports, so outside `NzbDrone.Core.Movies` the class `Parser` must be written
  `Movies.Parser.Parser` (a bare `Parser.Parser` binds to the core namespace and
  fails to compile); inside the module `Parser.Parser` still works. The same
  holds for `Movies.MediaCover.MediaCover`. Extension methods that moved
  (`CleanMovieTitle`, `SimplifyReleaseTitle`, `ToIndexerFlags`,
  `ToIndexerFlagSet`, `AddIndexerFlags`) need `using NzbDrone.Core.Movies.Parser`
  or `.Parser.Model` at the call site.
- Persisted full type names change in one place: `ScheduledTasks.TypeName` for
  `CleanUpRecycleBinCommand` and `ImportListSyncCommand`. `TaskManager` deletes
  rows it does not know and recreates them, so each of those two tasks restarts
  its interval once after the upgrade. Provider settings are stored by class
  name and the auto-tagging converter builds its type name from the new
  namespace; neither changed meaning.

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
specification and the queue read them. Both are global types with Plattarr
IDs (see [Qualities, languages, profiles, and custom formats](#qualities-languages-profiles-and-custom-formats)).

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
- `PLATTARR__MODULES__ENABLED` hides media types the user doesn't want (for
  example, no music). A hidden media type has no UI, runs no scheduled tasks,
  and receives no releases. Its tables still exist, empty, because the schema
  is one migration chain.

As built in porting step 9b, the contract is two interfaces, because
`TableMapping.Map()` runs in `DbFactory`'s static constructor, before the
container exists:

```csharp
public interface IMediaModule            // registered by the container
{
    MediaKind Kind { get; }
    string Name { get; }                 // "Movies"
    IReadOnlyList<int> DefaultCategories { get; }
    IEnumerable<ScheduledTask> ScheduledTasks { get; }
}

public interface IModuleTableMap         // found by reflection, parameterless
{
    MediaKind Kind { get; }
    string TablePrefix { get; }          // "Movies_"
    void RegisterTypeHandlers();         // before any table is mapped
    void MapTables(TableMapper mapper);  // after the core tables
}
```

- `DefaultCategories` moved here from `IReleaseMatcher`. `IIndexerCategoryService`,
  `IIndexerReleaseFetcher`, and `RssSyncService` take a `MediaKind` and look the
  module up.
- A module maps every entity type it owns, including its views of shared
  tables (`MovieHistory` on `History`, `Blocklist`). `PendingRelease` and
  `DownloadHistory` are core types and stay in core mapping.
- `TableMapper.ModuleTablePrefixes` comes from the table maps.
- Not built yet, because nothing reads them: `CompatUrlBase` (Phase 2) and
  `PLATTARR__MODULES__ENABLED` (needs a second module).

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

As built in porting step 7 (`ba4cb87`, `61fd6e7`):

```csharp
public interface IReleaseMatcher
{
    MediaKind Kind { get; }
    IReadOnlyList<int> Categories { get; }   // moved to IMediaModule.DefaultCategories in 9b
    RemoteRelease Map(ReleaseInfo release, ModuleSearchRequest request);
}

public interface IRssReleaseHandler
{
    MediaKind Kind { get; }
    Task Process(IReadOnlyList<ReleaseInfo> releases);
}
```

- `Map` takes the module's request (null for RSS) because Radarr matches
  search results against the movie that was searched. It returns null when
  the title doesn't parse, and a release with a null `Parent` when it parses
  but matches nothing, so the decision engine can report "Unknown Movie".
  `MovieReleaseMatcher` is Radarr's parse and map, moved out of
  `DownloadDecisionMaker` unchanged.
- `MapFromHistory` and `MapFromTitle` wait for contract 5 (tracked download
  routing, step 9); nothing in the core calls them yet.
- `IRssReleaseHandler` receives the releases routed to its media type. The
  core `RssSyncService` fetches once per indexer, routes, and hands each
  handler its share; the module makes the decisions.
- Routing (`IndexerCategoryService`): a release belongs to a media type when
  one of its categories equals a wanted category or falls in a wanted
  top-level category's 1000 range. A release with no categories, or only
  indexer-specific ones (100000 and up) that nobody asked for, goes to every
  media type. The wanted categories are per indexer: the media type's
  defaults, or the indexer's override.
- Rule 3 (two modules both match) needs a second module and is not built.

As built in porting step 9h, download clients take the release, not the movie:

- `IDownloadClient.Download(RemoteRelease, IIndexer)`, and the same type in
  `TorrentClientBase`, `UsenetClientBase` and all 18 clients. A client reads
  `Release`, `SeedConfiguration`, `IsRecent`, and for Flood's tags
  `Quality`, `ReleaseGroup`, `Year`, `Collection` and `Studio` (the last two are
  null for media that has none). Settings held the movie-named category and
  priority values until porting step 9i (below).
- Three helpers the clients used moved to core: the torrent file reader
  (`NzbDrone.Core.Download.Torrents`), file name cleaning (`FileNameCleaner`,
  shared with the movie `FileNameBuilder`), and the blocklisted-hash check
  (`IReleaseBlocklist`, which asks the `IMediaBlocklist` of the release's kind).
- Left in the boundary allowlist: tracked downloads, history reads, decision
  processing and the aggregators (later steps moved most of these).

### Contract 3: Search translation

Prowlarr's criteria types (`MovieSearchCriteria`, `TvSearchCriteria`,
`MusicSearchCriteria`) stay in `NzbDrone.Core.IndexerSearch.Definitions` and
belong to the core. Module search requests are renamed and move into the
module: Radarr's `MovieSearchCriteria` becomes
`NzbDrone.Core.Movies.Search.MovieSearchRequest`, Sonarr's seven criteria
become `Tv.Search.*Request`, and so on.

```csharp
public interface ISearchTranslator<in TRequest>
    where TRequest : ModuleSearchRequest
{
    // Ordered tiers. The next tier runs only when the previous one found nothing on that indexer.
    IEnumerable<IReadOnlyList<SearchCriteriaBase>> Translate(TRequest request, IndexerDefinition indexer, int[] categories);
}
```

Amended in porting step 7 (`ba4cb87`, `61fd6e7`) from a flat
`IEnumerable<SearchCriteriaBase>`:

- **Tiers.** Radarr searches by ID first and by title only when the ID
  search returns nothing from that indexer; Sonarr does the same with scene
  titles. A flat list can't express the fallback.
- **Categories.** The core resolves them per indexer (defaults or
  override) and passes them in, so translators decide query shape only.

The core (`IndexerReleaseFetcher`) picks indexers by the enable flags, media
tags, and category support, runs each tier through Prowlarr's
`ReleaseSearchService` (query limits, `IndexerHistory`, stats), and returns
the releases to the module, which maps them with `IReleaseMatcher.Map`.
`MovieSearchTranslator` sends the IDs the indexer's capabilities support
(TMDb, IMDb) in tier 1 and each scene title with the year (without it when
the override's `RemoveYear` is set) in tier 2.

Indexer tags mean two things after the merge: a media restriction (Radarr,
Sonarr, Lidarr) and a proxy selector (Prowlarr). A tag that any indexer proxy
uses never restricts media (`IndexerMediaTagService`); search and the RSS tag
specification use the same rule.

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

As built in porting step 9d:

- Shared primitives. Radarr and Sonarr (`b3d4bb8`) have byte-identical
  `DownloadSpecDecision`, `RejectionType`, `SpecificationPriority`,
  `Rejection<T>`, `DownloadRejection`, `IRejectWithReason`, and
  `UpgradeableRejectReason`. The first six stay at their upstream full names
  as core: the root namespace `NzbDrone.Core.DecisionEngine` is core, so
  ported specifications compile against it with no edits. The movie types in
  that namespace (`DownloadDecision`, `DownloadDecisionMaker`,
  `DownloadDecisionComparer`, `DownloadDecisionPriorizationService`, their
  interfaces, and `UpgradeableRejectReason`, which only the module's upgrade
  rules use) were listed as module types in `ModuleBoundary` and now live in
  `NzbDrone.Core.Movies.DecisionEngine` (step 11).
  `DecisionEngine.Specifications` stays module code.
- `DownloadRejectionReason` is one core enum. Radarr's and Sonarr's ordinals
  diverge from the second value, but the value never leaves the process as
  an integer (the API sends `Message`; pending releases store their own
  `PendingReleaseReason`) and nothing branches on it. Sonarr's values are
  appended in Phase 3.
- `ReleaseDecisionInformation.SearchCriteria` is a `ModuleSearchRequest`.
  The one specification that reads a movie member (`MovieSpecification`)
  casts to `MovieSearchRequest`.
- Core `IReleaseSpecification` and eleven specifications live in
  `NzbDrone.Core.DecisionEngine.ReleaseSpecifications`: `BlockedIndexer`,
  `FreeSpace`, `MaximumSize`, `MinimumAge`, `NotSample`, `Protocol`,
  `RawDisk`, `ReleaseRestrictions`, `Retention`, `TorrentSeeding`, and (since
  step 12) `RequiredIndexerFlags`. They read
  `Release`, `Kind`, and `Parent` (`Tags`, `Path`) only. `Protocol` names
  the parent with the upstream noun for its kind ("movie", "series",
  "artist").
- `RequiredIndexerFlags` was module code until the indexer flag vocabulary
  was global (plan, finding 6; step 12). It reads the indexer override for the
  release's own `Kind`, so it runs for every media type. Sonarr has no such
  specification.

As built in porting step 12 (global indexer flag vocabulary):

- `IndexerFlags` (Radarr's values: `G_Freeleech` 1 ... `Nuked` 2048) is the
  global vocabulary, never renumbered. The enum and `IndexerFlagsMapper`
  moved back to `NzbDrone.Core.Parser.Model`, so core custom formats read it
  without a module reference (the allowlist went from 32 to 30). Release-level
  flags were already a named set (`IndexerFlag`); the enum is only the stored
  integer.
- Sonarr's eight flags (`Freeleech` 1, `Halfleech` 2, `DoubleUpload` 4,
  `Internal` 8, `Scene` 16, `Freeleech75` 32, `Freeleech25` 64, `Nuked` 128)
  all exist by name in the vocabulary, so no value was added. Its integers
  differ from bit 8 up and are translated by name with
  `SonarrIndexerFlags.ToGlobal` / `ToUpstream` (importer and compatibility
  API; `SonarrIndexerFlagsFixture` pins the table independently of the code).
  A flag Sonarr lacks (`PTP_Golden`, `PTP_Approved`) is dropped going the
  other way. A future app with a new flag appends a bit above 2048.
- The module-typed interface keeps its upstream name,
  `IDownloadDecisionEngineSpecification` (on `RemoteMovie` for Movies), in
  place of the generic `IReleaseSpecification<TRemote>` sketched above. A
  generic adds nothing while each module has one decision maker, and the
  upstream name keeps ported specifications unchanged.
- The decision maker stays per module for now (Movies' takes both sets) and
  still returns `DownloadDecision` with `RemoteMovie`. Both sets share one
  priority order: a priority group with any rejection stops evaluation, so a
  movie rejection at `Default` skips the core `FreeSpace` check at `Disk`,
  and the reverse.

As built in porting step 9j (grab and decision processing):

- `DownloadDecision` holds a `RemoteRelease`. Movie code reads it as a
  `RemoteMovie` through `GetRemoteMovie()` (`NzbDrone.Core.Movies`), which
  returns null for another media type.
- `PrioritizeDownloadDecision` (core) groups decisions by `RemoteRelease.Kind`
  and calls the `IDownloadDecisionPrioritizer` registered for that kind
  (`Kind`, `Prioritize`). Movies' is `DownloadDecisionPriorizationService`. A
  kind with no prioritizer keeps its place at the end of the list.
- `ProcessDownloadDecisions` (core) is media-agnostic. A delayed release goes to
  the `IPendingReleaseHandler` for its kind (`Kind`, `Add`, `AddMany`); Movies'
  is `PendingReleaseService`. A kind with no handler throws
  `InvalidOperationException` instead of dropping the release. A release counts
  as already processed only when another one of the same kind shares an item id.
- `DownloadService.DownloadReport` takes a `RemoteRelease` and publishes
  `ReleaseGrabbedEvent` (was `MovieGrabbedEvent`), which carries
  `RemoteRelease Release`. Handlers that write movie data (`HistoryService`,
  `MovieNotificationHandler`, `PendingReleaseService`, the movie SignalR
  controller) return early when `Release.Kind` is not Movies.
  `DownloadHistoryService`, `TrackedDownloadService` and
  `DownloadMonitoringService` are kind-agnostic.
- No schema change.

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

As built in porting step 9e (compared with Sonarr `b3d4bb8`):

```csharp
public interface IDownloadImporter
{
    MediaKind Kind { get; }
    DownloadImportCheck Check(TrackedDownload trackedDownload);   // Ready, Skip, Blocked
    IReadOnlyList<IImportResult> Import(TrackedDownload trackedDownload, string outputPath);
    bool IsImported(TrackedDownload trackedDownload, IReadOnlyList<IImportResult> importResults);
    void ImportBlocked(TrackedDownload trackedDownload);
}

public interface IImportResult
{
    ImportResultType Result { get; }
    List<string> Errors { get; }
    string Path { get; }                       // null when no file was matched
    FailDownloads? FailsDownloadAs { get; }    // the indexer option this rejection falls under
}
```

- `CompletedDownloadService` and `RejectedImportService` are core and read
  only `TrackedDownload`, `RemoteRelease`, and `IImportResult`. The
  completed-download check splits: the core checks status, state, and the
  output path; the module's `Check` does the grab-history and title match
  (Radarr's and Sonarr's are the same shape with different entities). The
  output path is now checked before the grab-history test, so a download
  that fails both shows the path warning.
- `IsImported` takes the results just returned. Radarr marks a download
  imported when any file was; Sonarr requires every episode in
  `RemoteEpisode`. Both fall back to history, so the whole decision is the
  module's. `TrackedDownloadAlreadyImported` moved to
  `NzbDrone.Core.Movies.Import`.
- `ImportBlocked` replaces the core's `ManualInteractionRequiredEvent`
  publish: the event carries `RemoteMovie` and movie grab history, so the
  module raises it. The core still sends it once per tracked download.
- `ImportMode` is not in the contract. Automatic import is always `Auto`;
  manual import calls the module directly.
- `ImportResult` keeps its movie shape and implements `IImportResult`. No
  `ItemIds`: the core reads nothing per item. `FailsDownloadAs` maps the
  first rejection (`DangerousFile`, `ExecutableFile`), as
  `RejectedImportService` did. Sonarr's `UserRejectedExtension` arrives
  with a `FailDownloads` value in Phase 3.
- `ImportResultType` moved to `NzbDrone.Core.Modules`; values unchanged.
- `TrackedDownload` carries `Kind` and `RemoteRelease`; `RemoteMovie` is a
  typed view of `RemoteRelease`. A download with no kind, or a kind with no
  importer, is blocked with a warning.
- Routing: `TrackedDownloadService` is still movie code and sets `Kind` to
  Movies for every download it tracks. The rules below come with the TV
  module, when tracking is generalized.
- The "not imported or missing" status names the items by kind ("movies",
  "episodes", "tracks").

As built in porting step 9k (failure, ignore, redownload, download history,
seed configuration; compared with Sonarr `b3d4bb8`). Two small core contracts,
chosen by `Kind`, in `NzbDrone.Core.Modules`:

```csharp
public interface IFailedDownloadSource
{
    MediaKind Kind { get; }
    bool WasGrabbed(string downloadId);
    DownloadFailedEvent Describe(string downloadId, string message, TrackedDownload trackedDownload, bool skipRedownload);   // null without a grab
    DownloadFailedEvent Describe(int historyId, string message, bool skipRedownload);
}

public interface IRedownloadSearcher
{
    MediaKind Kind { get; }
    void Search(DownloadFailedEvent failure);
}
```

- `FailedDownloadService` is core. It runs the state machine (`Check`,
  `ProcessFailed`, `MarkAsFailed`) and asks the source for the tracked
  download's `Kind` whether the module grabbed the download and what the
  failure is about. `MarkAsFailed(int historyId)` became
  `MarkAsFailed(MediaKind kind, int historyId)`; the history controller passes
  Movies. A download with no kind, or a kind with no source, counts as not
  grabbed: the queue shows the existing warning and nothing is failed. A
  history-id request for a kind with no source throws.
- `RedownloadFailedDownloadService` is core. It applies the user's settings
  (skip requested, auto redownload, interactive search) and hands the failure to
  the module's searcher. Movies: `MovieRedownloadSearcher` pushes a movie
  search. Sonarr's version (episode, season or multi-episode search) is the
  TV module's searcher.
- `IgnoredDownloadService` is core and needs no contract: it reads the neutral
  `RemoteRelease` (`Parent`, `Quality`, `Languages`).
- `DownloadFailedEvent` and `DownloadIgnoredEvent` carry `Kind` (no default;
  the publisher sets it) and `ParentId` (was `MovieId`).
  `DownloadCompletedEvent.MovieId` is `ParentId`; its media type is
  `TrackedDownload.Kind`. No `ItemIds` yet: Sonarr's events carry `EpisodeIds`
  and the TV module adds them when it is their first reader. Movie-only
  handlers (`BlocklistService`, `HistoryService`) ignore another kind.
- `DownloadHistoryService` is core and records failed, ignored and completed
  downloads for the kind the event names. Its two movie-only handlers
  (`MovieFileImportedEvent`, `MoviesDeletedEvent`) moved to
  `Movies.Downloads.MovieDownloadHistoryHandler`. `DownloadHistoryRepository`
  is no longer scoped to Movies: a download id belongs to one download whatever
  its media type, so lookups by download id see every row, and the delete is
  `DeleteByParentIds(kind, ids)` because movie, series and artist ids overlap.
  It refuses a row with no media kind.
- `DownloadSeedConfigProvider` is core and reads no media type. The old code
  also required the grabbed title to parse as a movie, which no value used;
  the indexer is known from the grab, so its seed settings now apply to any
  title. Sonarr's season pack seeding (`ISeedConfigProvider` with a
  `fullSeason` flag) is not in Prowlarr's shared seed settings yet; it comes
  with the TV module.
- Movies: `MovieFailedDownloadSource` reads the movie history,
  `MovieRedownloadSearcher`, `MovieDownloadHistoryHandler`, all in
  `NzbDrone.Core.Movies.Downloads`.
- Upstream behavior kept: for a history row that has a download id,
  `MarkAsFailed(historyId)` drops the skip-redownload flag (Sonarr is the
  same).

#### Tracked download routing

A download in the client must be attributed to one module before import:

1. `DownloadHistory` lookup by `DownloadId`. The row records `MediaKind`.
   This covers everything Plattarr grabbed.
2. The download client category. Each category maps to one module (see
   [Download client settings](#download-client-settings)).
3. Neither available (category empty or `*`, no history): ask each enabled
   module's `MapFromTitle`. Exactly one match wins. Zero or several matches
   leave the download in the queue with a `ManualInteractionRequired` warning.

As built in TV porting step 0, item 3 (tracked download routing). Contract 5
gains `ITrackedDownloadMatcher` (`NzbDrone.Core.Modules`): `Kind`,
`MatchesTitle(title)`, `Match(trackedDownload, downloadHistory)` and
`Rematch(trackedDownload)`. `TrackedDownloadService` no longer parses titles or
reads movie history. It takes every matcher, picks the media type of a new
download, stores it in `TrackedDownload.Kind`, and calls that matcher's `Match`:

1. The latest `DownloadHistory` row for the download id, when its `MediaKind`
   has a matcher.
2. The client category: `IModuleCategorySettings.KindForCategory` returns the
   media type whose entry holds the category (case-insensitive, empty and
   unknown categories give none). Only clients with category settings take
   part.
3. The title: each matcher's `MatchesTitle`. Exactly one yes wins. With no yes
   and a single matcher installed, that matcher owns the download (this is
   the Movies-only behavior of every earlier step, kept so a one-module
   install is unchanged). With several yes, or no yes and several matchers,
   `Kind` stays empty, the download is still tracked and shown in the queue,
   and `CompletedDownloadService` blocks it with "No media type claims this
   download" (a deliberate difference from the text above, which warns with
   `ManualInteractionRequired`: with no kind there is no module to send that
   notification).

`TrackedDownload.RemoteMovie` is gone. Movie code reads
`trackedDownload.GetRemoteMovie()` / `GetMovie()` (`MovieTrackedDownloadExtensions`,
like `MovieQueueExtensions` for the queue). The movie logic moved unchanged to
`Movies.Downloads.MovieTrackedDownloadMatcher`: parse the title, map it (by
`ParentId` when the history row is `DownloadImported`), fall back to the grab
history's source title, copy indexer and indexer flags from the grab, augment,
calculate custom formats, and warn on the tracked download for several movies
or a parse failure. The library events (`MovieAdded/Edited/BulkEdited/Deleted`)
moved to `MovieTrackedDownloadRefresher`, which selects the Movies downloads
that are affected and calls `ITrackedDownloadService.Rematch`; the service asks
each one's matcher to `Rematch` and publishes `TrackedDownloadRefreshedEvent`
once. The refresher also turns `MovieFileImportedEvent` into the core
`RefreshDownloadsRequestedEvent`, which `DownloadMonitoringService` debounces
as before (it used to handle the movie event itself). `TrackedDownload`,
`TrackedDownloadService` and `DownloadMonitoringService` left the boundary
allowlist (30 to 27). No schema change.

As built in porting step 9l (aggregators; compared with Sonarr `b3d4bb8`).
No core contract. Radarr's and Sonarr's aggregation services each take their
own remote type (`RemoteMovie`, `RemoteEpisode`), and Sonarr's only aggregator
(`AggregateLanguages`) reads the episode parse result and series, so the
aggregators are module code. `IRemoteMovieAggregationService`,
`RemoteMovieAggregationService`, `IAggregateRemoteMovie` and
`AggregateLanguages` moved to `NzbDrone.Core.Movies.Aggregation`; their
callers (`DownloadDecisionMaker`, `PendingReleaseService`, the parse
controller) are movie code, and `TrackedDownloadService` stayed on the
allowlist until tracking was generalized (see the routing step above). Freeing the `Download.Aggregation`
name lets the TV module add its own without a rename. No behavior or schema
change.

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

As built in step 8c: the four tables carry a non-null `MediaKind` with no
default, and their `MovieId` indexes lead with it. Each module reads and writes
them through a `MediaKindRepository<T>`, which adds `MediaKind = <kind>` to
every query, delete, count, and purge, and refuses to insert a row of another
kind (including an unset 0). The orphan housekeepers only look at movie rows.
`MovieId` keeps its name until the boundary step generalizes the models; the
rename to `ParentId` and `ItemId`/`ItemIds` is then a column rename. Tracked
download routing rule 1 needs a core reader of `DownloadHistory` across kinds;
it comes with the routing work, not here.

As built in step 9f:

- Migration 002 (the first since `001_initial`; it upgrades a database in
  place) renames `MovieId` to `ParentId` on all four tables, renames
  `PendingReleases.ParsedMovieInfo` to `ParsedInfo` (the owning module's parsed
  release as JSON, read back by the module that wrote it), and replaces the
  three `MovieId` indexes with `ParentId` ones. No row changes value: for
  Movies the parent is the movie. The models carry the new names because the
  table mapper maps a property to the column of the same name. The v3 API
  keeps `movieId` on the wire.
- No `ItemId` on `History` and no `ItemIds` on `Blocklist` or `PendingReleases`
  yet. For Movies parent and item are the same entity, so the columns would
  duplicate `ParentId` and nothing would read them (the same rule that left
  `ItemIds` out of contract 5). They arrive as an additive migration with the TV
  module's history and blocklist code. `PendingReleases` may never need
  `ItemIds`: Sonarr's pending release stores only the series and re-derives
  the episodes by re-parsing (finding 26).
- `IActivityRenderer` and `ActivitySubject` are not built. Their first caller
  is contract 7 (`GrabMessage.Subject`); the queue does not need them because
  `RemoteRelease.Parent` already carries the title and path. They are built
  with contract 7.
- The core `Queue` carries `RemoteRelease` instead of `Movie` and
  `RemoteMovie`. `QueueService` reads quality from the new
  `RemoteRelease.Quality` and languages from `RemoteRelease.Languages`. Movie
  code reads the queue through `GetRemoteMovie()` and `GetMovie()`
  (`MovieQueueExtensions`). A queue item has no `Kind` yet; it is added with
  the routing work, when a second module can own a download.
- Pending releases are module code. `PendingRelease`, its repository, and
  `PendingReleaseService` moved to `NzbDrone.Core.Movies.Pending` and are
  mapped by `MoviesTableMap`, like `MovieHistory` and `Blocklist`. The
  `PendingReleaseReason` enum and `PendingReleasesUpdatedEvent` stay in core.
- Boundary allowlist: 135 to 129 (`Queue`, `QueueService`, and the four
  pending-release types).

```csharp
// Deferred to contract 7 (see the 9f note above).
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
gain a per-media-type toggle so a user can, for example, notify on movie grabs
only.

As built in porting step 9g (contract 7). The core no longer references
a movie type in `Notifications`; 56 types left the boundary allowlist, and
`ManualInteractionRequiredEvent` with them (129 to 72). The shapes differ
from the sketch above in these ways:

- **Messages carry two channels.** `Subject` (`ActivitySubject`) holds what
  text providers show: title, `ItemLabel`, path, year, overview, genres,
  rating, poster and fanart URLs, tags, `ExternalIds` (keyed by
  `ExternalIdTypes`) and `Links`. `Data` (`IMediaNotificationData`, with
  `Kind`) holds the module's own objects. Only the module's builders read
  `Data`: `IWebhookPayloadBuilder`, `ICustomScriptEnvironmentBuilder`,
  (the Trakt builder hook was removed with the Trakt provider). Core picks the builder with
  `Single(b => b.Kind == message.Data.Kind)`. Files and releases are neutral
  (`NotificationFile`, `NotificationRelease`), so there is no `Kind` or
  `RemoteRelease` on `GrabMessage` itself.
- **The module raises messages.** `Movies.Notifications.MovieNotificationHandler`
  handles the movie events, builds messages with `MovieActivityRenderer`
  (an `IActivityRenderer`, found by `Kind`) and calls the core
  `INotificationDispatcher`, which `NotificationService` implements. Core
  keeps the provider selection: enabled events, tag filter, the upgrade rule
  (`OldFiles.Empty() || OnUpgrade`), the file-delete rule, failure
  recording.
- **`AnyEnabled(NotificationEvent)`** lets a module skip building a message
  when no provider listens. Without it every bulk `MoviesImportedEvent`
  would load metadata for each movie.
- **`ActivitySubject.RootFolderPath` is lazy.** `RootFolderService.All()` is
  expensive; the module supplies a resolver and only Jellyfin pays.
- **Renamed provider methods:** `OnRename(RenameMessage)`,
  `OnMediaAdded`, `OnMediaFileDelete`, `OnMediaDelete`. `Supports*`
  properties, the `NotificationDefinition` columns (`OnMovieAdded`,
  `OnMovieDelete`, `OnMovieFileDelete`, `OnMovieFileDeleteForUpgrade`) and
  the v3 API names are unchanged: no migration.
- **Deferred:** the per-media-type toggle and the column rename. They have
  no purpose until a second module exists, and the rename needs a migration
  plus an API alias.
- **Moved to `NzbDrone.Core.Modules`:** `DeleteMediaFileReason`,
  `RenameCompletedEvent`, `DeleteCompletedEvent`.
- **Link URLs normalized.** Metadata links are built once by the renderer.
  Discord's Links field now reads `https://www.themoviedb.org/movie/{id}`
  and `https://www.imdb.com/title/{id}` (no trailing slash). The Trakt link
  type was selectable in a provider's `MetadataLinks` setting but ignored by
  the providers that use the generator; it now works (finding 27).
- **Other behavior changes:** `MoviesImportedEvent` handles each movie
  separately, so one failure no longer skips the rest; Discord omits the
  TMDb link when the TMDb id is 0; Discord's file-delete path is
  `Combine(ParentPath, RelativePath)`.
- **Not done here:** `DownloadMonitoringService` still handles
  `MovieFileImportedEvent`, so it stays on the allowlist. It handles
  `ReleaseGrabbedEvent` since step 9j.

`NotificationGoldenFixture` pushes a fixed movie library through the handler
into Webhook, Custom Script and Discord and compares the output with
`Files/Notifications/Golden`. The webhook and script files are byte-identical
to the capture taken before the change.

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

Defaults: `plattarr-movies`, `plattarr-tv`, and `plattarr-music`, filled in
when a client is added, so the user doesn't have to think about categories.
The client settings page shows them under an "Advanced" toggle.

Validation: within one client, each media type's category must be unique
and non-empty. Routing step 2 depends on it.

The migration importer maps Radarr's `MovieCategory` to the Movies entry.
When `sonarr.db` is imported in Phase 3 and a client with the same host and
port exists, the importer adds a TV entry to that row instead of creating a
second client.

The tested setup used qBittorrent behind gluetun with one shared
download/media path. Separate categories per module are required before TV is
enabled, or routing falls to title parsing.

As built in porting step 9i (migration 003). Since the prune only qBittorrent
and SABnzbd remain; the notes below describe all 11 clients as built and hold
for those two:

- `ModuleCategorySettings` (`NzbDrone.Core.Download.Clients`) is as sketched, with
  value equality. The 11 clients that route by category (qBittorrent, Deluge,
  rTorrent, uTorrent, Transmission and Vuze, Hadouken, Freebox Download,
  Download Station, SABnzbd, NZBGet, NZBVortex) derive their settings from
  `ModuleDownloadClientSettings<T>` and carry `[ModuleCategoryFields]`. The
  settings hold `ModuleCategories`, a list with one entry per media type. A new
  client starts with the Movies entry; the form adds an entry for every other
  registered media type when it is saved. The list is sparse on purpose: a
  media type with no entry has no category on that client.
- Core code and clients read the list through `IModuleCategorySettings`:
  `For(kind)` (throws a `DownloadClientException` that names the media type when
  there is no entry, so a grab fails with a reason), `ForCategory(category)`
  (case-insensitive; a download that no entry claims belongs to Movies until
  tracked downloads route by category with TV), `ForItem`,
  `MonitoredCategories()` and `Monitors(category)`. A client with no category
  configured takes every download, as it always did.
- The form fields are generated by `SchemaBuilder` for every registered
  `IMediaModule`, with the upstream wire names (`movieCategory`,
  `movieImportedCategory`, `recentMoviePriority`, `olderMoviePriority`, and
  `tvCategory` and so on for the other media types). A client never names a media
  type. With one module the labels are unchanged; with more, each label adds the
  media type. The v3 `downloadclient` resources therefore keep their wire fields
  without a mapping.
- `GetItems()` asks the client for each monitored category and attributes each
  download to its entry (qBittorrent and Deluge make one request per category;
  SABnzbd and NZBVortex filter server-side only when there is one category).
  `GetStatus()` reports one output folder per category.
- Migration 003 moves the stored values into a Movies entry. Four clients stored
  them under other names: NZBVortex and both Download Station clients used
  `tvCategory`, Freebox Download and Hadouken `category`, and Freebox Download
  `recentPriority` and `olderPriority`. An empty category stays empty. Only the
  `Settings` column changes.
- Deviations from the sketch above, both reversible. Validation requires each
  media type's category to be unique within a client, but not non-empty: whether
  an empty category is allowed stays each client's own rule (uTorrent refuses it,
  rTorrent warns, qBittorrent accepts it). Only the imported category is behind
  "Advanced"; the category and priorities are shown. The default category for a
  new client is now `plattarr-movies` (Radarr's clients defaulted to `radarr`
  or empty); existing clients keep what they stored.
- Settings that name a directory are per-client paths, not per-media-type; a
  client that needs separate folders per media type uses categories.

### Indexer settings

Each indexer has one global setting. There is no per-media-type indexer
configuration in the default UI.

- **Enable flags.** `EnableRss`, `EnableAutomaticSearch`, and
  `EnableInteractiveSearch` move onto the shared indexer definition and apply
  to every media type.
- **Seeding.** `SeedRatio`, `SeedTime`, `PackSeedTime`, and
  `MinimumSeeders` are global per indexer. `PackSeedTime` covers Sonarr's
  season packs and Lidarr's discographies.
- **Categories.** Derived, not configured. Prowlarr already maps every
  indexer's native categories to the standard Newznab tree in its
  capabilities. A search for movies uses the indexer's 2000-range
  categories, TV uses 5000, anime uses 5070, music uses 3000. RSS results are
  routed by the same tree (Contract 2).
- **Indexer-wide flags.** `FailDownloads` and
  `RejectBlocklistedTorrentHashesWhileGrabbing` move onto the shared indexer
  definition.

As built in porting step 6 (`ec0d294`): the three enable flags are columns
on `IndexerDefinition` (default true) beside Prowlarr's `Enable`, which stays
the master switch. `FailDownloads` is in `IndexerBaseSettings`.
`MinimumSeeders` (Prowlarr's `AppMinimumSeeders`, default 1) and
`RejectBlocklistedTorrentHashesWhileGrabbing` are in
`IndexerTorrentBaseSettings`, next to the seed ratio and times. Settings
blocks render as indexer form fields, so they need no API or UI work of their
own. `IndexerOverrides` waits for step 7, its first reader.

As built in step 7 (`61fd6e7`, `ac24a75`; in `001_initial` since step 8a):
`IndexerOverrides` (unique per indexer and media type; rows go with their
indexer). Fields with readers today: `MultiLanguages`, `RequiredFlags`
(torrent releases, as in Radarr), `RemoveYear`, and `Categories` (exact IDs,
replacing the media type's defaults on that indexer for search and RSS).
Sonarr's and Lidarr's fields come with their modules. The API is
`/api/v3/indexeroverride`; there is no UI section yet.

Some upstream per-app fields have no global equivalent: `MultiLanguages`,
`RequiredFlags`, `RemoveYear` (Radarr), `AnimeStandardFormatSearch` (Sonarr),
`EarlyReleaseLimit` (Lidarr), and a manual category override for indexers
that miscategorize. These live in an optional `IndexerOverrides` table, one
row per indexer and media type, edited from an "Advanced" section on the
indexer page. Rows exist only when a user sets one. Defaults are empty, and
most users never see the section.

The migration importer fills global values from `prowlarr.db` first. Values
from `radarr.db` or `sonarr.db` that differ from the global value become
override rows, so nothing the user configured is lost.

### Configuration

Settings are global by default. A setting that can reasonably differ between
media types can be overridden for one media type.

- **Global only:** `CalendarWeekColumnHeader`, `DownloadClientHistoryLimit`,
  `DownloadClientWorkingFolders`, `EnableColorImpairedMode`,
  `EnableCompletedDownloadHandling`, `FirstDayOfWeek`, `LongDateFormat`,
  `MaximumSize`, `MinimumAge`, `Retention`, `RssSyncInterval`,
  `ShortDateFormat`, `ShowRelativeDates`, `TimeFormat`,
  `TrustCgnatIpAddresses`, `UILanguage`.
- **Global with per-media-type override:** `AutoRedownloadFailed`,
  `AutoRedownloadFailedFromInteractiveSearch`, `ChmodFolder`, `ChownGroup`,
  `CleanupMetadataImages`, `CopyUsingHardlinks`, `DeleteEmptyFolders`,
  `DownloadPropersAndRepacks`, `EnableMediaInfo`, `ExtraFileExtensions`,
  `FileDate`, `ImportExtraFiles`, `ListSyncLevel`,
  `MinimumFreeSpaceWhenImporting`, `RecycleBin`, `RecycleBinCleanupDays`,
  `RescanAfterRefresh`, `ScriptImportPath`, `SetPermissionsLinux`,
  `SkipFreeSpaceCheckWhenImporting`, `UseScriptImport`.

Storage: the global value uses the plain key (`RecycleBin`). An override uses
a prefixed key (`Movies.RecycleBin`). `ConfigService` reads the override
first, then the global value. Radarr has 50 `ConfigService` keys and Sonarr
49; the 38 they share are all in the two lists above. `ConfigKey` is a
helper, not a setting.

Media-type-only settings (for example, Sonarr's episode naming tokens) are
not overrides; they belong to that media type's settings page.

### Qualities, languages, profiles, and custom formats

- **One global quality catalog** with Plattarr IDs: about 31 video qualities
  (the union of Radarr's and Sonarr's, aliases merged) and Lidarr's 38 audio
  qualities. Each quality records which media types it applies to (video
  qualities: movies and TV; audio: music).
- **One global language catalog** with Plattarr IDs: the 64 distinct names
  from Radarr and Sonarr. Lidarr has no release-language model to merge.
- **Parsers stay per media type, output is global.** Video and audio quality
  detection are different programs (Lidarr's `QualityParser.cs` shares almost
  nothing with Radarr's), so each module keeps its parser, and every parser
  returns catalog qualities and languages. The Radarr and Sonarr video
  parsers are 80% the same; merging them is optional later work, not
  required.
- **Quality profiles and custom formats are global.** Each has an
  `AppliesTo` set of media types, defaulting to every type its qualities
  support. A "1080p" profile created once serves movies and TV; a "Lossless"
  profile serves music. A user who wants a different profile for TV picks one
  per series, the same as today, or creates a TV-only profile.
- **Import remap.** The importers translate each upstream quality and
  language ID to a catalog ID with a static name table, including the two
  alias pairs. Profiles and custom formats imported from Radarr and Sonarr
  that are identical after remap are merged into one; others are kept and
  scoped to their source media type.
- **API remap.** The compatibility APIs translate catalog IDs back to the
  upstream app's IDs, so tools that send Radarr or Sonarr quality IDs
  (Recyclarr, Jellyseerr) keep working (see
  [Compatibility APIs](#compatibility-apis)).
- Release group parsing moves to the core.

Trade-off: upstream quality parser fixes that add a new quality need a new
catalog entry and a remap row. That happens a few times a year at most.

### Other tables

Global, optionally scoped to media types with `AppliesTo`: delay profiles,
release profiles, tags, auto-tagging, import list exclusions.

Per media type, because the values can't be shared: root folders (a movie
folder and a TV folder are different places), naming formats, import lists
(each list type returns one media type), metadata consumers, and all media
and file tables (module-prefixed).

Shared with no scoping: remote path mappings, indexers and their status,
download clients and their status, notifications and their status, users,
config, commands, logs, scheduled tasks, custom filters.

### Database migrations

One FluentMigrator chain and one `VersionInfo` table for the whole schema,
starting at `001_initial`. All migrations live in the core project's
`Datastore/Migration` folder; module code adds tables through ordinary
numbered migrations.

- Phase 1's `001_initial` creates the core tables, the global catalogs, and
  the `Movies_` tables.
- Phase 3 adds the `Tv_` tables in a later migration; Phase 4 adds `Music_`.
- Hidden media types still have their tables. An empty table costs nothing.

As built in step 8a: `001_initial` reproduces the schema Radarr's migrations 1
to 242 plus Plattarr's 243 and 244 produced on Postgres, with Radarr's legacy
indexer tables dropped and Prowlarr's under the plain names `Indexers` and
`IndexerStatus`. The maintenance check `PreviousSchemaCheck` refuses a
database whose version 1 is upstream's `initial_setup`. `MediaKind` on
activity tables and the global catalogs follow in later step 8 commits, each
edited into `001_initial` while no Plattarr database outside dev exists.

Step 8b: the 16 movie-owned tables carry the `Movies_` prefix in full
(`Movies_Movies`, `Movies_MovieFiles`, `Movies_RootFolders`, ...), so an
upstream raw SQL fix ports by adding the prefix. Shared: activity tables,
import list exclusions, delay and release profiles, and every core table.
API sort keys keep upstream's table names (`movieMetadata.sortTitle`);
`TableMapper` resolves them through `ModuleTablePrefixes`.

### Compatibility APIs

Goal: tools built for Radarr, Sonarr, and Lidarr work against Plattarr
unchanged, with only the URL base changed in their settings.

| URL base | Emulates | Source of the controllers |
|---|---|---|
| `/radarr/api/v3` | Radarr v3 | Radarr's `Radarr.Api.V3`, which Plattarr already contains |
| `/sonarr/api/v3` | Sonarr v3 | Sonarr's `Sonarr.Api.V3`, ported with the TV module |
| `/lidarr/api/v1` | Lidarr v1 | Lidarr's `Lidarr.Api.V1`, ported with the Music module |
| `/{indexerId}/api` | Prowlarr's per-indexer Newznab and Torznab feeds | Prowlarr's `NewznabController`, ported with the indexer layer |

Design:

- **Full fidelity, not a subset.** Each URL base serves the upstream app's
  complete API surface, because the controllers are the upstream
  controllers. This reverses the 2026-10-20 "captured subset only" scope.
  Phase 2 still records Jellyseerr's real calls through a logging proxy, now
  as a test fixture rather than a scope limit.
- **Shared data, upstream shapes.** Resources that are global in Plattarr
  (`tag`, `qualityprofile`, `customformat`, `qualitydefinition`,
  `downloadclient`, `indexer`, `notification`, `system/status`) are read from
  the global tables and shaped as the upstream app would return them.
  `qualityprofile` under `/radarr` returns profiles whose `AppliesTo`
  includes movies, with Radarr quality IDs.
- **ID translation.** Quality and language IDs are translated in both
  directions at the shim boundary. Entity IDs (movie, series, artist) need no
  translation because each media type owns its own table.
- **One API key.** All URL bases accept Plattarr's API key.
- **Identity.** `system/status` reports `appName` `Radarr`, `Sonarr`, or
  `Lidarr` under the matching base. Tools use it to detect the app type.
- The existing Radarr UI keeps calling `/api/v3` until the unified frontend
  replaces it. During Phase 1, `/api/v3` and `/radarr/api/v3` serve the same
  controllers.

Not emulated: Prowlarr's own management API (`/api/v1/indexer` and so on).
Tools that read it are dashboards (Homepage, Homarr) and mobile apps.
Revisit if one the owner uses needs it.

Delivery is on demand. The design keeps every tool reachable, but testing
and fixes happen in this order:

1. **Required for cutover (Phase 2):** Jellyseerr, the only tool in use
   today. Plattarr can't replace the live apps until a Jellyseerr request
   works end to end.
2. **Owner's next tool:** Homarr. Its Radarr, Sonarr, and Lidarr widgets
   read the URL bases above. Its indexer widget reads Prowlarr's management
   API, which is not emulated; that widget is deferred until requested.
3. **Everything else, when a user asks:** Overseerr, Ombi, Bazarr,
   Recyclarr, Notifiarr, Unpackerr, autobrr, cross-seed (Torznab feeds),
   Maintainerr, Kometa, Homepage, and mobile clients such as LunaSea, nzb360,
   and Ruddarr. Each is a test-and-fix task, not new API work, because the
   endpoints are already the upstream ones.

What must be right from Phase 1 so on-demand integrations stay cheap: the
URL base routing, and quality and language ID translation at the API
boundary. Both are needed anyway, for Jellyseerr and for the importers.

Open item, per tool as it is tested: confirm it accepts a URL base. Most
expose one as a field or accept it in the host URL. A tool that hardcodes
`/api/v3` at the server root gets the Radarr API at `/api/v3` by default;
which app answers at the bare root is a setting.

### What stays inside a module

The core does not call these, so they are not part of the interface. Each
module keeps its upstream code:

- Metadata provider (TMDB, TheTVDB via Sonarr's proxy, MusicBrainz via
  Lidarr's proxy) and refresh.
- Entity model, monitoring rules, and wanted (missing and cutoff unmet).
- Naming and folder templates.
- Import lists, extras, and media covers.
- The quality and language parsers (output is global, see above).
- The module's compatibility API controllers.

## TV module: step 1 design

Written in session 35 against Sonarr `develop` at `b3d4bb8c18f7feb6744a6cd3231279a45351bb5e`,
read from its table mapping, `Tv` folder and activity entities. Each point is a call made by
Claude (delegated, reversible until migration 004 ships).

**Tables.** All owned tables start with `Tv_`, mirroring Movies:

| Table | From Sonarr | Note |
|---|---|---|
| `Tv_Series` | `Series` | `QualityProfileId` references the global profile; `RootFolderPath` and `Translations` are not stored. |
| `Tv_SeriesTranslations` | `SeriesTranslations` | |
| `Tv_Episodes` | `Episodes` | `EpisodeFileId` points at `Tv_EpisodeFiles`. |
| `Tv_EpisodeFiles` | `EpisodeFiles` | `Quality` and `Languages` use the global catalogs through the same two converters. Includes Sonarr's `ReleaseType` column (int, 0 unknown). |
| `Tv_SceneMappings` | `SceneMappings` | TV only (XEM and TheTVDB scene data). |
| `Tv_RootFolders` | `RootFolders` | One table per module, as in Movies. |
| `Tv_NamingConfig` | `NamingConfig` | Standard, daily and anime episode formats; series, season and specials folder formats; multi-episode style and colon replacement. |

**Not a table.** The direction note's "SeasonStatistics" is a computed query over `Episodes` and
`EpisodeFiles` (Sonarr's `SeriesStats` folder), not a table. It is ported as a repository query
in step 3.

**Deferred tables.** `Tv_MetadataFiles`, `Tv_SubtitleFiles`, `Tv_ExtraFiles` and `Tv_Metadata`
arrive with the import step (extras). `Tv_ImportLists`, `ImportListItems` and the list status
table wait for the import list decision (plan section 6, pruning candidates): the feature is
not carried per module twice.

**Shared tables.** `History`, `Blocklist`, `PendingReleases` and `DownloadHistory` already carry
`MediaKind` and `ParentId` (`ParentId` is the series). What TV needs that they lack, added by
migration 004 and read first by the TV module (the "no ItemId columns until TV reads them" rule):

| Table | Added | Why |
|---|---|---|
| `History` | `ItemId` (int, default 0) | Sonarr writes one history row per episode (`EpisodeId`). |
| `Blocklist` | `ItemIds` (JSON list, default `[]`) | Sonarr's `EpisodeIds`. |
| `Blocklist` | `ReleaseType` (int, default 0) | Sonarr's single-episode, multi-episode, season pack. Movies rows keep 0. |

`DownloadHistory` and `PendingReleases` need nothing: Sonarr keeps `SeriesId` only, and
`PendingReleases.ParsedInfo` already holds any module's parsed document through its own type
handler. Migration 004 upgrades in place, is proven on a copy of the dev config first, and gets a
`MigrationTest<T>` with old-shape rows.

**What step 1 ports.** Entities and repositories only: `Series`, `Episode`, `Season`,
`SeriesTranslation`, `Actor`, `Ratings`, `SceneMapping`, the status and type enums, `EpisodeFile`
and their repositories, plus `TvModule` and `TvTableMap` (prefix `Tv_`, `MediaKind.Tv`). No
services that call a metadata source, parser or indexer: those come with their steps. The Sonarr
`Tv` folder is 47 files; none shares a path with a Plattarr file.

**Media file code is not shared yet.** Radarr's disk scan, file moving, rename, recycle bin and
script import code sits under `Movies.MediaFiles`; Sonarr has the same set at its `MediaFiles`
root. Steps 1 to 4 do not touch it. At step 5 the files that are identical in both apps
(`FileDateType`, `FileExtensions`, `MediaFileExtensions`, `RecycleBinProvider`, the `Script*`
types, `MediaInfo`) move to core once, and the episode and movie specific ones live in
`Tv.MediaFiles` and `Movies.MediaFiles`. Do that move as one commit before the TV import code.

**Namespace.** `NzbDrone.Core.Tv.*`, which `ModuleBoundary` already treats as a module. Types that
share a full name with a core or Movies type take the module name (`Tv.Parser.Parser`).

### TV step 1: as built

Built in session 36. Compared with Sonarr `b3d4bb8`. Migration 004 (`004_tv_module_tables`)
adds `History.ItemId`, `Blocklist.ItemIds`, `Blocklist.ReleaseType`, the index
`IX_History_MediaKind_ItemId_Date` and the seven `Tv_` tables of the design. It changes no
existing row. `ModuleBoundaryAllowlist` is unchanged at 27 and migrations 001 to 003 are
untouched. Calls made while building (each is reversible):

- **`MediaCover` moved to core** (`NzbDrone.Core.MediaCover`, `be15834`). Both modules need it and
  a module may not reference another. The Movies cover code is now `Movies.Covers`.
- **`TvModule`**: `Name` is `"Tv"` (the table prefix check requires `Name + "_" == TablePrefix`),
  `Kind` is `MediaKind.Tv`, `DefaultCategories` is `{5000}`, and `ScheduledTasks` is empty until
  the steps that own a task.
- **Entities and repositories ported**: `Series`, `Episode`, `Season`, `SeriesTranslation`,
  `Actor`, `Ratings`, `SceneMapping`, `EpisodeFile`, `RootFolder`, `NamingConfig`,
  `EpisodeHistory`, `Blocklist` and the enums. `EpisodeHistory` and `Blocklist` are
  `MediaKindRepository` views of the shared tables (`ParentId` is the series, `ItemId` and
  `ItemIds` are episodes).
- **Dropped from Sonarr**: `Blocklist.Source` (Radarr never stored it and the core failed
  download event has no such field) and the quality-rank sort on History and Blocklist pages
  (Sonarr's `QualityProfileQualityRanks` table does not exist here).
- **`Tv_Episodes.TvdbId` is `NOT NULL` default 0.** Sonarr's column is nullable. The importer maps
  null to 0.
- **Copies kept per module for now**: Tv has its own `MediaInfoModel` (Sonarr's shape; it differs
  from Radarr's), `ColonReplacementFormat` (it has `Custom`), `MultiEpisodeStyle` and
  `ReleaseType` (`Tv.Parser.Model`). Unify the media info models at step 5 with the media file
  move.
- **`BasicRepository.ModelUpdated` and `PublishModelEvent` take `forceEvent`.** The Sonarr episode
  repository publishes a model event from `SetFileId` and `ClearFileId` although the repository
  does not publish events by default.
- **`TagUsage.Tables` includes `Tv_Series`**, so `CleanupUnusedTags` keeps tags that only a series
  uses. Tag detail (`TagService`) does not list series until the Tv services exist.
- **Shared history filter SQL** (`ActivityFilterSql`): the language and quality `LIKE` filters of
  the History page moved out of the Movies repository and the Tv repository into one helper.
  This fixes a bug the Tv tests found on PostgreSQL: the language filter used `CHAR(13)`, which
  PostgreSQL rejects (`CHR(13)`), so the Movies History page failed whenever a language filter was
  set on a PostgreSQL database. The helper chooses the function by database type and is tested on
  both.

**Visible effect.** The download client schema gains a "Category (Tv)" field next to the movie
category, and the existing field is labelled "Category (Movies)". The core builds one category
field per registered module. A Tv download client category has no effect until the Tv grab step
(step 4) reads it. The two Tv priority help texts were missing from `en.json` (the UI shows the raw
key for a missing one); they are added, and `ModuleLocalizationFixture` fails when a registered
module lacks them.

**Gaps left on purpose.** No Tv service calls a metadata source, parser, indexer or the disk. The
`IMediaModule` comment still says `Name` is the table prefix; it is the prefix without the
underscore. The Tv types repeat the simple names of Movies types (`Ratings`, `MonitorTypes`,
`MonitoringOptions`, `NamingConfig`, `RootFolder`, `Blocklist` and others) in separate
namespaces. Nothing keyed by type name collides yet; the OpenAPI schema ids will at step 7, so the
Tv API resources need distinct names.

**Tests.** `TvModuleTablesFixture` (migration test with old-shape History and Blocklist rows),
the repository fixtures under `NzbDrone.Core.Test/TvTests`, and `ActivityFilterSqlFixture` run on
SQLite and PostgreSQL. Each was checked by breaking the code under test and seeing the test fail.

### TV step 2: as built

Built in session 37 against Sonarr `b3d4bb8`. No migration, no table change. Calls made while
building (each is reversible):

- **Ported into `NzbDrone.Core.Tv.Parser`**: `Parser` (`Tv.Parser.Parser`), `QualityParser`,
  `LanguageParser`, `ReleaseGroupParser`, `ParserCommon`, `ReleaseComparer`, `SceneChecker`,
  `ValidateParsedEpisodeInfo`, `InvalidSeasonException`, and in `Tv.Parser.Model`
  `ParsedEpisodeInfo`, `SeriesTitleInfo`, `SubtitleTitleInfo`, `ReleaseComparerModel`,
  `FindSeriesResult` (`ReleaseType` came with step 1). Quality and language output is the global
  catalog; the ID maps (`SonarrQualityIds`, `SonarrLanguageIds`) already existed from the catalog work.
- **`ReleaseGroupParser` and `ParserCommon` are Tv copies, not merged into core.** The core versions
  are Radarr's and differ in content, not just names: Sonarr's release group regex handles HDTV and
  non-ASCII groups and its cleanup strips an `S01E01` prefix, Radarr's handles `tmdbid`/`imdbid`
  tags; the exception lists and their precedence differ; Sonarr's `PreSubstitutionRegex` (Chinese
  and Korean release rewrites) and `PathPreSubstitutionRegex` are empty in Radarr. A union would
  change Movies parsing. Merging is optional later work.
- **`MediaFileExtensions` and `FileExtensions` are Tv copies and must NOT move to core at step 5.**
  The s36 note listed `MediaFileExtensions` among the files identical in both apps. They are not:
  Radarr maps `.mkv` to WEBDL-720p, adds `.mk3d`, and maps `.ts` and `.wtv` to SDTV; Sonarr maps
  `.mkv`, `.ts` and `.wtv` to HDTV-720p. `FileExtensions.RemoveFileExtension` reads the matching
  `MediaFileExtensions`, so it follows. The extension guess feeds the parser (the Raw-HD branch of
  `QualityParser`). Step 5 moves only files that diff clean.
- **Sonarr's source vocabulary stays inside the TV module.** The catalog uses Radarr's (source plus
  modifier). Sonarr's `QualityParser` reasons in `Television`, `TelevisionRaw`, `Web`, `WebRip`,
  `DVD`, `Bluray`, `BlurayRaw` and compares them with `>=`, so `TvQualityFinder` holds Sonarr's 22
  qualities as a table of (catalog quality, Sonarr source, resolution) and runs Sonarr's
  `QualityFinder` over it. A test requires the table to hold exactly the IDs of `SonarrQualityIds`,
  so the TV parser cannot return a quality Sonarr never had. Remuxes are `BlurayRaw`, so a 720p
  remux finds nothing and returns Unknown, as in Sonarr.
- **Two deliberate differences from Sonarr's parser tests.** (1) `RemuxRegex` accepts a leading `[`
  (Radarr's regex already does). The catalog names are `Remux-1080p` and `Remux-2160p`, which the
  file namer writes; Sonarr's regex only knew `Bluray-1080p Remux`, so a renamed remux re-parsed as
  HDTV-1080p. `parsing_our_own_quality_enum_name` caught it. (2) `Ríkið` cleans to `rikið`, not
  `rikid`: Sonarr's test expects the second with the same Diacritical.Net 1.0.5 and the same code,
  but the library does not transliterate the eth (checked in isolation). The cause of the upstream
  difference is not known. The clean title is built the same way on both sides of a match, so the
  matching is consistent; the test records the observed value.
- **Scene mappings ported** to `Tv.DataAugmentation.Scene`: `SceneMappingService`,
  `ISceneMappingProvider`, `ServicesProvider`, `SceneMappingProxy`, `UpdateSceneMappingCommand`,
  `SceneMappingsUpdatedEvent`, `InvalidSceneMappingException`. The entity, repository and
  `SearchMode` came with step 1. `ISonarrCloudRequestBuilder.Services` (`NzbDrone.Common.Cloud`)
  points at `https://services.sonarr.tv/v1/`; it has no metadata endpoint yet (step 3).
- **Visible effect.** `TvModule` now schedules the scene mapping update every 3 hours (Sonarr's
  interval), so a Plattarr instance fetches the mapping list from the Sonarr project's data service
  and fills `Tv_SceneMappings`. Nothing reads the table until step 4. This is the plan's decision to
  use Sonarr's data services for TV; the task can be removed from `TvModule.ScheduledTasks`.
- **Events added without a publisher.** `SeriesRefreshStartingEvent`, `SeriesAddedEvent` and
  `SeriesImportedEvent` (`Tv.Events`) exist because `SceneMappingService` handles them. Until
  step 3 publishes them the scheduled task is the only trigger.
- **Deferred on purpose.** `ParsingService` (needs the series and episode services, step 3),
  `LocalEpisode` (import, step 5), `RemoteEpisode` and `GrabbedReleaseInfo` (decisions, step 4),
  `ImportListItemInfo` (import lists), and `XemService`, `XemProxy` and `DailySeries*` (they read
  and write series and episodes; step 3).

**Tests.** All of Sonarr's `ParserTests` except `IsoLanguagesFixture` (core code shared with Movies,
which has its own fixture) and `ParsingServiceTests` (deferred) run under
`NzbDrone.Core.Test/TvTests/ParserTests`: 1,670 cases, plus `SceneMappingServiceFixture`,
`SceneMappingProxyFixture` (integration, live service), `TvQualityFinderFixture` and
`TvModuleFixture`. Mutation checks, each killed: removing the `[` from `RemuxRegex`, removing the
sub-720p rule in `TvQualityFinder`, changing the task interval.

### TV step 3: as built

Built in session 38 against Sonarr `b3d4bb8`. No migration, no table change. Calls made while
building (each is reversible):

- **Ported into `NzbDrone.Core.Tv`**: `SeriesService`, `EpisodeService`, `AddSeriesService` and
  `AddSeriesValidator`, `RefreshSeriesService`, `RefreshEpisodeService`, `EpisodeRefreshedService`,
  `EpisodeMonitoredService`, `EpisodeCutoffService`, `SeriesEditedService`, `MoveSeriesService`,
  `SeriesPathBuilder`, `SeriesAddedHandler`, `SeriesScannedHandler`, `SeriesTitleNormalizer`,
  `SeriesTitleSlugValidator`, `SeriesTranslationService`, `ShouldRefreshSeries`, the commands
  (`RefreshSeriesCommand`, `MoveSeriesCommand`, `BulkMoveSeriesCommand`), the events, the
  validators and `RootFolderService`. `MetadataSource` holds `IProvideSeriesInfo`,
  `ISearchForNewSeries`, `SearchSeriesComparer` and `SkyHook` (`SkyHookProxy` and its resources).
  `DataAugmentation` gained `Xem` and `DailySeries`. `SeriesStats` holds the season statistics
  query. `Parser/ParsingService` came with its tests.
- **`ISonarrCloudRequestBuilder.SkyHookTvdb`** points at `https://skyhook.sonarr.tv/v2/tvdb/{route}/`.
  Metadata for TV therefore comes from the Sonarr project's service (the plan's decision, as for
  scene mappings). Series information is requested by three-letter language code, TVDB ID and
  season type.
- **Season statistics are a query, not a table.** `SeriesStatisticsRepository` aggregates
  `Tv_Episodes` and `Tv_EpisodeFiles`; the raw SQL names the prefixed tables, and runs on SQLite and
  PostgreSQL (both runs are in the test notes below). The airing columns come back as local-kind
  dates because Sonarr parses them with `AssumeUniversal`; compare instants, not ticks.
- **`RemoteEpisode` and `TvSearchRequest` now exist.** `RemoteEpisode` derives from `RemoteRelease`
  (kind `Tv`, parent `Series`, item IDs the episode IDs, `Year` and `Studio` from the series).
  `TvSearchRequest` derives from `ModuleSearchRequest` and replaces Sonarr's `SearchCriteriaBase`
  in `ParsingService`. The search commands (`EpisodeSearchCommand`, `SeriesSearchCommand`,
  `MissingEpisodeSearchCommand`, `CutoffUnmetEpisodeSearchCommand`, namespace `Tv.Search`) are data
  only; their executors belong to step 4.
- **Auto-tagging is not ported.** `SeriesService` and `RefreshSeriesService` lose Sonarr's
  `IAutoTaggingService` calls; `UpdateTags(List<Series>)` stays. Auto-tagging stays a
  Movies feature until someone wires TV specifications into it; nothing in TV calls it.
- **Interim pieces so the metadata services compile and resolve.** `DiskScanService` only checks
  the root and series folders, creates empty folders when asked, sets permissions and publishes
  `SeriesScannedEvent` (the real scan is step 5). `FileNameBuilder` holds the series, season and
  folder naming only (the episode file name tokens are step 5), with `NamingConfigService`.
  Media covers are not ported; `Series.Images` holds the remote URLs.
- **Language default.** `Tv_Series.Language` is NOT NULL (migration 004 is shipped and is not
  edited), and Sonarr's constructor left it unset. `Series` now defaults `Language` to English and
  `ApplyChanges` keeps the old value when the new one is null.
- **Config additions.** `IConfigService.AutoUnmonitorPreviouslyDownloadedEpisodes` and
  `CreateEmptySeriesFolders` (both default false), and `CommandResult.Indeterminate` (a refresh
  that skipped a series TheTVDB no longer has is not a full success). New helpers:
  `NullableExtensions.NonNegative`, `SeriesNotFoundException`.
- **Events published.** `SeriesService.AddSeries` publishes `SeriesAddedEvent`, `AddSeries(list)`
  publishes `SeriesImportedEvent`, and `RefreshSeriesService.Execute` publishes
  `SeriesRefreshStartingEvent` (manual flag from the command trigger). Tested with mutation checks.
- **Scheduled tasks.** `TvModule.ScheduledTasks` now has `UpdateSceneMappingCommand` (3 h) and
  `RefreshSeriesCommand` (12 h). **Visible effect:** `Refresh Series` appears in `system/task`.
  With no series it makes no outbound call; XEM and scene mapping refreshes are triggered only
  by a manual refresh or by adding or updating a series. There is no scheduled daily-series task: Sonarr loads
  that list on demand and caches it for an hour.

**Tests.** Sonarr's fixtures for the ported classes run under `NzbDrone.Core.Test/TvTests`
(series, episode, refresh, add, move, monitored, organizer, `ParsingService`, XEM, daily series). New: `SkyHookProxyFixture` (30 cases against a recorded response in
`Files/SkyHook/tvdb-121361-official.json`; no network), `SeriesStatisticsFixture` (4),
`AddSeriesEventsFixture` and `RefreshSeriesStartingEventFixture` (event publishing).
`CoreTest.UseRealHttp` now also sets the Sonarr cloud request builder. Mutation checks, each
killed: dropping the `Monitored` filter from the next and from the previous airing column,
hard-coding the SkyHook language code, monitoring special seasons, and deleting each of the three
event publishers. Two checks first survived (the unmonitored episode in the airing test was
further from now than the monitored one) and the test was changed to make them fail.

**Live proof (dev stack, image `80d734b`).** Migration 006 ran first on a copy of the config
(version 6, the four tables with the expected columns, clean logs), then live. With Game of Thrones
seeded (there is no TV API yet) and a qBittorrent client whose `tvCategory` is `plattarr-tv`, a
CC-licensed torrent renamed to `Game.of.Thrones.S01E01.1080p.WEB-DL.x264-BLENDER` completed in that
category with no grab history. The next `RefreshMonitoredDownloads` matched it by title
(`TvTrackedDownloadMatcher`), `TvDownloadImporter.Check` returned `Ready` from the category alone,
and `ProcessPath` imported it: a hardlink (same inode, link count 2) named
`Season 1/Game of Thrones (2011) - S01E01 - Winter Is Coming WEBDL-480p.mp4` (480p comes from the
file's real resolution, so the ffprobe read ran), a `Tv_EpisodeFiles` row with scene name, group and
original path, the episode linked, and a shared `DownloadHistory` row with `MediaKind` 2 from
`DownloadCompletedEvent`. No Warn or Error beyond the two expected first-refresh lines. `RssSync`
completed and the movie search is unchanged (70 releases, 56 approved). Not exercised: the episode
`History` table stays empty (step 6 owns those handlers), no queue API for TV (step 7), no TV
notification (no builders yet), and the sidecar subtitles were not imported (`ImportExtraFiles` is
off by default). Everything seeded was deleted afterwards.

**Lesson, CI.** `Fast` builds use `RunAnalyzers=false`, and an incremental `Radarr.sln` build can
skip a test project whose last edit followed it. CI failed on two analyzer errors in a test file
edited after the last full build (SA1117 and IDE0005). Before every push run
`dotnet build Radarr.sln -c Release --no-restore --no-incremental` (about 3 min).

Results (Release, full solution builds with 0 warnings): Core 6,688 passed and 21 skipped, Common
656, Host 29, Api 38, 0 failed; Core `DbTest` on PostgreSQL 16: 432 passed, 0 failed.

### TV step 4: as built

Built in session 39 against Sonarr `b3d4bb8`. One migration (005), no table change. Calls made
while building (each is reversible):

- **Custom formats are neutral (4a).** `CustomFormatInput` carries what a specification may read
  (release title, group, quality, edition, year, original language, release type, size, indexer
  flags, languages, filename). Each module fills it from its own parse result and parent, so no
  specification references a module type; the module-boundary allowlist shrank to 18 entries.
  `CustomFormatCalculationService.ParseCustomFormat(CustomFormatInput, MediaKind)` is the one entry
  point. `Tv.CustomFormats.TvCustomFormatCalculationService` builds the input from a `RemoteEpisode`,
  an episode file, a blocklist row or a history row.
- **`ReleaseType` lives in the TV module.** The Release Type specification reads the neutral input's
  `ReleaseType` (0 unknown, 1 single episode, 2 multi episode, 3 season pack). Movies and Music
  inputs leave it 0.
- **Rejection reasons are appended.** `DownloadRejectionReason` gained the TV reasons at the end, so
  existing numeric values are unchanged.
- **`UpgradableSpecification` and `UpgradeableRejectReason` moved to `NzbDrone.Core.DecisionEngine`.**
  Both modules use the core types.
- **`IConfigService.AnimeStandardFormatSearch`** (default false) replaces Sonarr's per-indexer
  setting of the same name. Anime categories are not carried: the search translator uses the
  categories the indexer category service returns for TV.
- **Search criteria carry every identifier (4a).** `TvSearchTranslator` sends all IDs it has
  (TVDB, TVMaze, IMDb, TMDB, TVRage) instead of Sonarr's per-indexer choice; Prowlarr's indexer layer
  drops what an indexer does not support. Daily series are searched by year (a season request) or by year plus `MM/dd` (an episode request), as Sonarr does.
- **Matching and aggregation (4b).** `TvReleaseMatcher` is Sonarr's title parse and series lookup
  moved out of `DownloadDecisionMaker` (contract 2). `RemoteEpisodeAggregationService` and
  `AggregateLanguages` are Sonarr's; an aggregator that throws is logged and skipped.
- **Pending releases are ported on the shared `PendingReleases` table (4b).** `PendingReleaseService`
  keeps Sonarr's static cache. Two deviations: the grouping key for pending releases is a string of
  the ordered episode IDs (Sonarr's `GroupBy` over an `IEnumerable` never grouped, because it
  compared references), and there is no `QualityProfileUpdated` handler or obsolete queue method.
  The queue ID is a hash of `pending-{id}`.
- **History and blocklist (4b).** `Tv.History.HistoryService` and `Tv.Blocklisting.BlocklistService`
  are read services plus `Block(RemoteEpisode, message)`, which sets `MediaKind`, `ParentId`,
  `ItemIds`, `ReleaseType` (a deviation from Radarr, which has no such column) and the release
  fields. The event handlers that write history and blocklist rows are step 6.
- **Specifications are Sonarr's (4c).** All 29 decision specifications live in
  `Tv.DecisionEngine.Specifications` (root, `RssSync`, `Search`) behind the module's own
  `IDownloadDecisionEngineSpecification`, and run in one priority order with the core release
  specifications. `DownloadDecisionMaker` rejects a release with `UnknownSeries`,
  `MatchesAnotherSeries` (the title is a scene alias of another series) or `UnknownEpisode` before
  any specification runs. `DownloadDecisionComparer` is Sonarr's order (quality, custom format
  score, protocol, episode count, episode number, indexer priority, peers, age, size) and has no
  indexer-flag comparison. The module's `IDownloadDecisionPrioritizer` has `Kind` `Tv`.
- **Size limits come from the shared quality definitions.** Sonarr keeps minimum, maximum and
  preferred size on the profile's items. Plattarr keeps them on `QualityDefinition`, in MB per
  minute, so `AcceptableSizeSpecification` and the comparer read `IQualityDefinitionService`. A
  profile can no longer set a size limit for one series only.
- **TV-only settings (migration 005).** Columns with defaults that keep today's behavior:
  `ReleaseProfiles.AirDateRestriction`, `AirDateGracePeriod` and
  `AllowSeasonPackWithoutAllEpisodesAired`, and `IndexerOverrides.SeasonSearchMaximumSingleEpisodeAge`
  (a TV row, 0 is off). Sonarr stamps the indexer setting on each release; the season pack
  specification reads the indexer's TV override instead. The season pack upgrade settings are
  global config keys, not overrides: `SeasonPackUpgrade` (`All`, `Threshold` or `Any`, default `All`)
  and `SeasonPackUpgradeThreshold` (default 50). The reference tree has no definition of either, so
  the enum and defaults are ours; `All` matches the behavior before Sonarr had the setting.
- **Search services are Sonarr's (4d).** `NzbDrone.Core.Tv.Search` now holds `ReleaseSearchService`
  (`ISearchForReleases`: episode, season and interactive search, scene and XEM mappings, daily,
  anime and special routing), `EpisodeSearchService` (`EpisodeSearchCommand`,
  `MissingEpisodeSearchCommand`, `CutoffUnmetEpisodeSearchCommand`), `SeriesSearchService`,
  `SeasonSearchService` with the new `SeasonSearchCommand` (Sonarr's, copied unchanged), and
  `TvRssReleaseHandler` (`IRssReleaseHandler`, `Kind` `Tv`). Nothing schedules or routes these
  commands yet; that is step 7.
- **A search goes through the shared fetcher.** `ReleaseSearchService.Dispatch` calls
  `IIndexerReleaseFetcher.Search(request, series.Tags, MediaKind.Tv, translator)`. The fetcher
  filters indexers by the series' tags, so Sonarr's four indexer-tag search tests became one test
  that the tags reach the fetcher. `LastSearchTime` is set, and saved, only when
  `IndexersSearched` is above 0.
- **RSS handling.** `TvRssReleaseHandler` is the Movies handler with TV types: it adds the
  pending releases to the fetched ones, makes the decisions, processes them and publishes the TV
  module's own `RssSyncCompleteEvent`. The history and blocklist event handlers that Sonarr hangs
  on RSS and grab events are step 6.
- **`EventAggregator` caches subscribers per event type, not per type name.** The live proof
  found it: the TV and Movies modules each have a `RssSyncCompleteEvent`, the cache was keyed by
  `Type.Name`, so the second module's publish got the first one's subscribers, cast them to `null`
  and threw a `NullReferenceException` that aborted `RssSync` after the TV handler ran. The key is
  now `typeof(TEvent)`. Unit tests could not see it because no fixture published two events with
  one name; `should_publish_events_that_share_a_type_name_to_their_own_handlers` does, and fails
  with the same exception under the old key. Any future module event may reuse a name.
- **A kept Sonarr quirk.** The global scene mapping sets `AbsoluteEpisodeNumber` from
  `SceneSeasonNumber` (`ReleaseSearchService.cs:330` in Sonarr `b3d4bb8`). It is ported as is, to
  stay comparable; fix it only together with a test against a real anime search.

**Tests.** Sonarr's decision and search fixtures run under `TvTests/DecisionEngineTests` (347
tests) and `TvTests/SearchTests` (108, with the 4a and 4b translator and matcher fixtures), adapted to the module seams (neutral
custom format input, quality definitions for sizes, `IIndexerOverrideService` for the
single-episode age, `IIndexerReleaseFetcher` for searches). New fixtures: decision maker ordering
and reasons, prioritizer, comparer, migration 005, `ReleaseProfile` TV settings, the config and
override defaults, `TvEpisodeSearchServiceFixture`, `TvSeasonSearchServiceFixture`,
`TvRssReleaseHandlerFixture`, `TvReleaseSearchBranchesFixture` (daily, special and anime
routing, monitored filtering, scene mapping season filters, de-duplication), and two Host
container tests that resolve the TV contracts and command executors.

**Mutation checks.** 4c: 50 mutations of the maker, comparer, prioritizer, size and season pack
specifications; 48 killed. The 2 survivors are equivalent: the `DownloadProtocol != Torrent ||`
and `!= Usenet ||` guards in the comparer cannot change a result, because the protocol comparison
runs earlier. 4d: 48 mutations of the search services and the RSS handler; 16 first survived, in
`ReleaseSearchService` (daily air date, special and anime routing, monitored filters, season
scoped mappings, scene title fallback, de-duplication order, absolute number). 18 tests were
added, and a re-run killed 15. The last, an explicit scene mapping with the series' own title no
longer suppressing the implied one, is equivalent: both mappings merge into one request with the
same title.

Results (Release, full solution builds with 0 warnings): Core 7,255 passed and 21 skipped, Common
656, Host 31, Api 38, 0 failed; Core `DbTest` on PostgreSQL 16: 444 passed, 0 failed.


### TV step 5: as built

Built in session 40 against Sonarr `b3d4bb8`. One migration (006), four tables. Calls made while
building (each is reversible):

- **Shared helpers moved to the core.** Both modules need the recycle bin and the cover plumbing, so
  these moved out of `Movies` with `git mv` and kept their code: `RecycleBinProvider` (and its
  exception), `MediaFileAttributeService`, `RootFolderNotFoundException`, `CleanUpRecycleBinCommand`
  (`NzbDrone.Core.MediaFiles`), `ImageResizer`, `MediaCoverProxy` and
  `CoverAlreadyExistsSpecification` (`NzbDrone.Core.MediaCover`). The recycle bin cleanup is still
  scheduled by the movie module; a TV-only install would not run it. Move the schedule to the core
  when the core owns scheduled tasks (owed).
- **Contract 5 grew two members, both for Sonarr behavior.** `IImportResult.BlocksAutomaticImport`
  (TV: a multi-season release; movies: never) and `IDownloadImporter.BlocksImportAfterRejection` (TV:
  true, Sonarr's rule that one rejected result moves the download to `ImportBlocked`; movies:
  false, Radarr's). `CompletedDownloadService` reads both; nothing else in the core knows either
  module's rejection reasons. `FailDownloads` gained `UserDefinedExtensions` (value 2), mapped from
  the TV `UserRejectedExtension` reason through `IImportResult.FailsDownloadAs`.
- **The TV importer is Sonarr's `CompletedDownloadService` split at the seam.** `Tv.Import`:
  `TvDownloadImporter` (`Check`: grab history, then the title, then the series on the grab; a match
  made by ID outside an interactive search blocks, as Sonarr; `Import`: `ProcessPath` with the
  matched series; `IsImported`: imported episode count against the release, then the history check)
  and `TrackedDownloadAlreadyImported`. `Tv.Downloads`: `TvTrackedDownloadMatcher` (the series half
  of Sonarr's `TrackedDownloadService.TrackDownload`, same order: title, then history, then the
  special-episode parse) and `TvTrackedDownloadRefresher` (the library event handlers that
  Sonarr had inside the tracked download service). `ManualInteractionRequiredEvent` carries a new
  `Tv.Notifications.TvNotificationData` (series, remote episode, grabbed release); the TV
  notification builders are step 6.
- **Sonarr's import code is ported, not rewritten.** `Tv.MediaFiles`: `DiskScanService`,
  `DownloadedEpisodesImportService`, `DownloadedEpisodesCommandService`, `EpisodeFileMovingService`,
  `MediaFileService`, `MediaFileDeletionService`, `MediaFileTableCleanupService`,
  `RenameEpisodeFileService`, `UpdateEpisodeFileService`, `UpgradeMediaFileService`, the script
  import decider, and `EpisodeImport` (decision maker, 16 specifications, aggregators and
  augmenters, `ImportApprovedEpisodes`, sample detection, `RejectedFileExtensionService`).
  `LocalEpisode` is in `Tv.Parser.Model`. `Tv.Organizer.FileNameBuilder` is Sonarr's with all
  episode tokens, multi-episode styles and the naming validators; `Tv.Extras` has the extra file
  service, subtitles, other extras and the Kodi/XBMC consumer.
- **Moves are limited to files that diff clean.** Sonarr's `EpisodeFileMovingService` and
  `UpdateEpisodeFileService` are unchanged apart from namespaces and the config keys below.
- **Config keys.** `EpisodeTitleRequired` (`Always`, `BulkSeasonReleases`, `Never`; default
  `Always`), `UserRejectedExtensions` (default empty) and `EpisodeFileDate` (`None`, `LocalAirDate`,
  `UtcAirDate`; default `None`) are new `IConfigService` members. The movie module's `FileDate`
  keeps its own enum, so TV did not reuse the key.
- **Command names stay unique.** The command queue and the API look a command up by its type name
  without `Command`, so a second `RenameFilesCommand` would resolve to the movie one. TV's is
  `RenameEpisodeFilesCommand`. `CommandNamesFixture` fails the build if any two commands share a name.
- **Media info is not unified.** The plan said to unify `MediaInfo`; the two modules store it as an
  embedded document on different file tables and the TV copy reads Sonarr's fields, so each
  module keeps its `MediaInfoModel`, `VideoFileInfoReader` and `UpdateMediaInfoService`. The cost is
  two copies of the ffprobe call. Unify when a third module needs it or when a fix has to be made
  twice.
- **Quality vocabulary.** Sonarr's import code reads a source and resolution from a `Quality`;
  Plattarr's shared `Quality` has neither, so the import code uses `TvQualityFinder` and
  `TvQualitySource` (step 4's vocabulary) and `TvQualityFinder.ResolutionOf`.
- **Custom formats on a file use the neutral input.** `TvCustomFormatCalculationService` gained
  `ParseCustomFormat(LocalEpisode, fileName)` (release title is the scene name or the file's name,
  plus group, quality, size, languages, flags, release type).
- **Migration 006 (`tv_extras_tables`).** `Tv_Metadata` (consumer settings), `Tv_MetadataFiles`,
  `Tv_ExtraFiles` and `Tv_SubtitleFiles`, with Sonarr's property names and `SeriesId` where Movies
  has `MovieId`. New tables only; no row changes.
- **Not ported, on purpose.**
  - Manual import (`ManualImport*`): it is an API feature, so it arrives with step 7.
  - The WDTV and Roksbox metadata consumers and `TorrentFileInfoReader`: the movie module has
    neither, and nothing in the import path uses them.
  - Sonarr's "download client reports a rejected file" path in `CompletedDownloadService`
    (`ProcessRejectedDownloadClientFiles`, which needs `IDownloadClient.GetFiles`). A download
    whose only file is rejected by extension and which the folder scan does not see ends as "No files
    found are eligible for import", not as a failed download. Port it with the client interface
    change when a client supports it (owed).
- **A bug I made and caught.** A bulk unused-using clean-up applied build-log line numbers after
  `dotnet format` had already shifted them and deleted 116 needed `using` lines in 149 files. The
  next compile showed 259 errors, all missing namespaces; restoring each file's header from Git
  and the Sonarr original and re-running the formatter fixed it. Rule: a script that edits by
  reported line number must run on a log produced by the files as they are, never after another
  tool touched them.

**Tests.** Sonarr's fixtures run under `TvTests` for `MediaFiles` (disk scan, import services,
decision maker, specifications, aggregation, moving, renaming, deletion, upgrade, media info),
`OrganizerTests` (file name builder and folders), `Extras` and `Download`. New fixtures:
`TvDownloadImporterFixture` (check, import, imported judgment, blocked notification),
`TvImportResultContractFixture`, `TvTrackedDownloadMatcherFixture` (Sonarr's tracked download
tests plus ambiguity, failure and rematch), `TvTrackedDownloadRefresherFixture`,
`TvExtraFileRepositoryFixture` (a round trip through each new table), migration 006,
`CommandNamesFixture`, and four Host container tests that resolve the TV importer, matcher,
media file services and command executors.

**Mutation checks.** Seven mutations of the importer, refresher, result mapping, matcher and table
map; the first run left one alive (the matcher's series lookup for an already imported download:
the test mocked `Map` for any series). The test now requires the series with the history's
parent ID and the mutation is killed.

Results (Release, full solution builds with 0 warnings): Core 8,165 passed and 29 skipped, Common
656, Host 35, Api 38, 0 failed; Core `DbTest` on PostgreSQL 16: 458 passed, 1 skipped, 0 failed (`DbTest` and `DbMigrationTest`).


### TV step 6: as built

Built in session 41 against Sonarr `b3d4bb8`. No migration: the shared activity tables already had the
TV columns (migration 004). Calls made while building (each is reversible):

- **Core events carry the covered items.** `DownloadFailedEvent` and `DownloadIgnoredEvent` gained
  `ItemIds` (the movie itself, the episodes or the tracks), so a release that covered several
  episodes is one failure that names them all. `MovieFailedDownloadSource` sets `[movieId]`.
  Module handlers still start with a `Kind` guard, because the core publishes both events for every
  module.
- **TV history is Sonarr's, one row per episode.** `Tv.History.HistoryService` handles grab, import,
  failure, ignore, file delete, file rename and series delete, with `ParentId` the series and `ItemId`
  the episode. `Size` is written with `GetValueOrDefault()`: Sonarr's `Size.ToString()` writes a
  blank for a release with no size and the blocklist's `long.Parse` would throw on it.
- **Blocklist.** `Tv.Blocklisting.BlocklistService` now handles `DownloadFailedEvent` (TV only; it
  reads the published date, size, protocol, flags and release type from the failure's data) and
  `SeriesDeletedEvent`. The clear-blocklist command stays with Movies: it is a single-handler
  command and the TV API does not exist yet (step 7).
- **Failed downloads and redownload.** `Tv.Downloads.TvFailedDownloadSource` is Sonarr's
  `FailedDownloadService` at the seam the core leaves, including the rule that a skip-redownload
  request only counts for a history row with no download ID. `TvRedownloadSearcher` is Sonarr's
  `RedownloadFailedDownloadService`: one episode searches again, a failure that covers the whole
  season runs a season search, anything else an episode search for the episodes it covered.
- **Notifications.** `Tv.Notifications`: `TvNotificationData` (series, remote episode, local episode,
  episode file, old and renamed files, grabbed release), `TvActivityRenderer`,
  `TvNotificationHandler` (grab, import, series added, renamed, file deleted, series deleted, manual
  interaction), `TvWebhookPayloadBuilder` and `TvCustomScriptEnvironmentBuilder`. The message text is
  Sonarr's ("Series - 1x01x02 - Titles [WEBDL-1080p Proper]", " vN" for anime, the air date for a
  daily series).
- **Sonarr's payloads and variable names are kept.** The 16 webhook payload classes are copies of
  Sonarr's (`WebhookGrabPayload`, `WebhookImportPayload`, `WebhookSeriesAddPayload`, and so on) in
  `NzbDrone.Core.Tv.Notifications`, so existing receivers keep working. Scripts get `Sonarr_Series_*`,
  `Sonarr_EpisodeFile_*`, `Sonarr_Release_*` and `Sonarr_Download_*`.
- **Contract 7 grew these members for this.** `ICustomScriptEnvironmentBuilder.VariablePrefix` and
  `EventType(NotificationEvent)`: the core adds `{prefix}_EventType`, `_InstanceName` and
  `_ApplicationUrl`, and a series sends `SeriesAdd`, `EpisodeFileDelete` and `SeriesDelete` where a
  movie sends `MovieAdded`, `MovieFileDelete` and `MovieDelete`. `ActivitySubject.ItemKindLabel` /
  `ItemNoun` ("Episode Grabbed" against "Movie Grabbed"), `MetadataUrl` (the page the title links
  to: TMDb for a movie, TVDb for a series) and `OmitYearFromTitle` (Sonarr's Discord title is
  "Series - 1x01 - Title", with no year). `MetadataLinkType` gained `Tvdb` and `Tvmaze`,
  `WebhookEventType` gained `SeriesAdd`, `SeriesDelete` and `EpisodeFileDelete`, and the Emby and
  Jellyfin lookup also matches a series by its TVDb or TVMaze ID.
- **Movies output is byte-identical.** The Movie golden files for Webhook, CustomScript and Discord
  did not change; `GOLDEN_OUT` regeneration diffs clean.
- **Orphan cleanup.** `Tv.Housekeepers.CleanupOrphanedTvActivity` deletes TV rows of `History`,
  `Blocklist` and `PendingReleases` whose series is gone, and history rows whose episode is gone.
  The movie housekeepers still touch only `MediaKind.Movies` rows.
- **Not built, on purpose.**
  - Sonarr's `OnImportComplete` (fires when the whole release has imported): it needs a column on the
    notification definition, a migration, and the API and UI to set it. It arrives with step 7.
  - A TV clear-blocklist command (step 7, see above).
  - Per-media-type notification toggles. A provider enabled for Grab sends for movies and series both;
    tags already narrow it per series or movie.
  - Discord's rename embed and "Links" field are the shared Radarr-style code, not Sonarr's text.
    The shared notification providers are not reopened.

**Tests.** New fixtures: `HistoryEventsFixture` (a row per episode, the size fallback, kind guards, the
file-delete reasons that write nothing), `TvFailedDownloadSourceFixture`,
`TvRedownloadSearcherFixture`, blocklist handler tests added to `BlocklistServiceFixture`,
`CleanupOrphanedTvActivityFixture`, `TvActivityRendererFixture`, `TvNotificationHandlerFixture`
(including the message format by series type and revision), three TV golden fixtures
(`tv-webhook.json`, `tv-customscript.txt`, `tv-discord.json`, reviewed by hand against Sonarr's
shapes), and three Host container tests.

**Mutation checks.** Five mutations (failure guard in history, the season branch of the searcher, the
grab guard in the notification handler, the episode orphan query, the guard in the blocklist
handler); all five were killed by the new tests.

**Live proof (dev stack on `5b46067`, 2026-10-23).** Game of Thrones (TVDB 121361, 128 episodes) was
seeded by SQL as in step 5. A real GoT release would have meant downloading copyrighted content, so a
small fake Torznab indexer (a Python script on the test host) served two CC-licensed torrents (Sintel,
Cosmos Laundromat) under GoT S01E01 titles. Knaben was disabled while the TV run was in progress and
re-enabled afterwards. The throwaway qBittorrent client used `tvCategory` `plattarr-tv`, and a Webhook
listener received every event.

- **Grab.** `EpisodeSearch` for S01E01 searched the fake indexer, grabbed
  `Game.of.Thrones.S01E01.1080p.WEB-DL.x264-BLENDER` and wrote one `History` row
  (`MediaKind` 2, `Grabbed`, `ItemId` = the episode, `DownloadId` = the torrent hash, camelCase `Data`
  keys including `torrentInfoHash`, `releaseSource`, `indexerId`). The `Grab` webhook arrived in
  Sonarr's shape (`series`, `episodes`, `release`, `downloadClient`, `downloadId`, `customFormatInfo`).
- **Blocked import.** The torrent was named `Sintel`, which does not parse. The queue item went to
  `importBlocked` ("Unable to parse file") and a `ManualInteractionRequired` webhook fired with
  `downloadStatusMessages`.
- **Failed download, blocklist, redownload.** `DELETE /queue/{id}?removeFromClient=true&blocklist=true`
  wrote a `downloadFailed` history row and one `Blocklist` row (`MediaKind` 2, `ItemIds` `[440]`,
  `TorrentInfoHash`, indexer). The redownload `EpisodeSearch` ran by itself and grabbed the other
  release (`...720p...COSMOS`), skipping the blocklisted one. The failed torrent was removed from the
  client.
- **Import.** After renaming the second torrent to its release title, the import hard-linked the file
  (same inode, link count 2), wrote the `Tv_EpisodeFiles` row, a `downloadFolderImported` history row
  and a `DownloadHistory` row, and fired a `Download` webhook with `episodeFile` (media info, scene
  name, source path), `isUpgrade` and `release`. The file's real width (1920) gave `WEBDL-1080p` for a
  "720p" title (the media-info rule from step 5).
- **Housekeeping.** After the series was deleted by SQL, `POST /command {"name":"Housekeeping"}`
  removed the 4 orphaned Tv history rows and 1 Tv blocklist row.
- **Regression.** `RssSync` completed (9 reports, 0 grabbed) with both the TV and Movies handlers
  running. `GET /release?movieId=1` still returns 70 releases, 56 approved. Movies history was empty
  before and after.
- **Logs.** Warn, Error and Fatal lines were all caused by the test setup: a bad `SeasonTypes` seed
  value (`{}`; it must be `[]`) that made `SeriesService` fail once at startup, the webhook created
  before its listener was up (the connection test failed with a 500), and the two expected
  first-refresh warnings. No new error from the step 6 code.
- **Found while proving.**
  - `GET /queue` hides items that have no movie unless `includeUnknownMovieItems=true`, so a TV item
    is invisible in the queue API until step 7. `DELETE /queue/{id}` already works for TV items.
  - The webhook `Test` event still sends the movie-shaped sample payload.
  - The webhook `instanceName` is "Radarr" (`BuildInfo.AppName`, kept on purpose until a
    compatibility API exists).
- **Not exercised live:** file delete, rename, series add and series delete events (no TV API to
  trigger them; covered by unit and golden tests), a client-reported failure (`FailedDownloadService.Check`),
  and `OnImportComplete` (not built).
- **Cleaned up:** client, webhook, fake indexer, torrents (with files), series, episodes, episode file,
  translations, naming and root folder rows, TV `DownloadHistory` rows, cover folder and `testdata/*`;
  the fake indexer and listener processes are stopped and the qBittorrent stack is down.
  `Tv_SceneMappings` keeps its rows, and `Tv_Metadata` keeps the default Kodi/Emby row that the first
  import created. Backup: `~/plattarr-dev-backups/config-tvstep6-20261023-0645.tgz`.


### TV step 7, part 1 (API): as built

Built in session 42 against Sonarr `b3d4bb8`. No migration and no core contract change. Sonarr's API
controllers are ported, not rewritten (`Radarr.Api.V3/Tv/`, namespaces `Radarr.Api.V3.Tv.*`, `Sonarr.Http`
swapped for `Radarr.Http`). Calls made while building (each is reversible):

- **Routes.** Names Sonarr owns alone keep Sonarr's route under `/api/v3`: `series`, `series/lookup`,
  `series/editor`, `series/import`, `series/{id}/folder`, `episode`, `episode/monitor`, `episodefile`.
  Where Radarr already owns the route, TV moves beside it: `tv/rootfolder`, `tv/config/naming`,
  `series/rename` (Radarr's is `rename`), `history/tv`, `release/tv`, `release/tv/push`. The compatibility
  URL bases (`/sonarr/api/v3`) will serve Sonarr's own route names later.
- **Type names are distinct** so the OpenAPI schema ids do not collide: `TvRootFolderResource`,
  `TvNamingConfigResource`, `TvNamingExampleResource`, `EpisodeMediaInfoResource`, `EpisodeHistoryResource`,
  `TvReleaseResource`. `CommandNamesFixture` and the route table are the guards.
- **Shared queue endpoints are kind-neutral.** `GET /queue` shows every download a module matched
  (`QueueItemExtensions.IsMatched`: the release has a parent), so TV items are no longer hidden
  (`includeUnknownMovieItems` keeps its name and now means "not matched by any module"). `QueueResource`
  gained `seriesId`, `episodeId`, `seasonNumber`, `series`, `episode`, `episodeHasFile`, written only when
  set, so a movie item serializes as before. New query parameters: `seriesIds`, `includeSeries`,
  `includeEpisode` (and `seriesId` on `queue/details`). `queue/status` counts matched items of any kind.
  Sorting adds `series.sortTitle`; `year` reads the release's year.
- **Pending queue.** Each module keeps its own pending table, so `PendingQueueService` (API project) joins
  the two lists and sends a removal to the module that owns the item. Manual grab of a pending item passes
  the `RemoteRelease` to `IDownloadService`; a manual blocklist of a pending item goes to the module's
  blocklist service by kind.
- **TV validators in the core:** `SeriesExistsValidator`, `SeriesRootFolderExistsValidator` (the shared
  `RootFolderExistsValidator` reads the movie root folders).
- **TV history** is Sonarr's controller at `history/tv` (paged, `since`, `series`, `failed/{id}`) over
  `Tv.History.HistoryService`; `failed/{id}` calls `MarkAsFailed(MediaKind.Tv, id)`.
- **TV releases.** `release/tv` (GET: `episodeId`, or `seriesId` with `seasonNumber`, or RSS; POST: grab
  from the 30-minute cache, with Sonarr's override rules) and `release/tv/push`. Flags and `imdbId` map as
  the movie resource does (`IndexerFlags` is still Radarr's vocabulary; the collision stays open).
- **Not done in part 1:** ManualImport for TV, TV auto-tagging, the TV blocklist API, the series-shaped
  webhook `Test` payload, `OnImportComplete`, the TV clear-blocklist command, the statistics caller beyond
  the series list, `openapi.json` regeneration, and every frontend page.

**Tests.** `QueueResourceFixture` (TV mapping, series profile scoring, include flags, movie JSON unchanged,
`IsMatched`), `PendingQueueServiceFixture` (join, find, removal by kind), `SeriesExistsValidatorFixture`,
`SeriesRootFolderExistsValidatorFixture`, two Host container tests (TV controllers; pending queue and the
queue controllers that use it). `QueueController` itself cannot be resolved against the dummy database
(its constructor reads the default profile), so its list filter is proved live.

**Mutation checks.** Four mutations (`IsMatched` movies-only, removal routed to the wrong module, no series
profile score, `episodeHasFile` dropped); all four were killed.

Results (Release, full solution builds with 0 warnings): Core 8,233 passed and 29 skipped, Host 41 and 5
skipped, Api 47, 0 failed.

**Live proof (2026-10-23, image `4a31542`, dev stack on the test host).** Every step went through the new API; no
SQL seeding and no fake indexer were needed.

1. `POST tv/rootfolder`, `PUT tv/config/naming` (`renameEpisodes` true), a qBittorrent client and a Webhook
   notification (all `on*` flags) were created through the API. The Webhook fired `SeriesAdd` with a
   series-shaped payload.
2. `series/lookup?term=tvdb:121361` returned Game of Thrones; `POST series` added it (128 episodes after the
   refresh). **`addOptions.monitor: "none"` leaves the series itself unmonitored** (Sonarr's rule), so the first
   push was rejected with "Series is not monitored"; `PUT series/{id}` with `monitored: true` fixed it.
3. `POST release/tv/push` (Sintel torrent, title `Game.of.Thrones.S01E01.1080p.WEB-DL.x264-BLENDER`) returned
   approved and grabbed. History (`history/tv`) showed `grabbed` with `seriesId`/`episodeId`; the `Grab`
   webhook carried `series`, `episodes`, `release` and `downloadId`.
4. The shared `GET queue` listed the TV item **without** `includeUnknownMovieItems`, with `seriesId`,
   `episodeId`, `seasonNumber`, and `series`/`episode` with the include flags; `queue/details?seriesId=5` and
   `queue/status` counted it. Before the torrent was renamed the item was `importBlocked` ("Unable to parse
   file") and a `ManualInteractionRequired` webhook arrived.
5. After renaming the torrent to the release title, the import ran: `downloadFolderImported` in `history/tv`,
   `episodefile?seriesId=5` returned the renamed file (`Season 1/Game of Thrones - S01E01 - Winter Is Coming
   WEBDL-480p.mp4`, scene name kept, media info read), the episode showed `hasFile`, and the `Download`
   webhook carried `episodeFile` and `isUpgrade: false`. The queue emptied.
6. Failure path: S01E02 pushed with the Cosmos Laundromat torrent, then `DELETE queue/{id}?removeFromClient=true
   &blocklist=true&skipRedownload=true` gave `downloadFailed` in `history/tv` and a `Blocklist` row with
   `MediaKind` 2, `ParentId` 5 and `ItemIds` `[569]`.
7. Regression: `RssSync` ran; `GET release?movieId=1` still returned 70 releases, 56 approved, 63 mapped; the
   movie queue was empty as before.
8. `DELETE series?deleteFiles=true`, the root folder, client and notification all returned 200. Torrents,
   the qBittorrent stack and the hook listener were removed. The only Warn lines were "root folder is empty"
   (expected) and four `Unknown video format: 'AVC'` lines logged when `PUT tv/config/naming` built its
   examples (the ported `FileNameSampleService` uses the same `VideoFormat = "AVC"` sample as Sonarr; it
   follows the upstream code, so it is noise, not a regression). No Error lines.

Still open after the proof: the webhook `Test` payload is movie-shaped; the quality of the imported file
comes from the real file (480p), as in Sonarr; the checked-in `openapi.json` is stale.

### TV step 7, part 2 (API): as built

Built in session 43 against Sonarr `b3d4bb8`, in five batches (`e6f4d66`, `ca0d584`, `727d056`, `b47ebf5` and the
auto-tagging commit). Two migrations (007, 008) and two neutral core contracts (`ImportCompleteMessage`,
`IAutoTagUsageSource`). Calls made while building (each is reversible):

- **Routes added** (all under `/api/v3`): `wanted/missing/tv`, `wanted/cutoff/tv`, `calendar/tv` (with
  `calendar/tv/Sonarr.ics`), `blocklist/tv`, `parse/tv`, `manualimport/tv`, `extrafile/tv`, `autotagging/tv`
  (with `autotagging/tv/schema`). `RouteTableFixture` guards them. Type names stay distinct for the OpenAPI
  schema ids (`TvAutoTaggingResource`, `TvAutoTaggingSpecificationSchema`, `TvExtraFileResource`, and so on).
- **Tag usage.** `TagService` asks every module's `ITaggedItemSource` which library entries carry a tag, so
  series appear in `TagDetails.SeriesIds` and keep a tag from being deleted (part 2a). Auto tags use the same
  shape: `IAutoTagUsageSource` (`NzbDrone.Core.Modules`) returns, per auto tag, the tags it applies and the tags
  its conditions look for. `TagDetails.SeriesAutoTagIds` is new (the movie `AutoTagIds` is unchanged, so ids
  from the two tables never mix), `CleanupUnusedTags` keeps condition tags, and `Tv_AutoTagging` joined
  `TagUsage.Tables`. `TagService` and `CleanupUnusedTags` were already on the module-boundary allowlist; the
  list did not grow.
- **Commands.** The clear-blocklist command is `ClearTvBlocklist` and manual import is `TvManualImport`
  (a command is looked up by its type name; `ClearBlocklist` and `ManualImport` are the movie module's).
- **Blocklist.** `blocklist/tv` is Sonarr's controller over `Tv.Blocklisting`: paged, `series`, `bulk`
  delete, `DELETE blocklist/tv/{id}`. The blocklist spec matches by exact title (case-insensitive) or by
  torrent info hash, and the hash is only known after the client reports the download.
- **Manual import** (`manualimport/tv`, command `TvManualImport`). Ported from Sonarr's service. Two
  deviations, both for files Sonarr cannot parse: `ReprocessItem` gives a file that does not parse an empty
  `ParsedEpisodeInfo` (Sonarr's `Augment` throws and the call returned 500), and a file inside a tracked
  download uses the parse made at grab time (`DownloadClientInfo`). The controller defaults a missing
  `quality` and `languages` instead of throwing a `NullReferenceException`. `GET manualimport/tv` without
  `folder` or `downloadId` fails with a 500, as Sonarr does (parity kept; the movie endpoint answers 400).
- **On Import Complete** (Sonarr's event). One flag on the shared `NotificationDefinition` (migration 007,
  default off, so existing notifications are unchanged), one neutral `ImportCompleteMessage`, one
  `INotificationDispatcher.ImportComplete`. `TvImportCompleteNotifier` handles `DownloadCompletedEvent` for
  TV downloads only: it reads the imported episode ids from history (`downloadFolderImported` rows of the
  download whose parent is the series), loads the episodes and files, and sends the full-season message when
  the grab was a season pack. It sends nothing when no file was imported. Webhook payload: Sonarr's shape
  (`eventType` `Download`, `fileCount`, `sourcePath`, `destinationPath`). Movies never send the event
  (`MovieWebhookPayloadBuilder.BuildImportComplete` throws `NotSupportedException`; Radarr has no such event).
  Sonarr's second trigger, completion of an untracked download, is not ported on purpose.
- **Webhook Test sends every module's test payload** (one per `IWebhookPayloadBuilder`, ordered by kind), so
  the receiver sees a movie-shaped and a series-shaped example.
- **Extra files.** Sonarr has no extra file endpoint, so `extrafile/tv?seriesId=` is the series version of
  Radarr's `extrafile`: subtitles, then metadata, then other files, each ordered by relative path.
- **TV auto-tagging.** Own table `Tv_AutoTagging` (migration 008; the same four columns as `AutoTagging`),
  own service, repository, event and API, because its conditions (network, series type, original country)
  and its target (series tags) are the TV module's. All eleven Sonarr specifications are ported (genre,
  monitored, network, original country, original language, quality profile, root folder, series type, status,
  tag, year); the movie-only ones (keyword, runtime, studio) are not. Matching is Sonarr's:
  conditions of one type form a group, a group needs one match unless a member is required, every group must
  match, and `removeTagsAutomatically` takes the tags back when the series stops matching. Hooks (as Sonarr):
  `SeriesService.UpdateSeries` (single and bulk) and the end of a refresh. One deviation: when refreshing all
  series Sonarr applies the tags to the series object read **before** the refresh and then saves it, which
  writes the stale copy back; Plattarr applies them to the refreshed object. The tag condition reuses
  `FieldType.MovieTag` (a tag picker; there is no `SeriesTag` type in Plattarr). `TagController` also
  broadcasts on `SeriesAutoTagsUpdatedEvent`.
- **Already covered, no new code.** Series statistics ride on `GET series` (`statistics` per series and
  season); decisions and their rejections for an episode come from `GET release/tv`.
- **Not done:** `openapi.json` regeneration (needs a Debug build once the API is complete), the series
  frontend, and the unified UI.

**Tests.** New fixtures: `ManualImportServiceFixture`, `TvManualImportResourceFixture`,
`TvImportCompleteNotifierFixture`, `NotificationServiceImportCompleteFixture`, `WebhookTestPayloadFixture`,
`NotificationOnImportCompleteFixture` (migration 007), `TvExtraFileResourceFixture`,
`TvAutoTaggingServiceFixture`, `TvAutoTaggingSpecificationFixture`, `TvAutoTaggingRepositoryFixture`,
`TvAutoTagUsageSourceFixture`, `TvAutoTaggingFixture` (migration 008), `AutoTaggingFixture` (series service),
`TvAutoTaggingResourceFixture`, and additions to the tag service, tag cleanup, refresh and golden notification
fixtures; Host container tests cover each new controller group. The golden file `tv-webhook.json` grew by the
import-complete payload.

**Mutation checks.** Twelve in all, all killed: the series filter and the enabled-flag test in the notifier,
the tag check in the dispatcher, the empty parse info and the client-title parse in manual import, the
controller defaults, the extra-file ordering, and the five auto-tagging mutations (single update, bulk update,
condition tags in cleanup, refreshed-versus-stale series, condition tags in `Uses`).

Results (Release, full solution builds with 0 warnings, analyzers on): Core 8,334 passed and 29 skipped, Host 46
and 5 skipped, Api 72, 0 failed.

**Live proof (2026-10-23, image `81000bc`, dev stack on the test host).** Migrations 007 and 008 were proved on a copy of
the config first (version 8, both logged); a backup was taken before the deploy.

1. **Auto-tagging** (`autotagging/tv`). Schema lists eleven specifications with their fields. Validation:
   empty name, no tags, no conditions and an empty network each answer 400; a duplicate name answers 400
   ("Must be unique"). A condition with an unknown implementation (`KeywordSpecification`, a movie one)
   answers 500 with `ArgumentException`, which is what the movie endpoint does too. A network condition `HBO` on
   Game of Thrones: `PUT series/{id}` returned the series with the tag, `GET tag/detail/{id}` showed
   `seriesAutoTagIds` and `seriesIds` (and an empty `autoTagIds`), and `DELETE tag/{id}` answered 409. Changing
   the condition to `AMC` with `removeTagsAutomatically` took the tag off on the next `PUT`. `RefreshSeries` and
   `PUT series/editor` (bulk update) both applied the tag again. A tag-condition specification kept its tag
   through `Housekeeping`, and the tag was deleted once nothing used it. The movie `autotagging` list stayed empty.
2. **Webhook Test** sent two payloads, one movie-shaped and one series-shaped. The Webhook schema reports
   `supportsOnImportComplete: true`.
3. **Manual import.** `GET manualimport/tv?downloadId=` listed the unparseable Sintel file with "Unable to parse
   file". `POST manualimport/tv` (reprocess), which returned 500 before the batch 3 fixes, answered 200, with and
   without `quality` and `languages`. The `TvManualImport` command imported the file for the chosen episode
   (quality and release group as chosen, `downloadFolderImported` in `history/tv`, `hasFile` true, `Download`
   webhook with `episodeFile`). The grab covered two episodes, so the first single-file command did not complete
   the download (the completion check counts the files of one command, as Sonarr does); the second command
   imported the other episode and the automatic importer then marked the download imported ("all episodes were
   previously imported").
4. **On Import Complete.** Both routes sent the payload: a normal grab imported automatically (`Cosmos`) sent
   `ManualInteractionRequired`, `Download` with `episodeFile`, then `Download` with `fileCount` 1,
   `sourcePath` and `destinationPath` (series-shaped, `downloadId`, release title); the manually completed
   download sent `fileCount` 2 (the files come from history, so a download imported in two commands reports
   both).
5. **Extra files.** A `.en.srt` beside an episode, `importExtraFiles` on, `RescanSeries`: `extrafile/tv` returned
   one `subtitle` entry with the episode file id and season. (`languageTags` holds only extra tags, as in Radarr.)
6. **Already covered.** `GET series/{id}` carries `statistics` for the series and each season; `GET release/tv?
   episodeId=` returned 8 releases from the real indexer with approval and rejection strings (for example
   "9.2 GB is larger than maximum allowed 5.4 GB"); `wanted/missing/tv` and `wanted/cutoff/tv` counted correctly
   after the imports.
7. **Regression.** `RssSync`, then `GET release?movieId=1`: 70 releases, 56 approved, 63 mapped; no movie queue.
8. **Cleanup.** The series (with files), notification, client, root folder, auto tag and torrents were deleted;
   `Housekeeping` removed the orphaned tags; the qBittorrent stack was taken down and the test data emptied.
   Only deliberate bad requests logged errors (the unknown specification, a malformed `quality.source` in a
   command body); the Warn lines were the expected unparseable-file and empty-root-folder ones.

Still open after the proof: the notification resource and schema carry only the movie-named event flags
(`onMovieAdded`, and so on), so there is no flag for series add, series delete or episode file delete yet (the
payloads and the TV events exist; the toggles come with the unified notification page); the checked-in
`openapi.json` is stale.


### TV step 7, frontend slice 3 (shared settings pages): as built

The media type switcher goes only where a page has content that differs per media type, so a
switcher is never a control that changes nothing.

- **Indexers page.** `MediaKindSwitcher` is in the settings toolbar. The options fieldset shows the
  shared fields (`minimumAge`, `retention`, `maximumSize`, `rssSyncInterval`) for every kind, the Movies-only
  ones (`preferIndexerFlags`, `availabilityDelay`, `allowHardcodedSubs`, `whitelistedHardcodedSubs`: only
  `NzbDrone.Core.Movies` reads them) under Movies, and `animeStandardFormatSearch` under Series. The
  indexer list itself is shared (one set of indexers, contract 4).
- **`animeStandardFormatSearch`** is now on `IndexerConfigResource` (`GET`/`PUT config/indexer`); the
  setting already lived in `IConfigService` (see step 4). `openapi.json` gained that one property. A
  new media type adds its own kind-specific fields under its own `useMediaKind()` check.
- **Download clients page.** `MediaKindSwitcher` in the toolbar; each client card shows the category the
  client routes the selected kind's downloads to (`DownloadClientCategory`, `Helpers/moduleCategoryFields`,
  which mirrors `ModuleCategoryFieldNames`: `movieCategory`, `tvCategory`; the `Record<MediaKind, string>`
  makes a new kind a compile error until it is added). A client with no category field for the kind, or an
  empty one, shows no label. The edit modal still shows every kind's category and priority fields together,
  each labelled with its media type, because it is one form that saves one client.
- **General settings** has no switcher: nothing on it differs per media type.
- The SignalR `calendar/tv` and `wanted/*/tv` handlers are real (they refresh `tvCalendar` and `tvWanted`),
  not no-ops, so nothing was removed.
- Proof: scratch instance in the cloud container driven with Playwright (kind-dependent fields, the
  `PUT config/indexer` body, persistence after reload, card labels per kind with a mock qBittorrent
  endpoint, no console errors).

## Music module

Source: Lidarr `develop` at `aec88028d036707619baacbbb840b30c2d716318` (2026-10-10), the pin in
`claude/plattarr-music-spike.md`. Namespace `NzbDrone.Core.Music.*`, tables `Music_`,
`MediaKind.Music` (3).

### Music step 0: as built

Built in session 48. Step 0 adds the audio quality catalog and checks that the shared core carries
Music's keys. Calls made while building (each is reversible until a migration stores them, except the
catalog IDs, which are permanent):

- **Audio catalog types live in core, next to the video ones** (`NzbDrone.Core.Qualities`):
  `AudioQuality` (`Id`, `Name`; 37 qualities plus `Unknown`), `AudioQualityDefinition` (title, group,
  weights and size limits) and `AudioQualityModel` (quality plus the shared `Revision`). Core owns them
  because the importer and the compatibility APIs (both core) need the catalog and a core type may not
  reference module code. The video types are untouched.
- **IDs.** `Unknown` is 0; the 37 others are 201 to 237, in Lidarr's default weight order at birth with
  ties in the order of Lidarr's default definitions (`MP3-8` is 201, `FLAC` 231, `WAV` 237). A test
  pins every ID and name. Lidarr's names, weights, group names and group weights, and size limits are
  copied unchanged; `AudioQualityFixture` checks all 38 rows against Lidarr's own table.
- **`LidarrQualityIds`** (`Qualities/Upstream`) maps Lidarr's 0 to 37 to the catalog and back, with the
  same API as `RadarrQualityIds` and `SonarrQualityIds`. `UpstreamIdMapFixture` checks it against Lidarr's
  own ID and name table, and checks that Radarr and Sonarr map no audio quality and Lidarr no video one.
- **Storage.** `AudioQualityIntConverter` (JSON) and `DapperAudioQualityIntConverter` write a quality as
  its catalog ID, as `QualityIntConverter` does for video. `MusicTableMap` registers them in step 1.
- **Where the rest lives.** The audio quality profile (`Music_QualityProfiles`) and definition
  (`Music_QualityDefinitions`) tables, repositories and services belong to the Music module; they are
  step 1. Only the catalog and its ID maps are in core.
- **Shared core types keep `QualityModel`.** The queue, `RemoteRelease`, `CustomFormatInput`, the failed
  and ignored download events and the webhook client item carry a video `QualityModel` (11 core files outside the
  modules name it, its own among them). Rather than make those types generic, a Music release shows its audio quality there as
  a display copy: `AudioQualityModel.ToDisplayModel()` keeps the ID, name and revision and leaves source,
  resolution and modifier at their defaults, and `FromDisplayModel` gets the audio quality back from the
  ID alone (video IDs 1 to 199 never overlap audio IDs 201 to 399, which is why the ranges are disjoint).
  A video ID passed to `FromDisplayModel` throws. Nothing in core reads a display copy from the database:
  stored Music rows (history, blocklist, track files, pending releases) hold `AudioQualityModel`, read by
  Music's own handlers. Revisit only if a core reader of a stored quality appears.
- **Audit: the shared activity keys already carry Music's.** `RemoteRelease.Parent` and `ItemIds`,
  `DownloadHistory.ParentId`, `History.ParentId`/`ItemId`, `Blocklist.ParentId`/`ItemIds`, the failed,
  ignored and completed events, `ProcessDownloadDecisions` (its overlap check reads `ItemIds`) and
  `ActivitySubject` use plain integers and name no entity. **Music's call: `ParentId` is the artist and
  `ItemId`/`ItemIds` are album IDs**, as in Lidarr (its history and blocklist are album-keyed).
  The core comments that said "the tracks" now say "the albums". `PendingReleases` is a module view keyed
  by `ParentId` (as in TV), so it needs no core change.
- **Still open for later steps.** (a) Lidarr also writes a track ID on history rows for track-file events
  (`TrackFileImported`, `TrackFileDeleted`, `TrackFileRenamed`, `TrackFileRetagged`); the shared table has
  no column for it. Decide in step 6 (a column in that step's migration, or a data key). (b) Lidarr has
  custom formats; whether an audio profile scores the global custom formats (`AppliesTo` `music`) is a
  step 4 call, and `CustomFormatInput.Quality` takes the display copy if it does.

**Tests.** `AudioQualityFixture` (every ID and name pinned, ranges and uniqueness, default definitions
against Lidarr's, converter round trip, display copy), and Lidarr cases in `UpstreamIdMapFixture`. Each
was checked by breaking the code under test and seeing the test fail: a changed catalog ID, a changed
default weight, a broken `FromDisplayModel`, and a map entry swapped (two entries, so the reverse map
still builds).

### Music step 1: as built

Built in session 48. Step 1 is the data layer: entities, repositories, tables and migration 009. It has no
services, API, parser or scheduled tasks (steps 2 to 7). Adding `MusicModule` and `MusicTableMap`
activates the module, because both are discovered automatically (the container finds `IMediaModule`; the
table mapper finds `IModuleTableMap` by reflection). Calls made while building:

- **Entities are Lidarr's, copied.** `NzbDrone.Core.Music`: `Artist`, `ArtistMetadata`, `Album`,
  `AlbumRelease`, `Track`, `Medium`, `Member`, `Links`, `Ratings`, `Entity`, the type and status enums,
  monitoring options, and the repositories. Lidarr's column names are kept, so the importer copies rows
  without renaming. Edits to the copies: `Artist.QualityProfile` is `LazyLoaded<AudioQualityProfile>`
  (module-owned); raw SQL uses the `Music_` names; `FindByTitle` is removed because it needs the parser
  (steps 2 and 3).
- **Module-owned types.** `Music/Profiles` (`AudioQualityProfile`, its items and repository, and
  `MetadataProfile` with its three item types and converters), `Music/Qualities`
  (`AudioQualityDefinitionRepository`, and `AudioQualityDefinitionService`, which seeds, updates and
  deletes definitions from `AudioQuality.DefaultQualityDefinitions` on `ApplicationStartedEvent`),
  `Music/MediaFiles` (`TrackFile`, `MediaInfoModel`, `TrackFileRepository`), `Music/RootFolders`,
  `Music/Organizer` (`NamingConfig`, `ColonReplacementFormat`). Core cannot reference module code and
  modules cannot reference each other (`ModuleBoundaryFixture`), so Music has its own `RootFolder`,
  `MediaInfoModel` and `ColonReplacementFormat` rather than sharing the Movies ones.
- **Audio quality profile has Lidarr's shape.** No `AppliesTo`, `Language` or `MinUpgradeFormatScore`.
  Whether an audio profile scores global custom formats is still the step 4 call; the repository already
  resolves custom format items through `ICustomFormatService`, as Lidarr's does.
- **Tables (migration 009, `music_module_tables`).** `Music_ArtistMetadata`, `Music_Artists`,
  `Music_Albums`, `Music_AlbumReleases`, `Music_Tracks`, `Music_TrackFiles`, `Music_QualityDefinitions`,
  `Music_QualityProfiles`, `Music_MetadataProfiles`, `Music_RootFolders`, `Music_NamingConfig`. Lidarr's
  columns, unique constraints and indexes; JSON columns default to `[]`. The migration creates tables only
  and touches no existing row.
- **Shared activity tables.** Music reads `History` and `Blocklist` through its own views
  (`AlbumHistory`, `Blocklist`) over `MediaKindRepository<T>` with `MediaKind.Music`; `ParentId` is the
  artist and `ItemId`/`ItemIds` are albums (step 0). `PendingReleases` and `DownloadHistory` get their
  Music views in step 6. **`Languages` is carried only because the shared tables require it:** on SQLite
  `History.Languages` and `Blocklist.Languages` are `NOT NULL` with no default (checked with a PRAGMA
  probe), and `BasicRepository` inserts every mapped property, so a view that omits the column cannot
  insert. Both Music views write an empty list. Music never reads it.
- **Storage of qualities.** `MusicTableMap` registers `DapperAudioQualityIntConverter` and the JSON
  converters for `AudioQualityModel` and for the profile items, so a stored quality is its catalog ID. A
  stored row with an unknown quality ID throws when read, as video does; qualities are never removed.
- **Core changes.** (a) `WhereBuilderSqlite` and `WhereBuilderPostgres` treat `Contains` on a
  non-concrete `List<string>` (Lidarr's `OldForeignArtistIds`) as a string Contains on the JSON text
  (`LIKE`/`ILIKE`). It is a substring match, which is exact enough for foreign IDs (UUIDs). (b)
  `MediaCoverTypes` gains `Cover` (7), `Disc` (8) and `Logo` (9). (c) `TagUsage` counts
  `Music_Artists`; root folder `DefaultTags` are not counted, as in Lidarr. (d) Two download client
  help-text strings in `en.json`.
- **Cutoff queries.** The cutoff-unmet query matches `"quality": N` in the indented JSON, the same
  pattern as Movies; a test pins it for audio.
- **Not stored.** `AudioQualityDefinition` `Weight`, `GroupName` and `GroupWeight` are not columns; the
  service fills them from the catalog defaults, as Movies does. The Lidarr default profiles (Any,
  Lossless, Standard) are not created yet (steps 3 and 7). The `IndexerFlags` translation from Lidarr's
  values to the global ones belongs to the importer.
- **Still open.** `TrackId` on history rows (step 6, see step 0); custom formats on audio profiles
  (step 4).

**Live proof (dev stack, `63fe578`).** Migration 009 was run first on a copy of the dev config: version 9,
the 11 `Music_` tables, 38 audio definitions seeded (37 plus Unknown), no existing History or Movies row
changed, no Warn or Error. On the deployed stack: `RssSync` completed, the Movies baseline is unchanged
(`release?movieId=1` = 70 releases, 56 approved, 63 mapped), `Housekeeping` completed, and the download
client schema shows `musicCategory`, `musicImportedCategory`, `recentMusicPriority` and
`olderMusicPriority` beside the Movies and Tv fields. **A client stored before this deploy has no Music
entry** (the sparse `ModuleCategories` list, fact 18 in the state doc); it gets one when saved once or
through `EnsureEntries`. Nothing grabs Music yet, so nothing fails.

**Tests.** `MusicTests` has fixtures for each repository, the profiles, the definition service, album
history, the blocklist, root folders, naming config and `MusicModule`, over fully populated entities
that must round-trip every column. `MusicModuleTablesFixture` checks the migration (tables empty, existing
activity rows untouched, every mapped property has a column, defaults, uniqueness, indexes). The
`Contains` handling has tests in both where-builder fixtures. All of it passes on SQLite and on
PostgreSQL 16. Each was checked by breaking the code under test and seeing a test fail: the definition
weight, the history and blocklist kind, the monitored filter on `GetFilesByArtist`, the JSON list
`Contains`, the audio quality JSON and Dapper handlers, the cutoff clause, a unique constraint, the album
type converter, the default categories and a table name. Lesson: a mutation run restores the source
file but not the built assembly, so rebuild before the next test run.

### Music step 2: as built

Built in session 49 against Lidarr `aec88028`. No migration, no table change, no service, no API, no
scheduled task. Calls made while building (each is reversible):

- **Ported into `NzbDrone.Core.Music.Parser`**: `Parser` (static: `ParseMusicPath`, `ParseMusicTitle`,
  `ParseAlbumTitle`, `ParseAlbumTitleWithSearchCriteria`, `ParseReleaseGroup`, `ParseArtistName`,
  `RemoveFileExtension`, and the extensions `CleanArtistName`, `CleanAlbumTitle`, `CleanTrackTitle`,
  `NormalizeTrackTitle`, `RemoveBracketsAndContents`, `RemoveAfterDash`), `QualityParser` with the
  `Codec`, `BitRate` and `SampleSize` enums, `SceneChecker`, `IsoCountries`, `IsoCountry`; in
  `Music.Parser.Model`: `ParsedAlbumInfo`, `ParsedTrackInfo`, `ArtistTitleInfo`. Lidarr keeps its release
  group parsing inside `Parser`, so there is no Music `ReleaseGroupParser` or `ParserCommon`.
  `RegexReplace` and `InvalidDateException` are the core ones (identical to Lidarr's).
- **Also ported because the parser needs them:** `Music.MediaFiles.MediaFileExtensions` (the audio
  extensions and the quality each one implies; step 5 adds nothing to it, it is the whole file), and in
  `NzbDrone.Core.Music` the `AlbumYearMatcher`, `IAlbumYearMatcher`, `AlbumYearMatchingOptions`,
  `AlbumYearMatchResult` and `YearMatchConfidence` (`ParsedAlbumInfo.YearConfidence` is one). The matcher
  is used by album lookup in step 3.
- **Quality output is the audio catalog.** `QualityParser.ParseQuality` returns an `AudioQualityModel`;
  every `Quality.X` of Lidarr became `AudioQuality.X` with no change of name or logic (the catalog names
  are Lidarr's, step 0). `parsing_our_own_quality_enum_name` proves all 13 names re-parse.
- **Detection sources (core change).** (a) `QualityDetectionSource.TagLib = 4` is appended (Lidarr's enum
  was `Name, Extension, TagLib`, so its value would have collided with Plattarr's `MediaInfo = 3`).
  (b) `AudioQualityModel` gains `QualityDetectionSource` and `RevisionDetectionSource`, both
  `[JsonIgnore]`, so nothing about them is stored. (c) **Lidarr's `Name` was the enum's zero value, so a
  title-based result needed no assignment. Plattarr's enum starts at `Unknown`, so the Music
  `QualityParser` now sets `Name` explicitly** (empty title, and the model built by
  `ParseQualityModifiers`); the extension and tag branches overwrite it as before. Without this, the
  upstream `should_parse_quality_from_name` fails (it did on the first run).
- **Kept as ported (Lidarr quirk):** a title with no codec and no known extension records
  `Extension` as the source though the quality stays `Unknown`. Pinned by a test.
- **Finding a clean name.** Lidarr's artist lookup is `ArtistService.FindByName`
  (`ArtistRepository.FindByName(title.CleanArtistName())`) and `FindByNameInexact`. The repository method
  has existed since step 1; the service is step 3. What step 2 changes: the test helper
  `MusicTestData.Clean`, which stood in for `CleanArtistName`, now calls it, and a repository test proves
  that the clean name stored for an artist and the one looked up agree for accents, case and punctuation
  (`Beyoncé`, `Motörhead`, `Earth, Wind & Fire`, `Florence + The Machine`).
- **Deferred on purpose.** `ParsingService` (needs `ArtistService` and `AlbumService`, step 3; its
  fixtures `GetArtistFixture`, `GetAlbumsFixture`, `GetAlbumsWithYearFixture` go with it),
  `FingerprintingService` and its fixture (step 5, with the `fpcalc` decision), `LocalTrack` and
  `LocalAlbumRelease` (step 5), `RemoteAlbum` (step 4, as `RemoteRelease`), `ImportListItemInfo` (no Music
  import lists). `ReleaseInfo` and `TorrentInfo` are the shared core types.

**Tests.** Eleven of Lidarr's `ParserTests` fixtures run under `MusicTests/ParserTests`
(`ArtistTitleInfo`, `CrapParser`, `ExtendedQualityParserRegex`, `HashedRelease`, `MusicParser`,
`NormalizeTitle`, `Parser`, `PathParser`, `QualityParser`, `ReleaseGroupParser`, `YearParsing`): 455 cases,
19 of them `[Ignore]`d upstream with Lidarr's own reasons (kept ignored). New in Plattarr: 9 for the
detection sources, 19 for the file extensions, 13 for countries (two-letter, three-letter and name),
19 for the year matcher, 2 for the stored model and the enum value, 4 for the artist lookup. Each new
behavior was checked by breaking the code under test and seeing a test fail: the `Name` default in
both places, the tag source, the stored-JSON flag, 24-bit FLAC, the any-release rule, the close-year
bonus, a file extension, the accent removal, the enum value, the three-letter country code and the
live-album rule (12 mutations; one survived the first time, the three-letter code, and got a test).
Full verify: 0 warnings, 0 errors; Core 9,073 passed (502 more than before), 48 skipped; Api 89; Host
48. The Music `DbTest`s (78) also pass on PostgreSQL 16.

### Music step 3: as built

Built in session 50 against Lidarr `aec88028`. No migration and no table change. One scheduled task
(`RefreshArtist`), two config keys, no API, no UI. Calls made while building (each is reversible):

- **Metadata source.** `Music.MetadataSource.SkyHook.SkyHookProxy` implements `IProvideArtistInfo`,
  `IProvideAlbumInfo`, `ISearchForNewArtist` and `ISearchForNewAlbum` against the Lidarr metadata service,
  `https://api.lidarr.audio/api/v0.4/{route}` (`artist/{mbid}`, `album/{mbid}`, `search?type=&query=`,
  `recent/artist`, `recent/album`). The base URL comes from `ILidarrCloudRequestBuilder`
  (`NzbDrone.Common.Cloud`, new, same shape as the Radarr and Sonarr builders). The new config key
  `MusicMetadataSource` (default empty) overrides it, the same way `MetadataSource` does for Movies. The
  album filter is Lidarr's: the metadata profile's allowed primary types, secondary types (an album with
  no secondary type counts as `Studio`) and release statuses. The service's `Logo` image maps to
  Plattarr's `Clearlogo`.
- **Services ported** (all in `NzbDrone.Core.Music`): `ArtistService` (`FindByName`, `FindByNameInexact`,
  `GetCandidates`), `AlbumService`, `ReleaseService`, `TrackService`, `ArtistMetadataService`,
  `AddArtistService`, `AddAlbumService`, `RefreshArtistService`, `RefreshAlbumService`,
  `RefreshAlbumReleaseService`, `RefreshTrackService`, `ShouldRefreshArtist`, `ShouldRefreshAlbum`,
  `MonitorNewAlbumService`, `AlbumMonitoredService`, `AlbumAddedService`, `AlbumEditedService`,
  `ArtistEditedService`, `AlbumCutoffService`, `ArtistPathBuilder`, `MoveArtistService`,
  `Music.Parser.ParsingService` with its three fixtures, `MetadataProfileService`,
  `AudioQualityProfileService`, `Music.RootFolders.RootFolderService`, `Music.MediaFiles.MediaFileService`,
  `Music.Covers.MediaCoverService`, plus the commands and events they publish
  (`Music.Commands`, `Music.Events`, `Music.Search` data-only commands).
- **Default profiles.** On `ApplicationStartedEvent`, when the table is empty, `AudioQualityProfileService`
  seeds **Any, Lossless, Standard** and `MetadataProfileService` seeds Lidarr's two metadata profiles
  (**Standard**: albums, studio, official; and an empty **None**). A quality group is allowed whole when any of its members is
  (Lidarr's rule), so "Lossless" also allows APE and WavPack. The cutoff is the id of the group that holds
  the cutoff quality (group ids start at 1000). Visible effect: on first start of an existing instance,
  `Music_QualityProfiles` and `Music_MetadataProfiles` gain rows. Nothing else reads them yet.
- **`RefreshArtist` task.** `MusicModule.ScheduledTasks` returns `RefreshArtistCommand` at
  **24 h** (Lidarr's interval; Sonarr's is 12 h). The task shows in `GET /api/v3/system/task`. Until the
  library has artists, it does nothing.
- **Interim services.** Lidarr's disk scan, file naming, tag reading and history are step 5 and 6 work.
  Step 3 needs a working shape of each, so the Music module has these, to be completed there:
  `MediaFiles.DiskScanService` (publishes `ArtistScannedEvent`; creates a missing artist folder only when
  the new key `CreateEmptyArtistFolders` is on and `DeleteEmptyFolders` is off), `Organizer.FileNameBuilder`
  and `NamingConfigService` (the artist folder and the parts `AddArtistService` and `MoveArtistService`
  use), `MediaFiles.AudioTagService`, and `History.HistoryService` (reads and `UpdateMany` over
  `IAlbumHistoryRepository`).
- **Config keys.** `MusicMetadataSource` (string, default empty) and `CreateEmptyArtistFolders`
  (bool, default false), both in `IConfigService`. Plattarr config is a single table, so nothing else
  changes.
- **Shared helpers moved into `NzbDrone.Common`.** `FuzzyContains.cs` and, in `StringExtensions`,
  `FuzzyMatch`, `FuzzyMatchComponents` and `LevenshteinCoefficient`; in `IEnumerableExtensions`,
  `ExclusiveOrDefault` (two overloads). Lidarr has them in its own `NzbDrone.Common`, which Plattarr
  shares with Movies and TV, so they are not Music-only. `AlbumRepository.FindByTitle` is new on the
  repository (the services need it).
- **Equality trap.** `Artist` now implements `IMediaParent` explicitly (`Kind` is `Music`, `Title` is the
  name). Equ compares non-public members, so both are marked `[MemberwiseEqualityIgnore]`; without that two
  copies of one artist differ by whether the metadata is loaded, and two upstream tests fail.
- **Covers.** Lidarr's cover service ported with Lidarr's folder layout, `MediaCover/Artists/{id}` and
  `MediaCover/Albums/{id}`. **Finding (not fixed):** the Movies and TV cover services both write
  `MediaCover/{id}`, so a movie and a series with the same id share one folder (and `MediaCoverController`
  serves `MediaCover/{movieId}` only). Music does not join that collision, because of its sub-folders, but
  Movies and TV should be split when the covers are next touched. Lidarr's `MediaCover` has an `Extension`
  property that Plattarr's does not; the Music service takes the extension from the URL.
- **Not ported on purpose** (each needs a table or a surface Plattarr does not have yet): import-list
  exclusions (the `addImportListExclusion` flag still rides on the artist and album delete events; nothing
  consumes it), auto-tagging on refresh, and the root folder file watcher. They come back with the surface
  that needs them.
- **Test dependency.** Lidarr's refresh tests use **AutoFixture**; `Radarr.Core.Test.csproj` gains
  `AutoFixture` 4.17.0. It is a test-only package.
- **Core test helper.** `CoreTest.UseRealHttp` also registers `ILidarrCloudRequestBuilder` (as it already
  does for the Radarr and Sonarr builders), because the metadata proxy resolves it.

**Tests.** Lidarr's metadata, refresh, add, monitor, move, album, artist, media file, root folder,
file-name and parsing-service fixtures run under `MusicTests` (the Music suite was 930 passing, 20
skipped before the new fixtures below). The hermetic SkyHook tests replay three recorded payloads from the
real service (`Files/SkyHook/lidarr-*.json`, 2026-10-24: an artist, an album and a search), as the TV
`SkyHookTestData` does; Lidarr's own proxy fixture needs the live service, so it is replaced, not
ported. New in Plattarr: SkyHook proxy (routes, override, errors, filter), `FindByTitle`, default
profile group and cutoff rules (and a seeding `DbTest` on the real tables), `MusicModule` schedule,
`DiskScanService` (11), cover download and skip rules, `HistoryService`, `RemoteAlbum`,
`MusicSearchRequest` query titles, `Artist` as `IMediaParent`, the config defaults, the helper
extensions in `NzbDrone.Common.Test`, and a Host container test that resolves the new services and the
`RefreshArtist` executor. Each new behavior was checked by breaking the code under test and seeing a
test fail (13 mutations: `FindByTitle` twice, `ExclusiveOrDefault`, the fuzzy index penalty, the task
interval, the cover skip rule for artists and for albums, the whole-group rule, the group cutoff, the
Studio rule, the release-status filter, the empty-folder setting, and the `Artist` equality ignore; three
survived the first time, the whole-group rule, the group cutoff and the Studio rule, and got a test each).
Full verify: 0 warnings, 0 errors; Core 9,486 passed (413 more than step 2), 49 skipped; Api 89; Host 50
(5 skipped). No schema change, so the PostgreSQL pass was not repeated.

### Music step 4: as built

Built in session 51 against Lidarr `aec88028`. One migration (010), one column. No API, no UI, no
scheduled task; the search commands are registered but nothing routes or schedules them yet (step 7).
Calls made while building (each is reversible):

- **Rejection reasons are appended.** `DownloadRejectionReason` gained the Music reasons at the end, so
  existing numeric values are unchanged. Lidarr's reason-less decision tests were strengthened to
  assert the reason (for example `MinimumAgeDelay`, `NoMatchingTag`, `DiskHigherPreference`,
  `ArtistNotMonitored`, `WrongArtist`, `YearMismatch`).
- **Specifications are Lidarr's (22).** They live in `Music.DecisionEngine.Specifications` (root,
  `RssSync`, `Search`) behind the module's own `IDownloadDecisionEngineSpecification`, and run in one
  priority order with the core release specifications: 13 root, 6 RSS-sync, 3 search. **Not ported:**
  `SameTracksGrabSpecification` is a stub in Lidarr (`IsSatisfiedBy` throws
  `NotImplementedException`, with a TODO to rework it for tracks), and `SameTracksSpecification` is
  its only caller. Porting a specification that throws would reject every release it runs on.
- **Matching.** `MusicReleaseMatcher` is Lidarr's title parse and artist/album lookup moved out of
  `DownloadDecisionMaker` (contract 2). `DownloadDecisionMaker` rejects a release that matches no
  artist or no album before any specification runs. The Host container test resolves the real
  matcher, because a decision-maker test that mocks `IParsingService` only never reaches it.
- **Custom formats score on audio profiles.** `MusicCustomFormatCalculationService` builds the neutral
  `CustomFormatInput` from a `RemoteAlbum`, a track file, a blocklist row or a history row (TV step 4a
  shape). `CustomFormatAllowedByProfileSpecification` and the comparer read the audio profile's
  format scores.
- **`UpgradableSpecification` is Music-specific.** It compares `AudioQualityModel` through
  `AudioQualityModelComparer` and the audio profile's quality groups. The core
  `NzbDrone.Core.DecisionEngine.UpgradableSpecification` stays Movies- and TV-only; fixtures that see
  both alias one of them.
- **Search criteria.** `MusicSearchRequest` (`AlbumSearchRequest`, `ArtistSearchRequest`) reaches the
  specifications through `ReleaseDecisionInformation`. `MusicSearchTranslator` is an ordered-tier
  `ISearchTranslator<MusicSearchRequest>`; a tier is skipped when the request lacks the data it needs.
- **A search goes through the shared fetcher.** `Music.Search.ReleaseSearchService.Dispatch` calls
  `IIndexerReleaseFetcher.Search(request, artist.Tags, MediaKind.Music, translator)`. Lidarr's
  per-indexer tag tests became one test that the tags reach the fetcher. `LastSearchTime` is set,
  and saved, only when `IndexersSearched` is above 0.
- **Executors.** `AlbumSearchService` (`AlbumSearchCommand`, `MissingAlbumSearchCommand`,
  `CutoffUnmetAlbumSearchCommand`), `ArtistSearchService` and `MusicRssReleaseHandler`
  (`IRssReleaseHandler`, `Kind` `Music`). **Deviation from Lidarr:** the missing and cutoff executors
  also filter by `ArtistId` when the command carries one; Lidarr's ignore it. `userInvoked` follows
  `CommandTrigger.Manual`. The Music `RssSyncCompleteEvent` is its own type in `Music.Search`
  (the event aggregator keys subscribers by type since TV step 4).
- **Early-release limit moved to the indexer override (migration 010).** Lidarr keeps
  `EarlyReleaseLimit` on the indexer. Plattarr has one global indexer, so the value is
  `IndexerOverrides.EarlyReleaseLimit` (nullable int, a Music row, null is off). Existing rows are
  unchanged. There is no API resource for it yet.
- **Protocol comparison uses the delay profile.** The comparer and the prioritizer compare protocols
  with the delay profile's `PreferredProtocol`, as in TV step 4, not Lidarr's per-indexer logic.
- **Size limits** come from the shared quality definitions. Audio limits are kbit/s, so
  `NumberExtensions.Kilobits()` (× 128 bytes) converts them.
- **Pending releases are ported on the shared `PendingReleases` table.** `Music.Pending.PendingReleaseService`
  keeps the static in-memory cache, rebuilt by `UpdatePendingReleases()` on `ApplicationStartedEvent`,
  artist-edited, artist-updated, artist-deleted, grab, RSS-sync-complete and config-saved events. **Deviation from Lidarr:** one queue item per
  release, not per album, with the queue id `HashConverter.GetHashInt31("pending-{Id}")`. Releases
  that cover the same album-id set are de-duplicated with `AudioQualityModelComparer`, then by the
  delay profile's `PreferredProtocol`. `PendingQueueService` now joins Movies, TV and Music pending items.
- **History and blocklist.** `Music.History.HistoryService` and `Music.Blocklisting.BlocklistService`
  are read services plus `Block(RemoteAlbum, message)`, which sets `MediaKind`, `ParentId` (artist),
  `ItemIds` (albums) and the release fields. The handlers that write rows are step 6.
- **Config.** `IConfigService.AutoUnmonitorPreviouslyDownloadedTracks` (default false), Lidarr's
  setting of the same name.

**Tests.** Lidarr's decision, prioritizer, pending and search fixtures run under
`MusicTests/DecisionEngineTests`, `MusicTests/PendingTests` and `MusicTests/SearchTests`, adapted to
the module seams. Two adaptations are worth knowing:

- `MusicTestData.DefaultQualities()` with no arguments **allows every quality**, as Lidarr's helper
  does. A profile that allows nothing must be built explicitly.
- Lidarr's pending tests predate its cache, so they were rewritten on the TV-style fixture base
  (`LoadCache()` raises `ApplicationStartedEvent`).

New fixtures: migration 010, the override round trip, the config default, `Kilobits`, the pending
repository (round trip, media-kind refusal, isolation from Movie rows), `PendingQueueService`,
translator tiers, the matcher, the RSS handler, the album and artist executors, and four Host
container tests (contracts, all 22 specifications, command executors, pending-cache event wiring).

**Known limit.** Host tests cannot resolve `IEnumerable<IHandle<ApplicationStartedEvent>>` from
DryIoc; the wiring test asserts the service type instead.

**Mutation checks.** 15 mutations of the new behavior; all killed. They cover the early-release limit
(null test, sign of the window, rejection reason, override media kind), both `Kilobits` overloads,
the executor `ArtistId` filter, both translator tiers' gates, the pending queue (fallback exclusion,
album-set grouping, quality order, protocol order, queue id hash) and the delay reason. One survived
the first time: the `double` `Kilobits` overload, which the size specification uses. Every Music
size test used 3 to 20 percent margins, so a 125 instead of 128 factor passed. A test at the exact
byte boundaries of the limits kills it (`should_convert_the_kbit_per_second_limits_to_exact_byte_boundaries`).

Results (Release, full solution build, analyzers on, 0 warnings): Core 9,863 passed and 49 skipped,
Host 54 (5 skipped), Api 90, 0 failed. The first full run failed two of the new tests (a Music
specification count of 21 where the real count is 22, and a `DBNull` check in the migration test);
both were test mistakes.

**PostgreSQL.** The migration 010 test, the indexer override tests and the pending release tests ran
on PostgreSQL 16 (34 `DbTest`s, 0 failed). The full `DbTest` pass was not repeated: the only schema
change is one nullable column.

**Live (dev stack, image `3ffb3ca`).** Step 4 has no endpoint or task of its own, so the proof is
that the migration and the new services do not disturb the running stack: `system/status` reports
migration 10, `IndexerOverrides` has the `EarlyReleaseLimit` column, `RssSync` completed (8 reports,
0 grabbed), `Housekeeping` completed, `GET release?movieId=1` is still 70 releases, 56 approved, 63
mapped, the queue and `PendingReleases` are empty, and the log has no Warn or Error line.
Search, decisions and pending releases for Music are proved by tests until the API (step 7) can
add an artist and a fake Torznab indexer can serve album releases.

### Music step 5: as built

Built in session 52 against Lidarr `aec88028`. No migration, no API, no UI, no scheduled task; the
import services and the album importer are registered and wired, but nothing routes `DownloadedAlbumsScanCommand`,
the rename or the retag commands yet (step 7). Calls made while building (each is reversible):

- **What is ported.** Tag reading (`AudioTagService`, TagLibSharp), identification and aggregation
  (`IdentificationService`, `CandidateService`, `TrackGroupingService`, `DistanceCalculator`, `Munkres`,
  `LocalTrack`, `LocalAlbumRelease`), the aggregators, `ImportDecisionMaker` with its 12
  specifications, `ImportApprovedTracks`, `DownloadedTracksImportService`,
  `DownloadedAlbumsCommandService`, `DiskScanService`, `MediaFileService`,
  `TrackFileMovingService`, `UpgradeMediaFileService`, `RenameTrackFileService`, retag,
  `MediaFileDeletionService`, `MediaFileTableCleanupService`, `ScriptImportDecider`, the full
  `FileNameBuilder`, `NamingConfigService` and `FingerprintingService`. These replace the interim disk
  scan, naming and tag services from step 3.
- **`SameTracksSpecification` is used here.** Step 4 did not port it because its only caller was a stub;
  the import's `SameTracksImportSpecification` does call it, so it now has a caller.
- **Typed rejections.** `ImportRejection : Rejection<ImportRejectionReason>` and `ImportSpecDecision`
  replace Lidarr's string-only rejections; the reason is never stored as an integer. Lidarr's
  tests that asserted only a message gained `Reason` assertions.
- **Album importer.** `Music.Import.MusicDownloadImporter` is the module's `IDownloadImporter`
  (`Kind` Music, `BlocksImportAfterRejection` true). `Check` skips a download with no history and no
  category, and blocks one whose title does not match the grabbed artist (manual import required).
  `Import` runs `ProcessPath(..., ImportMode.Auto, artist, importItem)`. `IsImported` is true when
  every result imported, or when the imported tracks reach the monitored releases' track count, else it asks
  `TrackedDownloadAlreadyImported` (the latest history row of every album is a `DownloadImported` or
  `TrackFileImported`). Nothing writes Music history rows until step 6, so that history check stays false.
- **Tracked-download matching.** `MusicTrackedDownloadMatcher` maps the download title through
  `ParsingService.Map` and falls back to the grabbed title in history, setting `Indexer`, `Added`,
  `IndexerFlags` and `IndexerId`. `MatchesTitle` needs both an album-shaped title **and** a library artist
  (or several), so a title that parses as an album but matches no artist stays unmatched and the core
  falls back to the client category. `MusicTrackedDownloadRefresher` rematches Music downloads when
  an artist is added, edited or deleted, when an album is deleted or removed in a refresh, and
  requests a refresh after a track import.
- **Download history.** `MusicDownloadHistoryHandler` writes a `FileImported` row for a new download
  (`MediaKind` Music, `ParentId` = artist) and deletes an artist's rows when it is deleted.
  `HistoryService.FindDownloadId` is Lidarr's: the single open grab per album, else null.
  **Deviation from Lidarr and from the Movies handler:** a manual import has no download client item,
  so the handler leaves the client fields empty instead of throwing a `NullReferenceException`.
  The Movies handler has the same latent fault; not changed here.
- **No `ImportFailed` state and no `AlbumImportIncompleteEvent`.** The core
  `CompletedDownloadService` owns the state machine; the event Lidarr raised for an incomplete album
  has no consumer here and was dropped.
- **Notification data.** `MusicNotificationData` carries only the artist and the `RemoteAlbum`, and is set on
  the manual-interaction message. Step 6 adds the other events and the builders.
- **Command renamed.** The Music `RenameFilesCommand` is `RenameTrackFilesCommand`; commands resolve by
  type name and the Movies command already has the other name (`CommandNamesFixture` guards it).
- **Config.** `TrackFileDate` (None or AlbumReleaseDate, Lidarr's "FileDate"), `AllowFingerprinting`
  (default NewFiles), `WriteAudioTags` (default No), `ScrubAudioTags` (default false) and `EmbedCoverArt`
  (default true). Tag writing, scrubbing and cover embedding therefore do nothing until the owner turns them on.
- **Not ported on purpose.** `ImportExtraFiles` (Music has no extra-file consumers), manual import
  (`ManualImportService`; step 7), the `fpcalc` binary and its health check (the image does not ship it;
  with it missing, fingerprinting finds nothing and identification falls back to tags), the naming UI
  resources and the API controllers (step 7).
- **Dependencies.** `TagLibSharp-Lidarr` comes from Lidarr's `Taglib` NuGet feed. `src/NuGet.config`
  lists the feed with a package source mapping that allows only that one package from it.
  `MostCommon` joined `IEnumerableExtensions` and `Compress` joined `NzbDrone.Common` as shared helpers.

**Tests.** Lidarr's import, identification, disk scan, moving, renaming, upgrade, deletion, table
cleanup, tag service and naming fixtures run under `MusicTests/MediaFiles` and `MusicTests/OrganizerTests`.
Lidarr's tests used `MockFileSystem`; they run on a real temporary folder instead
(`FileSystemTest<T>` with a `TestDiskProvider`, `AllowedFileShare` emulated with held file streams). That
changed one expectation: a path case difference (`C:\drop` against `c:\drop`) is real on Linux, so the
`_UNPACK_` test lowercases. The audio fixtures `nin.*` (16 MB) and the identification and fingerprint
JSON are copied from Lidarr with its licence file. Five tests are skipped for platform reasons, as upstream.

New fixtures: the album importer (`Check`, `Import`, `IsImported`, `ImportBlocked`), the import result
contract, `TrackedDownloadAlreadyImported`, the matcher (source-title fallback, indexer and flags from the
grab, `MatchesTitle`, warnings, rematch), the refresher, `FindDownloadId`, the history writer, the five
config keys, `MostCommon`, `Compress`, and five Host container tests (download contracts, import and
media file services, command executors, tracked downloads follow the artist library, history writer).

**Mutation checks.** Eight mutations of the new behavior; all killed: importer track-count threshold,
already-imported `All` to `Any`, matcher indexer from the grab, writer `NewDownload` inverted, writer
media kind, `FindDownloadId` count check and processed-grab filter, refresher kind filter.

Results (Release, full solution build, analyzers on, 0 warnings): Core 10,456 passed and 54 skipped
(593 more than step 4), Host 59 (5 skipped), Api 90, Common's new fixtures 25 with the existing
`NumberExtensionFixture`, 0 failed. No schema change, so no PostgreSQL pass.

**PostgreSQL.** Not run: step 5 changes no schema.

**Live (dev stack, image `9a312cb`).** Step 5 has no endpoint or task of its own, so the proof is that the
stack starts with the new services wired and nothing else moves. After the deploy: healthy,
`system/status` reports migration 10 (unchanged), `docker logs` has no Warn, Error or Fatal line,
`RssSync` completed (8 reports, 0 grabbed), `Housekeeping` completed, the queue and `PendingReleases` are
empty, `system/task` lists the same tasks as before, and the existing `DownloadHistory` rows are untouched.
**The Movies baseline moved from 70/56/63 to 71/57/64**, and the cause is the indexer, not this change:
the previous image (`3ffb3ca`), started on a copy of the same config, returns 71/57/64 as well, and
the new release is not recent (the newest of the 71 is 73 days old). The new baseline is 71 releases,
57 approved, 64 mapped. The import itself is proved by tests until the API (step 7) can add an
artist and a downloaded album can be fed through the importer.


### Music step 6: as built

Built in session 53 against Lidarr `aec88028`. No migration, no API, no UI, no scheduled task. The writers,
the failed-download seam, the cleanup and the notification builders are registered and wired; nothing
exposes them over HTTP until step 7. Calls made while building (each is reversible):

- **History rows.** `Music.History.HistoryService` now writes Lidarr's rows into the shared `History` table
  (`MediaKind` Music, `ParentId` = artist, `ItemId` = album). A grab, a failure and an ignored download write
  **one row per album**; an import, a delete, a rename and a retag write **one row per track**. Every handler
  that receives a core event (grab, failed, ignored, completed) returns at once for another media kind.
  `DownloadFailed` and `DownloadIgnored` turn the event's video `QualityModel` display copy back with
  `AudioQualityModel.FromDisplayModel`.
- **`TrackId` lives in `Data`** (`AlbumHistory.TRACK_ID` = `"trackId"`), not in a new column. The shared table
  has no such column and nothing filters on it, so there is no migration.
- **`DownloadImported` row.** `DownloadCompletedEvent` (published by the core when every album of a download is
  in) writes one `DownloadImported` row per album from `TrackedDownload.GetRemoteAlbum()`. This closes the gap
  from step 5: `TrackedDownloadAlreadyImported` now finds a row to read.
- **File delete skips.** `TrackFileDeletedEvent` writes nothing for the reasons `NoLinkedEpisodes` and
  `ManualOverride` (Lidarr's rule), nor when the file's artist is already gone.
- **Size.** A grab with no size writes `"0"`, so the blocklist handler can parse it back.
- **Blocklist writers.** `Music.Blocklisting.BlocklistService` handles `DownloadFailedEvent` (kind-guarded; a
  tracked torrent's download id wins as the info hash, as in TV) and `ArtistsDeletedEvent`
  (`DeleteForArtistIds`). **The clear-music-blocklist command is deferred to step 7** with the API, as TV's was.
- **Failed downloads.** `MusicFailedDownloadSource` is the TV source over `AlbumHistory`: one failure names every
  album the release was grabbed for. Upstream quirk kept: a request to skip the redownload counts only for a
  history row with **no** download id. `MusicRedownloadSearcher`: one album pushes `AlbumSearchCommand`; a
  failure that covers every album of the artist pushes `ArtistSearchCommand`; any other set pushes
  `AlbumSearchCommand` for those albums; a failure with no albums does nothing.
- **Orphan cleanup.** `CleanupOrphanedMusicActivity` deletes Music-kind rows of `History`, `Blocklist` and
  `PendingReleases` whose artist is gone, and `History` rows whose album is gone. **Lidarr's other orphan
  housekeepers (albums, releases, tracks, track files, artist metadata) are not ported**: no Music housekeepers
  existed before this step. Listed for step 7.
- **Notifications.** `MusicNotificationHandler` sends: grab ("Artist - Album - [FLAC Proper]"), download
  (once per imported album, "Artist - Album (12 Tracks Imported)"), artist added, rename, artist deleted (one
  message per artist) and manual interaction. `MusicActivityRenderer` makes the subject: item label = album
  titles joined with " + ", and an **untyped MusicBrainz link** (`MetadataLinkType` lists video databases, so the
  link renders but cannot be picked in a provider's link list).
- **Lidarr's wire shapes are kept**, including its inconsistencies: webhook events `Grab`, `Download` (an album
  import), `Rename`, `ArtistAdd`, `ArtistDelete`, `Test` (`WebhookEventType` gained `ArtistAdd` and
  `ArtistDelete`); script prefix `Lidarr_`, event types `Grab`, `AlbumDownload`, `Rename`, `ArtistAdd`,
  `ArtistDeleted`, and `Lidarr_Artist_Title` for add and delete but `Lidarr_Artist_Name` elsewhere. Manual
  interaction has no Lidarr script event; its variables follow the other modules (artist, then download).
- **Not notified yet.** Lidarr also notifies on a deleted album, a failed download, an incomplete import and a
  retag. The neutral contract has no event for them (a provider's event flags are columns on the definition), so
  they arrive with the API and UI in step 7. A track file delete is never notified (Lidarr did not either), and
  the Music builders throw `NotSupportedException` for the file-delete and import-complete messages.
- **Shared provider fix.** The Discord `Codecs` field now drops an empty video codec, so a music file reads
  "FLAC 2" instead of " / FLAC 2". Movies and TV output is unchanged (their goldens pass).

**Tests.** Lidarr has no history, blocklist-writer or notification-builder fixtures to port (its pinned tree
has none for these), so the fixtures are new, on the TV shapes: history events (21), blocklist handlers, failed
download source, redownload searcher, orphan cleanup (a `DbTest` on the real tables), renderer, handler,
builders (event-type map, refusals, Lidarr's test payload, tag labels, `Title` against `Name` variable), the
Discord codecs, three Music goldens (`music-webhook.json`, `music-customscript.txt`, `music-discord.json`, the
same Black Keys library through the real handler into one provider), the webhook test payload now sends Movies,
TV and Music shapes, and four Host container tests (failed-download source and searcher by kind, history and
blocklist writers, notification handler, payload and script builders). **A Host test that resolved every `IHousekeepingTask`
hit an unrelated recursive-dependency fault (`LogDatabase` and `MainDatabase`) and was dropped**; the housekeeper
is covered by its own `DbTest` and by the live `Housekeeping` run.

**Mutation checks.** 22 mutations of the new behavior; all killed in the end. Four first escaped: the
`DownloadCompleted` kind guard (the "other kind" case had no release, so the null check hid the mutant), the
artist-search count check (`==` against `>=`, nothing named more albums than the artist has), and two cleanup
mutations whose patterns did not apply (SQL in a verbatim string). Two tests were added and the patterns
fixed; all four then died.

Results (Release, full solution build, analyzers on, 0 warnings): Core 10,548 passed and 54 skipped (92 more
than step 5), Host 63 (5 skipped), Api 90, 0 failed. No schema change, so no PostgreSQL pass.

**Live (dev stack, image `733b463`).** Backup `config-musicstep6-20261024-1614.tgz` first. After the deploy:
healthy, `system/status` migration 10 (unchanged), no `Warn`, `Error` or `Fatal` line in `docker logs`, `RssSync`
completed (13 reports found, 0 grabbed), `Housekeeping` completed (so the new housekeeper resolves and runs),
queue empty. **Movies baseline unchanged: 71 releases, 57 approved, 64 mapped.** Step 6 has no endpoint, so the
history, blocklist and notification writers are proved by tests until step 7 can add an artist and feed a
download through them.

### Music step 7 (API, slices a to f): as built

Built after step 6 against Lidarr `aec88028`. Slices 7a to 7f are the Music API and the back-end
leftovers of steps 5 and 6; the remaining providers' notification events and the whole UI are still open (see the end of
this section). Migration 11 is the only schema change. Calls made
while building (each is reversible):

- **Route scheme.** Music sits beside Movies' routes with a `/music` suffix (`history/music`, `blocklist/music`,
  `wanted/missing/music`, `wanted/cutoff/music`, `calendar/music`, `parse/music`, `release/music`,
  `release/music/push`, `manualimport/music`); the library resources have their own names (`artist`, `album`,
  `track`, `trackfile`). Commands are found by type name, so a Music command must not reuse a name another
  module's command has (checked: no duplicates).
- **Wanted lists** use a sort-key whitelist (`albums.*`, `artists.sortName`) and a `monitored` filter that
  applies to both the album and its artist, as Lidarr does.
- **Calendar feeds.** Ical.Net rejects a `DateTime` of kind Local. The Music feed writes the release date as a
  calendar day. The TV feed had the same defect on its all-day path and now writes the local wall-clock time as
  unspecified. Both have tests that hold in any time zone.
- **Controllers that read the database in their base constructor** cannot be resolved in a Host container test;
  the Host tests resolve the services and the controller fixtures build them with mocks. Any `IMainDatabase`
  service (the housekeepers) cannot be resolved there either (recursive dependency), so a `DbTest` and a live
  `Housekeeping` run prove them.
- **Sparse metadata (live finding).** `SkyHookProxy` mapped an omitted collection to null and the NOT NULL
  columns rejected it, so an artist refresh added no album. Omitted collections now map to empty lists. The
  stored artist images could also carry a local file path as `RemoteUrl` into `GET artist`; it is cleared once
  the URLs are built. Lidarr has both defects.
- **Manual import.** `MusicManualImport` is the command name. A download that left the queue between listing and
  import is imported as plain files instead of throwing. `GET manualimport/music` answers 400 without a folder
  or a download id (the service failed with a 500 on the empty path). A command with `albumReleaseId` 0 fails
  with `ModelNotFoundException`, as in Lidarr; left alone.
- **Queue (slice 7e).** The shared queue endpoints are kind-neutral already (`IsMatched`, `PendingQueueService`
  joins the Music pending list). `QueueResource` gained `artistId`, `albumId`, `artist`, `album`, written only
  when set (the serializer already skips nulls, so the attributes only document it). `GET queue` takes
  `includeArtist`, `includeAlbum`, `artistIds`, `albumIds` (a download matches an album filter when **any** of its
  albums is listed) and sorts by `artists.sortName` and `albums.title`; `queue/details` takes `artistId`,
  `albumIds` and the include flags. A custom format score uses the artist's audio quality profile. **Fix:**
  deleting a pending Music release with `blocklist=true` fell through to the movie blocklist and would have
  thrown; it now goes to the Music blocklist. `QueueController` takes the Music `IBlocklistService`. The
  "Music views of `DownloadHistory` and `PendingReleases`" listed in step 6 need no API of their own: neither
  Radarr nor Lidarr exposes `DownloadHistory`, and pending releases are the pending part of the queue.
- **Commands and schedule.** A Host test proves every concrete command in `NzbDrone.Core.Music` has an executor,
  so each can be queued by name through `POST command`. The only scheduled Music tasks are Lidarr's (artist
  refresh and folder rescan, 24 hours each); the album searches and the download scan run on request.
- **Orphan housekeepers (slice 7d).** `CleanupOrphanedAlbums`, `CleanupOrphanedReleases`,
  `CleanupOrphanedTracks`, `CleanupOrphanedArtistMetadata` and `CleanupOrphanedTrackFiles`, raw SQL on the
  `Music_` tables as in Lidarr. The track-file task **unlinks** (`AlbumId` and the track's `TrackFileId` go to 0)
  and deletes nothing. This closes the gap listed in step 6.
- **Rescan task.** `MusicModule` schedules `RescanFoldersCommand` every 24 hours ("Rescan Folders"). A Host test
  proves that every scheduled task of every module has an executor.
- **Fpcalc check.** `FpcalcCheck` (`Music/Health`, so the `HealthCheck` namespace and type names do not collide)
  warns `FpcalcMissing` when fpcalc is not set up and `FpcalcUpgrade` when it is older than 1.4.3. It is
  **silent** when fingerprinting is `Never`, and when there is no Music root folder and no artist, so a
  Movies/TV-only install never sees it. It re-checks on a saved config and on an added artist.
- **Notification events.** The neutral contract gained `ItemDelete`, `DownloadFailure` and `Retag` (messages,
  dispatcher methods, `INotification` methods and `Supports` flags, `NotificationBase` no-ops, factory and
  definition flags). **Migration 11** adds the columns `OnAlbumDelete`, `OnDownloadFailure` and `OnTrackRetag`
  to `Notifications` (default false). The internal names are neutral (`OnItemDelete`, `OnRetag`); the wire and
  column names are Lidarr's, which is also what the API and the UI carry.
  - **Webhook** (all three), **CustomScript** (album delete and retag) and **Discord** (all three; slice 7f)
    implement them. Emby / Jellyfin got album delete and retag in slice 7i; Radarr's and Sonarr's own event flags are likewise implemented
    by only some providers (`OnImportComplete` and `OnManualInteractionRequired` exist on Webhook, CustomScript and
    Discord only).
  - Discord (7f): a danger embed titled "Artist - Album" with the files message and the content "Album Deleted";
    a danger embed titled by the release name with the reason, plus Quality and Download Client fields when they
    are not blank, content "Download Failed: reason"; a standard embed "Track File Tags Updated" with the diff.
  - Webhook events `AlbumDelete`, `DownloadFailure`, `Retag`. Script events `AlbumDeleted` and `TrackRetag`.
    The Movies and TV builders throw `NotSupportedException` for them.
  - The retag text uses an ASCII arrow (`key: old -> new`, `<missing>` for an empty value).
  - A failed download notifies only for the Music kind. **Import failure is not offered**: Lidarr has the flag,
    but nothing raises a source event for it and a manual interaction message already covers it.
- **Notification API and UI.** `NotificationResource` carries the three flags and their `supportsOn…` twins.
  The notification form shows the three events (labels "On Album Delete", "On Download Failure", "On Track
  Retag"); a provider that does not support one has the box disabled.

**Tests.** 7e adds a queue controller and details fixture (resource mapping, filters, sorts with three distinct
orders, includes, the three pending blocklist paths) and 17 mutation checks over it (one equivalent: the null-write
attribute). 7d adds: housekeepers (10, a `DbTest`; a spare metadata row keeps the ids from coinciding), Fpcalc
check (9), migration 011 (2), notification service scenarios (15), factory characteristics (2), base fixture,
handler fixtures, the goldens (`music-webhook.json` and `music-customscript.txt` regenerated with additions
only), resource mapping (4), and one Host test for the scheduled-task executors. Earlier slices: manual
import service (17) and controller (9), plus the fixtures listed in their commits.

**Mutation checks (7d).** 23 mutations of the new behavior (handler guards and messages, service factory
selection, `AnyEnabled`, support mapping, `Enable`, resource mappings, builders, the Fpcalc check); 22 killed.
Survivors, or mutants first missed, that were fixed: the factory's retag support (no test reached it; a
characteristics fixture was added), the housekeeper album join (ids coincided; spare row added). **Equivalent
mutants, left:** the `AlbumId <> 0` guard of the track-file task and the Fpcalc null-version check (`null <
Version` is true in .NET).

**Live (scratch instance, SQLite).** Queue: a pending Music release listed with `artistId` and `albumId`, the include
flags, the artist and album filters, both sort keys, `queue/details` by artist and by album, and
`DELETE queue/{id}?blocklist=true` wrote one Music blocklist row and left the movie blocklist empty. Migration 11 applied to a version 10 database and kept an existing webhook
row with the new flags false. The schema shows Webhook supporting all three events, CustomScript two, and no
other provider any. A real album delete sent an `AlbumDelete` payload to a local receiver. `system/task` lists
"Rescan Folders" at 1440 minutes and the command ran. A `Housekeeping` run removed seeded orphan albums,
releases, tracks and artist metadata, and unlinked an orphan track file and a dangling `TrackFileId` without
deleting the file row. The health list showed `FpcalcCheck` (fpcalc missing) next to the usual no-indexer and
no-client warnings. **Not proved live:** the download-failure and retag notifications (they need a grab and a
tagged file); the handler and golden tests cover them.

Results (Release, full solution build, analyzers on, 0 warnings, after slice 7e): Core 10,644 passed and 54 skipped,
Host 72 passed (5 skipped), Api 193, 0 failed. Migration 011, the housekeepers and the notification service tests also
ran on PostgreSQL 16 (108 passed; a wrong password fails the migration fixture, so Postgres was in use).
Frontend: eslint clean on the three notification files.

**Observations, faithful to Lidarr and left alone.** Root-folder default tags are not protected from
`CleanupUnusedTags`. `MusicWebhookPayloadBuilder` changes `artist.Metadata.Images` while it builds local URLs.
Adding an artist with monitor `none` leaves it unmonitored.

**Still open in step 7.** Nothing in the API. The provider events were closed in slice 7i and the UI was built in slices U1 to U3
(next sections).


### Music step 7 (UI, slices U1 to U3): as built

Built after the API against the Lidarr frontend at `aec88028`, which runs on the same stack as Plattarr's
(React 18.3, react-redux 7, react-router 5, connected-react-router), so its files copy over with path changes. Commits:
`d191386` (U1), `896ac9e` (U2 and U3), `1a724e2` (a fix found in review). Calls made while building (each is reversible):

- **Kind and navigation.** `'music'` is a `MediaKind`; the switcher, `DownloadClientCategory`'s field map and the sidebar know it.
  The sidebar has a Music entry (`/music`, alias `/artist` and `/album` so it stays highlighted on those pages; children
  Add New `/music/add/new` and Unmapped Files `/music/unmapped`). `createMediaKindPage(movies, tv, music?)` takes a third
  component **and passes it the route props**: Lidarr's list pages are wrapped in `withCurrentPage`, which needs `history`, so
  rendering them without props crashes with `Cannot read properties of undefined (reading 'action')`. Without a music
  component the page says it is not available for Music.
- **Parallel trees, copied from Lidarr and remapped:** `Artist/`, `AddArtist/`, `Album/`, `Track*`, `Retag/`,
  `UnmappedFiles/`, `InteractiveSearch/Music`, `InteractiveImport/Music`, `Activity/History/Music`,
  `Activity/Blocklist/Music`, `Calendar/Music`, `Wanted/Music/{Missing,CutoffUnmet}`, and under `Settings/`:
  `Profiles/Quality/Audio`, `Profiles/Metadata`, `Quality/Definition/Audio`, `Quality/Reset/Audio`,
  `MediaManagement/Naming/Music`, `MediaManagement/RootFolder/Music`. Shared pages (Queue, Profiles, Quality, Media
  Management, Metadata) get Music branches instead of copies.
- **Redux sections** (all registered): `artist`, `albums`, `tracks`, `trackFiles`, `artistIndex`, `artistHistory`,
  `albumHistory`, `albumSelection`, `retagPreview`, `addArtist`, `musicOrganizePreview`, `musicReleases`,
  `musicInteractiveImport`, `musicHistory`, `musicBlocklist`, `musicCalendar`, `musicWanted` (`missing`,
  `cutoffUnmet`), and under `settings`: `audioQualityProfiles`, `audioQualityDefinitions`, `metadataProfiles`,
  `musicRootFolders`, `musicNaming`, `musicNamingExamples`. SignalR handlers: `handleMusicRootfolder`,
  `handleCalendarMusic`, `handleWantedMissingMusic`, `handleWantedCutoffMusic` (name = `handle` + title-cased resource,
  slashes removed).
- **Root folders.** Music root folders are the `settings.musicRootFolders` section (`/music/rootfolder`) with no
  add-from-path: a Music root folder needs a name and the default profile ids, so it is created in the edit modal (the
  earlier plan of reusing the shared `rootFolders` section was dropped). `RootFolderSelectInput` picks the section by kind
  and hides "Add a new path" for Music.
- **Queue.** The shared Queue page asks for `includeArtist` and `includeAlbum` on every fetch, adds the columns
  `artists.sortName` and `albums.title` (visible) and `albums.releaseDate` (hidden), links to `/artist/{foreignArtistId}` and
  `/album/{foreignAlbumId}`, opens the Music manual import modal for a row with an `artistId`, and lets such a row be
  removed with "ignore". A Music row leaves the Movie and Series cells empty. `trackFileCount` and
  `trackHasFileCount` are in the API and typings but no column shows them.
- **Settings pages by kind.**
  - *Profiles:* Music lists audio quality profiles and metadata profiles; delay and release profiles are Movies and
    Series settings and are not shown for Music. The "in use" check for a Music profile reads artists and the Music root
    folders' defaults (`createMusicProfileInUseSelector`).
  - *Quality:* Movies and Series share one set of definitions; Music has its own (`/music/qualitydefinition`, reset command
    `ResetAudioQualityDefinitions`). The switcher is in the toolbar; the page is keyed by that choice so pending
    changes do not carry over. Switching between Movies and Series changes nothing on this page (the definitions are shared).
  - *Media Management:* Music shows `musicNaming`, `createEmptyArtistFolders`, `trackFileDate`, `allowFingerprinting`,
    `writeAudioTags` and `scrubAudioTags`, the shared import and recycle bin fields, and the Music root folders. The
    backend keeps the tag fields on `config/mediamanagement`, so they sit here and not on the Metadata page (Lidarr puts them there). Not shown for Music: the
    unmonitor-deleted field (Lidarr's UI has none; `autoUnmonitorPreviouslyDownloadedTracks` exists in the API), the
    video analysis field, and the File Management fieldset when advanced settings are off (it would be empty). The
    recycle bin help text uses Lidarr's wording for Music.
  - *Metadata:* Music shows one option, `musicMetadataSource` (empty = default source), and a note that Music has no metadata
    consumers.
- **Brand strings.** The copied files named the old app in three places: `window.Lidarr` and `/feed/v1/` in the calendar
  link modal (it threw), and two sentences. They now use `window.Radarr`, `/feed/v3/calendar/Lidarr.ics` (served by
  `MusicCalendarFeedController`) and translation keys with `{appName}`. Lidarr's search prefix `lidarr:` still works in
  the add-artist box (the backend accepts `lidarr:`, `lidarrid:` and `mbid:`).

**Traps found while copying (worth checking in any later port).**

- A copied form that uses a *generic* input type gets Plattarr's meaning of it: `MONITOR_NEW_ITEMS_SELECT` is the Series
  list ("All Seasons") and `QUALITY_PROFILE_SELECT` lists video profiles. Music uses `ARTIST_MONITOR_NEW_ITEMS_SELECT`,
  `MONITOR_ALBUMS_SELECT`, `AUDIO_QUALITY_PROFILE_SELECT` and `METADATA_PROFILE_SELECT`. Both mistakes rendered plausible
  option names, so only a look at the page found them.
- The key importer that scans for `translate('Key')` does not see keys held in objects or ternaries
  (`byKind({music: 'RescanArtistFolderAfterRefresh'})`); those showed as raw keys until added by hand. Scan every music page for text that looks like a key.
- Lidarr's history event list has id 7 twice; the Music filter uses 10 for the second (`AlbumHistoryEventType`).
- A Lidarr action section name like `qualityProfiles` is also a Plattarr section with a different meaning: after a copy,
  grep the new trees for `settings.` and `fetch`/`save` names and map each to its Music section (`audioQualityProfiles`,
  `musicRootFolders`, `musicNaming`, ...). The unresolved-import check does not catch this because the names resolve.
- A Lidarr `fileDate` setting is `trackFileDate` here; the three kinds each have their own date field on `config/mediamanagement`.

**Known gaps (none blocks the deploy).** The Music release filter still offers video quality values; `Album/Edit` uses the
string "releases" as a field name; interactive-search result rows were never exercised (the scratch instance has no
indexer; the page and its request were); the root folder modal's "Metadata Profile" label and the add-artist help text are
English literals; the Queue has no track-count column; the Recycling Bin help text still says "Movie files" for Series
(Movies' key). Proof, all on a scratch instance with Playwright: every Music page loads with no console error and no raw
translation key; saves reach `PUT config/mediamanagement`, `PUT music/config/naming` and `PUT config/metadata`; an audio
profile add answers 201 and an empty one 400; the metadata profile, audio profile and root folder modals open with the
right fields; the Queue shows a mocked Music item with working links; the iCal link and feed answer. Gates: eslint,
stylelint, `tsc` (zero errors) and webpack clean; `tools/verify.sh` PASS (Api 201, Host 72, Core 10,650).

### Music step 7 (provider events, slice 7i): as built

Closes the notification events of step 7. Plattarr ships four providers: Webhook, CustomScript, Discord and Emby / Jellyfin
(`MediaBrowser`). Lidarr's other providers were not ported, so the first three (7d, 7f) plus this slice cover every provider
that exists. Calls made (each reversible):

- **Follow Lidarr's provider, not the event list.** Lidarr's Emby / Jellyfin provider implements album delete (announce, then
  refresh the library) and retag (announce only) and has **no** download-failure handler. `MediaBrowser` therefore overrides
  `OnItemDelete` and `OnRetag` only; `SupportsOnDownloadFailure` stays false and the form disables that box for it.
- **Titles.** `Branded(ItemDeletedTitle(subject))` is "Radarr - Album Deleted" (`ItemDeletedTitle` is `{ItemNoun} Deleted`, so
  the Music subject's item noun supplies "Album"); a missing subject falls back to "Album Deleted". The retag title is the
  shared constant `TRACK_RETAGGED_TITLE` ("Track Retagged"). Both are announced only when "Send Notifications" is on.
- **Library refresh after an album delete is `Modified`, not `Deleted`.** The artist folder still exists; the queue batches
  one refresh per artist (`Kind`, `ParentId`), and two different update types for one artist collapse to a null type so the
  server decides, as for Movies. The refresh happens whether or not the files were deleted (Lidarr does the same) and only
  when "Update Library" is on. A retag never refreshes the library.
- **Lidarr defect not copied.** Its `MediaBrowserService.Update` assigns (`e.CollectionType = "music"`) instead of comparing
  and posts `Created` for every music folder; Plattarr's service posts the subject's own path.
- **Proxy fix (found while reading the Music path).** `MediaBrowserProxy.GetPaths` sent `years=<Year>` for every kind. A Music
  subject has no year and a server artist has no production year, so the filter could never match and the refresh worked only
  through the fallback that adds the mapped path. `years` is now sent only when the subject has a year (Movies and Series
  requests are unchanged).

**Tests.** `MediaBrowserMusicEventsFixture` (14: support flags, title, guards, update type, batching, mixed types, retag never
refreshes, failed download does nothing, two through the real `MusicNotificationHandler`) and `MediaBrowserProxyFixture`
(6: item type and `years` per kind, name match, no match, the update body). There is no Emby / Jellyfin golden file: the
provider talks to a service interface and a proxy, so the request shape is pinned by the proxy fixture. 15 mutation checks
(11 on the provider and base class, 4 on the proxy), all killed. `tools/verify.sh` PASS: Api 201, Host 72, Core 10,670
(54 skipped).


**Live proof (session 56, dev stack on `a4a0e47`, migration 11).** Everything went through the API on the dev stack with real
data and no SQL seeding: a Music root folder; a throwaway qBittorrent client with a Music category; a proof Torznab indexer
(`t=music`) serving two hand-built torrents of generated, tagged FLAC (a bencode builder; the files were pre-placed in the
save path so the torrents completed at once); a Webhook, an Emby / Jellyfin and a CustomScript notification, the first two
pointed at small listeners that log each request. Real metadata: The Black Keys from the Lidarr metadata service (14 albums).
The real Knaben indexer also returned Music rows for an interactive search (`release/music?albumId=`), so the search path was
exercised against a live indexer.

| Flow | Observed |
|---|---|
| Add artist | `ArtistAdd` webhook; album and track rows refreshed |
| Grab (`release/music` after a search) | `Grab` webhook; Emby "Album Grabbed"; queue item with `artist`, `album`, track counts |
| Import and rename | 10 tracks imported with `renameTracks` on; `Retag` webhooks (10, one per track), Emby "Track Retagged" x10; one `Download` webhook and one Emby "Album Downloaded (10 Tracks Imported)"; `Rename` webhook |
| Library refresh after import | Emby `GET /Items?...includeItemTypes=MusicArtist&fields=Path,ProviderIds` (no `years`), then `POST /Library/Media/Updated` with `Created` for the mapped and the reported path |
| Album delete with files | `AlbumDelete` webhook; Emby "Album Deleted" ("Album removed and all files were deleted"); refresh posted as `Modified` |
| Failed download | queue item removed with `blocklist=true`: `DownloadFailure` webhook, one Music blocklist row, `downloadFailed` history; **Emby received nothing** (by design, matches Lidarr) |
| Regrab and reimport | `Grab`, then `Download` again |
| Artist delete | one `AlbumDelete` webhook and one Emby "Album Deleted" per album (14), then `ArtistDelete` / "Artist Deleted" |

The album events during an artist delete say "files were not deleted" while the artist event says "all files were deleted".
That is Lidarr's behavior, kept: `AlbumService.Handle(ArtistsDeletedEvent)` calls `DeleteMany`, which publishes
`AlbumDeletedEvent(album, false, false)` for each album (checked in the pinned tree).

**Findings from the live run (none fixed in this slice):**

1. **Hardlink import plus tag writing damages seeding.** With `copyUsingHardlinks` on and `writeAudioTags` set to `newFiles`,
   the retag rewrites the hardlinked file in place, so the seeding copy changes and qBittorrent's recheck drops from 100% to
   0%. Upstream parity (Lidarr has the same pairing). The default `writeAudioTags` is `no`, so a fresh install is not affected.
   Decision for the owner: leave as is, or make tag writing force a copy.
2. **`FpcalcCheck` warns permanently on a default install.** `allowFingerprinting` defaults to `newFiles` and `fpcalc` is not
   shipped, so the health page shows the warning as soon as a Music root folder exists. Either ship `fpcalc` or default
   fingerprinting to `never`.
3. **The `Retag` webhook payload has `trackFile.id` 0.** The path and tag changes are correct; the cause was not
   investigated.
4. **An empty album folder remains after an album delete with files.** The track files go; the folder stays.
5. **`DELETE /api/v3/blocklist/{id}` does nothing for a Music row** (it is the Movies route and answers without an error). The
   Music route is `DELETE /api/v3/blocklist/music/{id}`. The shared route should answer 404 for an id it does not own.
6. **The header search box has no Music entry.** It searches Movies and Series only (`ADD_NEW_KINDS` is `movies`, `tv`), so a
   typed artist name finds no library artist and offers no "add artist" suggestion. The Music add page is reached from the
   sidebar.

**Clean-up.** The proof objects were removed (artist with files, notifications, indexer, client, Music root folder, queue and
blocklist rows, torrents), `renameTracks` and `writeAudioTags` were put back, the owner's qBittorrent client and the Knaben
flags were restored, and the listeners and the throwaway qBittorrent stack were stopped. `RssSync` and `Housekeeping`
completed; the Movies baseline is unchanged at 71 / 57 / 64 and the health page is empty.

## Pre-importer fixes (session 58): as built

Three defects found by the session 57 review, fixed before the importer writes anything. Finding 2 of the Music 7i live run (above) is resolved here.

- **Series covers move to `MediaCover/Series/{id}`.** Movies wrote `MediaCover/{id}` and TV wrote the same flat folder, so a movie
  and a series with the same id shared `poster.jpg`, `fanart.jpg` and the resized variants and overwrote each other (ids 1 to 32
  collide in a library of 87 movies and 32 series). `Tv.Covers.MediaCoverService` builds both the file path and the URL from one
  constant, and `MediaCoverController` serves `Series/{seriesId}/{filename}`; the static `MediaCoverMapper` already serves any
  path under `/MediaCover/`. Movies (`MediaCover/{id}`) and Music (`Artists/`, `Albums/`) are unchanged.
  **No automatic migration:** the old files in `MediaCover/{id}` cannot be told apart from a movie's, so they are not moved. A
  series refresh (the scheduled refresh, or `RefreshSeries`) downloads the covers to the new folder; the old series files are
  harmless leftovers and are removed by hand. Fresh installs and the importer never create the old layout.
  `TvTests/Covers/MediaCoverServiceFixture` pins the URL, the download, resize and existence-check paths, and the delete.
- **`allowFingerprinting` defaults to `never`.** The image ships no `fpcalc`, and the Lidarr default `newFiles` made
  `FpcalcCheck` warn on every install that has a Music root folder. `FpcalcCheck` itself already passed when fingerprinting is off
  or no music library exists, so only the default changed; `never`, `newFiles` and `allFiles` stay selectable. An instance whose
  settings page was saved earlier keeps the value it stored.
- **A failed import says why.** The generic catch in `ImportApprovedMovie`, `ImportApprovedEpisodes` and `ImportApprovedTracks`
  now reports `Failed to import <kind>: <exception message>` (Music's permissions case keeps its wording and appends the
  message). The queue shows only this text, so a read-only file system or a denied path is visible without the log. The specific
  cases (root folder missing, destination exists, recycle bin) are unchanged.

Mutation checks (seven mutants, one per changed line, all killed): folder missing from the cover path, folder missing from the cover
URL, default back to `newFiles`, and the message dropped in each of the four catch sites.

`tools/liveproof/` starts here: a restore journal, and the checks `smoke`, `covers`, `baseline`, `defaults` and `fingerprint`
(see its README). The Movies, TV and Music flows move into it in later increments.

Found by the first walkthrough gallery (`tools/liveproof/gallery.py`) and fixed in the same session:

- **The localization dictionary is case-sensitive.** `LocalizationService` built it with `StringComparer.OrdinalIgnoreCase`, but
  `en.json` holds three pairs of keys that differ only by case (`RSSSync` and `RssSync`, `IMDbId` and `ImdbId`, `TBA` and `Tba`)
  and the frontend looks keys up at their exact case. The later key overwrote the earlier one, so one spelling of each pair was
  missing from `/api/v3/localization` and the UI showed the raw key (`RssSync` on the Movies and Series toolbars and the
  Calendar). The dictionary is now `StringComparer.Ordinal`. No C# or translated key differed from `en.json` by case alone,
  except two test lookups of `UILanguage` (the key is `UiLanguage`), which are corrected. `LocalizationServiceFixture` asserts
  that every key of `en.json` is served, at exact case.
- **Music media management drew "Root Folders" twice.** The page wrapped `MusicRootFolders`, which has its own fieldset, in a
  second fieldset with the same legend. Movies and Series were not affected.
- **The connection dialog's Tags help text named only movies** (`NotificationsTagsMovieHelpText`); it uses the new
  `NotificationsTagsHelpText`. The old key stays for the translations.

## Decision record

Owner answers, 2026-10-20, to the six questions in revision 1:

| # | Question | Answer | Effect on the spec |
|---|---|---|---|
| 1 | Shared or prefixed activity tables | Shared | As proposed: `History`, `Blocklist`, `PendingReleases`, `DownloadHistory` with `MediaKind`. |
| 2 | Quality and language parsing per module | Global by default, granular when wanted | Revised: global quality and language catalogs with Plattarr IDs, global profiles and custom formats with `AppliesTo`, importer and API remaps. Parsers stay per media type. |
| 3 | Compatibility URL bases | Compatible with most popular tools | Revised: full-fidelity Radarr v3, Sonarr v3, and Lidarr v1 APIs plus Prowlarr's per-indexer feeds. Reverses the Jellyseerr-only, captured-subset scope. |
| 4 | Per-module indexer settings | One global setting per indexer | Revised: global enable and seed settings, derived categories, optional advanced overrides only. |
| 5 | Migration chains | All in one | Revised: one chain, one `VersionInfo`. |
| 6 | Boundary enforcement | Claude's call | Architecture test with a shrinking allowlist. No separate module assemblies: with one schema, one UI, and no plugin loading, an assembly split adds build work and no user benefit. The test catches the same violations in CI. Revisit only for Phase 6 third-party modules. |

Guiding principle from the owner: Plattarr is a single pane of glass. See
[Single pane of glass](#single-pane-of-glass).
