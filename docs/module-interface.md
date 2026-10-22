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

Owner's setup: qBittorrent behind gluetun with one save path
(`/mnt/share/Media/Importing`). Separate categories per module are required
before TV is enabled, or routing falls to title parsing.

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
