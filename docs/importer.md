# Importer for `radarr.db` and `prowlarr.db`

Status: **design note; built so far: the adapter read layer, the pure building blocks,
the engine, Tags, profiles, download clients, Movies, TV, indexers, release profiles and
portable Config settings, plus the `/import` command and explicit `/skip=` handling/report**
(see [Read layer](#read-layer-built),
[Building blocks](#building-blocks-built), [Engine and stages](#engine-and-stages-built),
[Movie stages](#movie-stages-built), [Indexer stage](#indexer-stage-built) and
[TV stages](#tv-stages-built)). Release verification and the importer Postgres slice pass;
a separate boot-time verifier and boot test are not built. Decisions marked
*(reversible)* were made by Claude under the owner's standing delegation
("least technical debt later, most efficient") and can change without rework
elsewhere.

Goal: a user with running Radarr and Prowlarr containers starts Plattarr on an
empty `/config`, runs one command, and gets their movies, history, profiles,
indexers, clients and settings, without rebuilding the library. The upstream
containers are never modified and keep running until the owner cuts over.

Scope of this note: Radarr and Prowlarr, and since session 61 Sonarr (see
[From `sonarr.db`](#from-sonarrdb)). `lidarr.db` adds an adapter (see [Adapters](#adapters)).

## Facts this design relies on

Checked in the repo at `3d6984b`, not assumed:

| Fact | Where | Consequence |
|---|---|---|
| Plattarr has one migration chain (001 to 003). `PreviousSchemaCheck` refuses a database from another chain. | `Datastore/Migration/000_previous_schema_check.cs` | The importer must never open a source database through the migration runner. It reads the source with plain SQL. |
| Movie tables are `Movies_<upstream name>` with upstream's columns. Activity tables (`History`, `DownloadHistory`, `Blocklist`, `PendingReleases`) are shared and keyed by `MediaKind` (no default), `MovieId` is now `ParentId`, and `PendingReleases.ParsedMovieInfo` is now `ParsedInfo`. | 001, 002 | Movie-owned tables copy nearly column for column. Activity tables need explicit mapping. |
| Quality and language IDs are Plattarr catalog IDs everywhere. Two-way maps exist: `RadarrQualityIds`, `SonarrQualityIds`, `RadarrLanguageIds`, `SonarrLanguageIds`. | `Qualities/Upstream`, `Languages/Upstream` | Every stored ID, including those inside JSON, goes through these maps. They throw on an unknown upstream ID, which the importer reports as corrupt source data. |
| Provider rows (indexers, clients, notifications, lists, metadata) are stored with a `ConfigContract` class name. On start, `ProviderFactory.RemoveMissingImplementations` **deletes** every row whose implementation no longer exists and logs `Removing <name>`. | `ThingiProvider/ProviderFactory.cs:199` | A row the importer writes for a pruned provider would silently vanish on first start. The importer must detect these rows itself, skip them, and report them. |
| Download client category settings are stored as a `moduleCategories` list (migration 003). Migration 003's key map is private to a shipped migration. | `003_module_download_client_categories.cs` | The importer converts Radarr's old-shape client settings itself (see [Download clients](#download-clients)), with a test that pins it to migration 003's behavior. |
| Default quality profiles are created on `ApplicationStartedEvent` only when no profile exists. The default delay profile (`Order = int.MaxValue`) is inserted by migration 001. | `QualityProfileService.cs:134`, 001 | Importing into a migrated database that has never started leaves no defaults to collide with. The delay profile row is updated, not inserted. |
| Indexers carry `EnableRss`, `EnableAutomaticSearch`, `EnableInteractiveSearch` as columns; `MinimumSeeders` is in the torrent base settings (Prowlarr's `AppMinimumSeeders`, renamed). | `Indexers/IndexerDefinition.cs`, `IndexerTorrentBaseSettings.cs` | Prowlarr's per-app sync profile collapses into these four values. |
| `StartupContext` parses `/key=value` into a dictionary (`Add`, so a repeated key throws) and treats anything with more than one `=` as a flag. | `NzbDrone.Common/EnvironmentInfo/StartupContext.cs` | Command-line options stay simple. Path maps and similar go in a JSON options file. |
| The Docker entrypoint is `exec /app/bin/Radarr -nobrowser -data=/config "$@"`. | `docker/entrypoint.sh` | Extra container arguments reach the binary, so `docker compose run --rm plattarr /import ...` works without a new entrypoint. |

Checked against a dump of the owner's databases on 2026-10-21: see
[Dump findings](#dump-findings). Upstream column names not listed there still come
from Radarr and Prowlarr `develop` as remembered and are confirmed against the dump
before the code that uses them is written.

## How it runs

```
docker compose run --rm plattarr /import /radarr=/import/radarr.db /prowlarr=/import/prowlarr.db /options=/import/options.json
```

- **Utility mode, not a web feature (first version).** `ApplicationModes`
  gains `Import`, routed in `Bootstrap` before the web host starts. It runs the
  migrations on the target (`-data`), runs the import, writes the report, and
  exits. The engine lives in Core (`NzbDrone.Core.Upstream`), so a UI wizard or
  a command in Phase 3 calls the same code.
- **Arguments:** `/radarr=<path>`, `/prowlarr=<path>`, `/sonarr=<path>` (any may be omitted, not all),
  `/options=<json file>`, `/skip=<app:table,...>`, `/dry-run`, `/report=<dir>` (default `/config/import-report`).
  Each source path is a `.db` file or a backup `.zip` (upstream's
  `radarr_backup_*.zip` contains `radarr.db`; this is the easiest input while
  the upstream container keeps running).
- Every non-empty source table with no import stage must appear in `/skip=` for both
  dry-run and committed imports. Use source app and table names, for example
  `/skip=Radarr:Notifications,Sonarr:History`. Entries for a table with a stage, a table
  not read from a supplied source, or a duplicate entry are errors. Tables the adapters
  count without reading are already reported by design and do not need `/skip=`.
- **Options file (all optional):**

  ```json
  {
    "pathMap": { "/movies": "/data/media/movies", "/downloads": "/data/torrents" },
    "strict": false,
    "allowNewerSource": false
  }
  ```

  `pathMap` rewrites path prefixes (see [Paths](#paths)). `strict` makes any
  skipped row a failure. `allowNewerSource` lets a source database newer than
  the supported range through (off by default).
- **Exit codes:** 0 done (skips are in the report), 1 failed and nothing was
  written, 2 `strict` and something was skipped (nothing written).
- **First-run option (later, not in v1):** a first-start wizard that asks for the
  two files. It would call the engine; nothing in this design changes.

### Safety rules

1. **Target must be empty.** The importer refuses unless the target has no movies,
   indexers, clients, notifications, quality profiles, custom formats or tags, and
   has not recorded a previous import (`Config` key `ImportedFrom`). To retry, delete
   the target config and start over. *(reversible: a merge-into-existing mode is
   possible later but multiplies the test matrix.)*
2. **Sources are read-only copies.** The importer copies `<name>.db` and its `-wal`
   and `-shm` files (or unzips the backup) into a scratch directory and opens the copy
   read-only. It never opens a live upstream database in place.
3. **One transaction.** All writes happen in a single transaction on the target
   (SQLite or Postgres). Any error, a failed verification, or `/dry-run` rolls it back.
   A dry run therefore exercises the real target schema and returns the same report.
4. **No `ApplicationStartedEvent`.** Utility mode does not start the application, so
   default profiles and quality definitions are not seeded before the import. The first
   normal start fills in whatever the import did not provide, as it does today.
5. **Nothing on disk is touched.** Media files, torrents and upstream config stay as
   they are. Cover images are not copied (see [Known costs](#known-costs)).

### The command (built)

`/import` runs today over the stages that exist (tags, profiles, clients, indexers, root folders,
portable settings, and the Movies and TV tables). Built as designed: `ApplicationModes.Import` is selected by
the `/import` flag in `Bootstrap.GetApplicationMode`, `Bootstrap.RunUtilityMode` resolves
`ImportCommand` (Host) instead of the router, and `UpstreamImportRunner` (Core) copies each
source, reads it through its adapter, runs `UpstreamImporter` and writes the report. The process
exits with the code of the run through `Bootstrap.UtilityExitCode` (`ConsoleApp` calls
`Environment.Exit` when it is not 0).

- **Every unclaimed table needs an explicit `/skip=` entry.** The command refuses either run
  when a non-empty source table has no stage and is absent from the list. An accepted entry is
  reported as "Deferred by the owner" and participates in reconciliation as skipped rows; it
  is not a row-level `ImportSkip`, so strict mode does not treat this explicit scope choice as
  an unexpected loss. The report still shows count-only tables separately.
- **Report files** (`<data>/import-report`, or `/report=`): `import-<UTC timestamp>.json` and
  `.txt`, plus `import-idmap-<timestamp>.csv` for a committed run only (after a rollback the
  target IDs name rows that do not exist). Sources appear by file name, schema version and SHA-256
  of the copy, never by path.
- **The console prints counts and reason codes only.** Titles and provider names (the skip
  list) are in the report files, so a run in a shared terminal or CI log leaks no library listing.
- `DryRun` needs a migrated, empty target: point `-data` at a scratch folder and the run
  migrates it, imports inside one transaction and rolls back.

## Pipeline

```
 source file ──► adapter.Read ──► SourceSnapshot (typed rows, upstream IDs)
                                         │
 (one per app)                           ▼
                              Planner: remap IDs, merge, skip
                                         │
                                         ▼
                           ImportPlan (target rows, IdMap, Skips, Warnings)
                                         │
                     ┌───────────────────┴───────────────────┐
                     ▼                                       ▼
              Writer (one transaction)               Report (JSON + text)
                     │
                     ▼
              Verifier ─ fail ─► rollback
```

- **Adapter** (one per source app): opens the copy, checks the version, reads each
  table into typed source rows. No remapping, no writes.
- **Planner** (shared): builds the target rows. Owns the `IdMap` (source table and ID
  to target ID), the merge rules, and the skip list. The pure parts (path mapping, tag
  merge, catalog remap, client and indexer matching, event types, release conversion)
  are unit-testable without a database; see [Building blocks](#building-blocks-built).
  **The planner is staged, not one pure function.** Repositories reject a row with a
  non-zero ID and assign IDs themselves, so a target ID for tags, profiles, clients,
  indexers and movies exists only after the writer inserts them. Each stage plans its
  rows from the `IdMap` the earlier stages filled, the writer inserts them and records
  the new IDs (`IdMap.Record`), and the next stage plans. Nothing is written outside the
  one transaction, so a failed stage still rolls everything back.
- **Writer**: inserts through the existing repositories so the table mapper and its
  converters (`QualityIntConverter`, `LanguageIntConverter`) apply and Postgres behaves
  the same as SQLite. **It must use the caller-transaction overloads**
  `InsertMany(models, connection, transaction)` and `UpdateMany(models, connection,
  transaction)` (see [Target-side pieces](#target-side-pieces-built)). The plain
  `InsertMany` opens its own connection and commits at once, so a design built on it
  cannot roll back. Order is fixed by dependencies (see [Write order](#write-order)).
- **Verifier**: runs inside the transaction after the writes, with plain SQL on the
  import's own connection. A repository call (`All()`, `Count()`) opens another
  connection that cannot see uncommitted rows. Any failure rolls back (see
  [Verification](#verification)).

**IDs are not preserved.** Repositories assign new IDs, and merging forces a remap for
tags, profiles, clients and indexers anyway; remapping everything keeps one mechanism.
The report includes the `IdMap` as CSV so a tool that cached a Radarr movie ID or profile
ID can be translated. *(reversible; the compatibility APIs, if built, can read the map
from a stored table. Revisit if a tool turns out to depend on Radarr's IDs.)*

## Adapters

```csharp
public interface IUpstreamAdapter
{
    string AppName { get; }              // "Radarr", "Prowlarr"
    VersionRange SupportedVersions { get; }
    SourceSnapshot Read(IDbConnection source, ImportOptions options);
}
```

Adding Sonarr or Lidarr later means implementing this interface, adding its
`MediaKind` handling to the planner's activity mapping, and adding fixtures. The
planner's merge rules (tags, profiles, custom formats, clients, settings) already
treat a source as one of N.

**Version gate.** The adapter reads `MAX(Version)` from `VersionInfo`. Below
`SupportedVersions.Min` or above `Max` it fails with the version it found, unless
`allowNewerSource` is set (then it warns). The range is filled in from the real
databases (see [Facts](#facts-this-design-relies-on)), not guessed.

## Read layer (built)

`NzbDrone.Core.Upstream`: `IUpstreamAdapter`, `UpstreamAdapterBase` (version gate,
tolerant table reader, counts), `RadarrAdapter`, `ProwlarrAdapter`,
`UpstreamSourceCopy` (private read-only copy, WAL files, backup zips), `SourceRow`,
`SourceSnapshot`, `ImportOptions`, `SkipReason`.

Where it differs from the sketch above:

- **Rows are `SourceRow`, not one class per table.** A row is keyed by column name
  (case-insensitive) and an absent column is not an error, so an older schema reads with
  the columns it has. Typed rows are the planner's job, table by table. Mandatory
  columns throw (`GetInt`, `GetBool`), optional ones return null. Error messages name
  the column, never the value.
- **`IUpstreamAdapter.DatabaseFileName`** was added so a backup zip can be searched for
  `radarr.db` or `prowlarr.db`. Only the entry's name is used, never its stored path.
- **Tables that are not read are counted.** `SourceSnapshot.SkippedTables` holds the row
  count and reason code per table, so the reconciliation (rows read = written + skipped)
  covers every table. A counted table the source lacks is not listed.
- **Version range** is `1` to the dumped version (Radarr 242, Prowlarr 44). The floor is
  not a tested lower bound.
- **Prowlarr `Config` is counted, not read** (reason `INSTANCE`). See open item 6.

Checked against the real schemas (`src/NzbDrone.Core.Test/Files/Upstream/`, DDL only,
no rows, byte-identical to the dump): every table in `radarr.db` (41) and `prowlarr.db`
(20) is either read or counted, and a test fails when one is neither. Two tables the
note lists do not exist in these databases: Radarr `Logs` and `UpdateHistory` (logs live
in `logs.db`). Other findings from the schemas:

- **Prowlarr `DownloadClients` has a different column set** from Radarr's: `Categories`
  (JSON, default `[]`, Prowlarr's mapping of indexer categories to client categories)
  and no `Tags`, `RemoveCompletedDownloads` or `RemoveFailedDownloads`. The copy list in
  [Download clients](#download-clients) holds for Radarr clients only; a Prowlarr-only
  client has no tags or removal flags to carry.
- **Radarr `Indexers.EnableRss` and `EnableAutomaticSearch` are nullable**
  (`EnableInteractiveSearch` is not). The planner must treat NULL as "not set".
- **Prowlarr `Notifications` has no per-movie event columns** and adds
  `IncludeManualGrabs`, which Radarr's table lacks.

## Building blocks (built)

Pure pieces the planner stages are made of. None of them touches a database. Each was
shown to fail its tests when its rule is broken (mutation checks).

Core, `NzbDrone.Core.Upstream`:

| Type | Rule |
|---|---|
| `IdMap` | `Record(app, table, sourceId, targetId)` throws if a source row is already mapped to a different target. Several sources may share one target (merged tags). `Resolve` throws `UpstreamImportException` for an unmapped row; `TryResolve` does not. `ToCsv()` is the report's map. |
| `PathMapper` | Longest prefix wins, whole path segments only, case-sensitive. Separators are restyled to the target's. A duplicate prefix throws. |
| `TagMerger`, `TagRemapper` | Merge by trimmed label, case-insensitive; the first-seen spelling is kept. `Remap` drops a tag ID with no imported row and reports the count; a non-array or non-integer value is `CorruptSourceDataException`. |
| `CatalogRemapper`, `IUpstreamCatalog` | Rewrites the integer `quality` and the language ID arrays to catalog IDs. An ID the app never had is corrupt data. |
| `DownloadClientSettingsConverter` | Migration 003's key map for the clients that exist (qBittorrent, SABnzbd). An import and an in-place migration give the same text; `UpstreamClientSettingsMigrationFixture` compares them. |
| `DownloadClientMatcher` | Same client = implementation, host, port, SSL flag and URL base. |
| `IndexerMatcher` | A Torznab or Newznab URL ending in a Prowlarr indexer ID, plus a Radarr application in Prowlarr, plus, when Prowlarr has sync records for Radarr, a record pairing the two indexers (`Confirmed`). Another app's sync record never confirms a match. |
| `ImportSkip`, `CorruptSourceDataException` | Skip entry for the report; bad source data. Messages name the column or table, never a value. |

Movies module, `NzbDrone.Core.Movies.Upstream` (these use movie vocabulary, so they
are module code; `ModuleBoundaryFixture` fails if a core type references them):

| Type | Rule |
|---|---|
| `RadarrEventTypes` | Explicit maps from Radarr's `History.EventType` and `DownloadHistory.EventType` integers to Plattarr's enums, pinned to Radarr 56f6168. An unknown value is not mapped. |
| `ReleaseInfoConverter` | Converts `DownloadHistory.Release` JSON: remaps `indexerId` through a callback, converts languages to names, converts the removed `indexerFlags` number into the flag list. In the owner's rows `languages` is always empty and `indexerFlags` is absent; the other shapes are handled because the code can produce them. |

**Module seam.** The planner for the activity tables is core code but needs the
Radarr-specific, movie-vocabulary pieces above. The contract the Movies module
registers for the core planner is not designed yet. Design it together with the
activity planner against its real caller, not ahead of it.

## Target-side pieces (built)

Found while starting the writer. Both are in core and used by the importer only for now.

- **Caller-transaction repository overloads.** `BasicRepository` gained
  `InsertMany(models, connection, transaction)` and `UpdateMany(models, connection,
  transaction)`, on `IBasicRepository<T>`. They assign IDs like `InsertMany`, commit
  nothing and raise no event. The importer opens one connection, begins one
  transaction, passes both to every repository, and commits only after the verifier
  passes. Tests (`CallerTransactionRepositoryFixture`, SQLite and Postgres): IDs are
  assigned, rows from two different repositories roll back together, a model with an ID
  is refused. Shown to fail when the overload commits on its own. Two consequences:
  no other connection may write meanwhile (SQLite allows one writer, and a repository
  call would hit `Busy` and retry), and any read the planner or verifier needs from the
  target is plain SQL on the import's connection.
- **`TagUsage.Tables`** (`NzbDrone.Core.Tags`): the list of tables with a `Tags` column,
  extracted from `CleanupUnusedTags`, which now reads it. `TagUsageFixture` compares it
  with the real table mapping, so a new tag-using table fails a test. Metadata consumers
  (`Movies_Metadata`) are mapped with `Tags` ignored and have no column, so they are
  correctly absent; adding them breaks the housekeeper (`no such column`), which the
  first version of the test caught.

## Engine and stages (built)

`NzbDrone.Core.Upstream`. This is the staged planner and writer the pipeline describes, as
code the rest of the stages plug into.

| Type | Role |
|---|---|
| `IImportStage` | `Name`, `Order`, `Tables` (the `SourceTableRef(app, table)` pairs it accounts for), `Run(session)`. A stage plans from the `IdMap`, writes with the repositories' caller-transaction overloads, records the new IDs, and reports rows with `session.Imported` and `session.Skip`. `ImportOrder` holds the position constants (gaps left on purpose); `UpstreamApps` the app names. |
| `ImportSession` | The connection and transaction the importer owns, the snapshots, `ImportOptions`, `IdMap`, `PathMapper`, skips, warnings, and the per-table imported counts. |
| `ImportReconciliation` | Rows read = imported + skipped for every table a stage claims or the owner explicitly defers, per app. A table counted but not read (instance and runtime state) is reported by its reason and never checked. An unbalanced table throws `ImportVerificationException`, which names tables and counts and no value. |
| `UpstreamImporter` (`IUpstreamImporter`) | Refuses two databases of one app, invalid `/skip=` selections and a target that already holds data (`Movies_Movies`, `Indexers`, `DownloadClients`, `Notifications`, `QualityProfiles`, `CustomFormats`, `Tags`) or an earlier import (Config key `importedfrom`). Runs the stages by `Order`, reconciles, then commits, or rolls back for a dry run or for strict mode with a skipped row. Any exception rolls back. `ImportResult` carries the outcome, per-table accounts, deferred tables, unread tables, skips, warnings and the `IdMap`. |
| `TagsStage` (core) | Writes the label union (`TagMerger`) and fills the tag `IdMap`. |
| `ProfilesStage` (core) | Radarr `CustomFormats`, `QualityProfiles`, `QualityDefinitions` and `DelayProfiles` in that order inside one stage (each reads the `IdMap` the one before filled). Documents are rewritten as JSON and parsed by the same converters the repositories use, so a shape the target cannot read skips that one row as `CORRUPT`; the report names the table, ID and column, never a value. Language conditions, quality items, cutoffs and profile languages go through the app's catalog; group IDs (1000 and up) are profile-local and kept; a score for a custom format that was not imported is dropped with one warning per profile; delay profile tags go through the tag map. The target's default delay profile (the one row migration 001 inserts) is updated in place by the source's default row; all other delay profiles are inserted. `AppliesTo` is `["Movies"]`. |
| `DownloadClientsStage` (core) | Merges matching Radarr and Sonarr clients by endpoint, maps a matching Prowlarr copy to the same target ID, and skips a Prowlarr-only client because it has no media type. Converts each source app's category keys, remaps tags, and calls `IModuleCategorySettings.EnsureEntries` for Movies, TV and Music on every client it writes. |
| `ReleaseProfilesStage` (core) | Imports Radarr and Sonarr release rules after indexers and tags so assigned indexer and tag IDs can be remapped. An unavailable assigned indexer makes that profile a reported `UNMATCHED_INDEXER` skip. |
| `ConfigStage` (core) | Imports the allowlisted portable media-loop settings from Radarr and Sonarr. Other config rows are reported as `INSTANCE`; conflicting shared values keep Radarr's value and report the Sonarr row as `CONFIG_CONFLICT`. |
| `SourceDocuments`, `RowBatch<T>` (core) | Helpers every stage shares. `SourceDocuments.IsBad` says which exceptions mean "this one document is unreadable" (a stage skips the row as `CORRUPT`; anything else fails the run) and `Describe` words them without a value. `RowBatch` collects models with their source IDs and writes them with the caller-transaction `InsertMany`, then records every new ID in the `IdMap` and counts the rows imported. |
| `RootFoldersStage` (Movies module, `NzbDrone.Core.Movies.Upstream`) | Radarr `RootFolders` to `Movies_RootFolders` through the path map. Two folders that map onto one path become one row (both counted as imported). A folder with no path is skipped as `CORRUPT`. Nothing refers to a root folder by ID, so its `IdMap` entries serve the report only. |

The movie stages (`ImportExclusionsStage`, `MovieMetadataStage`, `MoviesStage`, `MovieExtrasStage`) are described under [Movie stages](#movie-stages-built).

**The module seam is a stage.** Movie-vocabulary mapping lives in stages in the Movies module;
the engine discovers every registered `IImportStage` and knows nothing about movies. This is
the contract the note left open: it needs no registration beyond implementing the interface,
and a TV or Music module adds its own stages without touching core.

**Coverage cannot rot.** `ImportStageCoverageFixture` lists, per app, the tables no stage claims
yet. The list can only shrink: a table a stage now claims fails the test until it is removed
from the list, and a name that is not a table the adapter reads fails it too. A stage that
claims a table the adapters do not read, or two stages that claim one table, also fail.

**Strict means an unexpected skipped row, not an explicit owner deferral or unread table.** Strict mode fails on any `ImportSkip`; tables listed in `/skip=` are deliberate and are not included in that list.
Tables the adapters only count (`Commands`, `PendingReleases`, `Config` of Prowlarr...) are
skipped by design and would make every run fail.

Tests (`UpstreamImporterFixture`, `ImportReconciliationFixture`, SQLite and Postgres): the
union of tags and their `IdMap`, the per-table accounts, path mapping, merged and skipped root
folders, dry run and strict mode write nothing, a later stage's failure rolls back an earlier
stage's rows, stage ordering, a stage that loses rows fails the run, and each refusal. Each
rule was broken in turn and shown to fail its test (five engine rules, three stage rules).

Gaps found while building these, to close before the writer:

- `AutoTagging.Specifications` holds tag IDs inside `TagSpecification` and needs the same
  remap as a `Tags` column.
- Fixtures are written by hand from documented shapes until `tools/upstream-fixtures`
  exists.

### Movie stages (built)

`NzbDrone.Core.Movies.Upstream`. Four stages in `ImportOrder` positions 550 to 800: metadata
before movies, movies before the files that hang off them.

| Stage (order) | Tables | Rules |
|---|---|---|
| `ImportExclusionsStage` (550) | `ImportExclusions` | `TmdbId`, title and year copied; a NULL year becomes 0 (the model's `MovieYear` is a non-nullable int). Refers to nothing else. |
| `MovieMetadataStage` (600) | `MovieMetadata`, `AlternativeTitles`, `MovieTranslations`, `Credits`, `Collections` | All metadata is imported, including titles no movie uses (it is a lookup catalog). `OriginalLanguage` and translation `Language` go through the catalog; `Images`, `Genres`, `Keywords`, `Ratings`, `Recommendations` are parsed by the target's own converters. NULL `Year`, `CollectionTmdbId` and `Popularity` become 0. A child row whose metadata was skipped is skipped too. Collections resolve `QualityProfileId` (skipped when the profile was not imported), map `RootFolderPath`, remap `Tags` (dropped ones give one warning per collection), and take the import time as `Added` when the source has none. |
| `MoviesStage` (700) | `Movies`, `MovieFiles` | A movie needs its metadata, its quality profile and a non-empty path, otherwise it is skipped as `CORRUPT` and named by its title. Movies are inserted with `MovieFileId` 0, then files with the new movie IDs, then `Movies_Movies.MovieFileId` is set with plain SQL on the import's connection and transaction (a repository call opens another connection and cannot see the rows). A file whose movie was skipped is skipped; a movie whose file was skipped is imported without a file and the next disk scan finds the file again, with no warning because the skip is already in the report. A movie that points at a file that is not in Radarr's database at all gets one warning. `Quality` and `Languages` go through the catalog; `MediaInfo` is parsed as is (NULL stays NULL); `IndexerFlags` is copied. |
| `MovieExtrasStage` (800) | `SubtitleFiles`, `ExtraFiles`, `MetadataFiles` | `MovieId` resolves through `Movies`; `MovieFileId` through `MovieFiles` when it is above 0. 0 (or NULL for metadata files) stays as it was. A row whose movie or file was not imported is skipped as `CORRUPT`, because the target has no foreign keys and a stale ID would attach the row to whatever later takes it. Subtitle `Language` goes through the catalog. `Added` falls back to the import time and `LastUpdated` to `Added`. Metadata files keep their `Consumer` class name and `Type`. |

**Bad documents skip one row.** `SourceRow.GetJson` throws `CorruptSourceDataException` (a
subclass of `UpstreamImportException`) for text that is not valid JSON, so every stage skips
that row instead of failing the run. This also changed `ProfilesStage`: a profile or format
with malformed JSON used to abort the import and is now skipped and reported. A missing
mandatory column still fails the run, and messages name the column, never the value.

**Dates.** `SourceRow.GetNullableDateTime` and `GetDateTime` read a `DateTime`,
`DateTimeOffset` or text and always return UTC: a value with no zone is taken as UTC (Radarr
writes UTC), a value with an offset is converted. Text that is not a date throws without
echoing it. The target keeps microseconds on Postgres, so the seventh fractional digit Radarr
writes is dropped there. **The adapter reads `DATETIME` columns as the stored text.** The SQLite
driver otherwise converts them itself and, for text ending in `Z`, shifts the value to the
machine's local time without a zone; on a server not set to UTC every date would have moved by
the offset. Radarr 242 writes `YYYY-MM-DD HH:MM:SS.fffffffZ` (five to seven fractional digits,
or none for release dates).

**Deferred.** `ImportListMovies` waits for the import list stage, which needs the settings
and provider work.

**Checked against the owner's database (session 31, read-only, shapes only).** Dates, `Images`
(`coverType` string, `remoteUrl`), `Ratings` (`type` `user`, 1 to 5 providers), `MediaInfo` (22
key-set variants, `runTime` as `hh:mm:ss.fffffff`, `videoHdrFormat` as camel-case names such as
`dolbyVisionHdr10`), `Quality`, `Languages`, `Genres`, `Keywords`, `Recommendations`, `Tags` and
`LanguageTags` all parse; none was malformed. `MovieStagesFixture` has a test with those shapes
(values replaced). Not yet run: the stages over a copy of the real database, which is the
cutover rehearsal.

Tests (`MovieStagesFixture`, `SourceRowFixture`, `ProfilesStageFixture`; SQLite and
Postgres): each rule above, with a read-back through the target repositories. One test pins
that the target allows two metadata rows to share an alternative title `CleanTitle`. Each
rule was broken in turn and shown to fail its test (14 mutations, all caught).

### Indexer stage (built)

`IndexersStage` (core, `ImportOrder.Indexers` = 300, after profiles, before anything that stores
an indexer ID). It claims Prowlarr `IndexerProxies`, `AppSyncProfiles`, `Indexers`, `Applications`
and `ApplicationIndexerMapping`, and Radarr `Indexers`. Built in session 60 against the owner's
live Prowlarr 44 and Radarr 242 shapes (key sets read from a fresh backup-API copy, no values).

| Source | Rule |
|---|---|
| Prowlarr `IndexerProxies` | Written to `IndexerProxies` when Plattarr has the implementation (FlareSolverr, Http, Socks4, Socks5); otherwise `UNKNOWN_PROVIDER`. Tags through the tag map. A proxy without tags stays inert, as in Prowlarr. |
| Prowlarr `Indexers` | Implementation must be an exported `IIndexer` class (the set the factory gets) and `ConfigContract` must resolve through `ProviderConfigTypes.Find`; otherwise `UNKNOWN_PROVIDER`, because `RemoveMissingImplementations` would delete the row on the first start. `Settings` is rewritten (below), then parsed into the target settings type with the options the repository writes with (`ProviderSettingConverter.Parse`); a document the type cannot read skips the row as `CORRUPT`. `Enable`, `Priority`, `Redirect`, `Added` are copied (NULL `Added` takes the import time); `Tags` through the tag map; `DownloadClientId` is written as 0 with a warning when the source set one (the client stage, which runs later, is not built). `AppProfileId` is dropped. |
| Prowlarr `AppSyncProfiles` | Folded into each indexer: `EnableRss`, `EnableAutomaticSearch`, `EnableInteractiveSearch` come from the indexer's profile. A profile used by an imported indexer counts as imported; an unused one is skipped as `OBSOLETE`. An indexer whose profile is missing gets all three flags on and a warning. |
| Prowlarr `Applications`, `ApplicationIndexerMapping` | Read to match Radarr's indexers, never written. Marked **not imported by design** (`ImportSession.NotImportedByDesign`): the report lists them with the count-only tables, the reconciliation leaves them out (and fails if a stage counted rows of them), and strict mode does not fail on them. |
| Radarr `Indexers` | Matched with `IndexerMatcher` (now parameterized by the application implementation, so Sonarr reuses it). A match records Radarr indexer ID to the Prowlarr indexer's target ID in the `IdMap` and counts as imported; no row is written. No match: `UNMATCHED_INDEXER` with the indexer's name and the matcher's reason, **never the URL** (a hand-added RSS URL can carry a passkey; this changes the earlier "name and URL" line). A match whose Prowlarr indexer was skipped: `UNMATCHED_INDEXER`. No Prowlarr database: every Radarr indexer is `UNMATCHED_INDEXER`. |

**Settings rewrite** (`IndexersStage.PrepareSettings`, torrent settings types only for the first two):

- **F6, resolved from Prowlarr's code** (`develop` at `71c5435`): the stored key is
  `torrentBaseSettings.appMinimumSeeders` (camelCase of `AppMinimumSeeders`, written only when
  set), and Prowlarr pushes `AppMinimumSeeders ?? AppProfile.MinimumSeeders` to each app
  (`Applications/Radarr/Radarr.cs`). The stage renames the key to `minimumSeeders`; when it is
  absent, it writes the sync profile's value explicitly, because Plattarr's default (1) would
  otherwise apply silently. A `minimumSeeders` key already present wins.
- `rejectBlocklistedTorrentHashesWhileGrabbing`: a per-application setting in Prowlarr
  (`syncRejectBlocklistedTorrentHashesWhileGrabbing`) written into every synchronized indexer.
  Plattarr keeps it on the indexer: the matched Radarr copy's value, else the Radarr
  application's setting, else Plattarr's default (off).
- `baseSettings.failDownloads`: Radarr's own setting (Prowlarr's sync never writes it), kept on
  the indexer in Plattarr. Taken from the matched Radarr copy when Prowlarr's document has none.
- After parsing, every source key that did not survive the round trip is reported by name in
  one warning per indexer or proxy ("settings Plattarr does not have were not carried: ...").
  Never a value: settings hold API keys, cookies and passwords.

**Radarr's copies** (`IndexersStage.MovieOverride`): Prowlarr's sync overwrites `baseUrl`,
`apiKey`, `categories`, `minimumSeeders`, the seed criteria and the reject flag on every full
sync, and writes `multiLanguages`, `removeYear` and `requiredFlags` only on the first sync, so
those three are the user's. One `IndexerOverrides` row (`MediaKind` Movies) is written when the
copy sets any of: `multiLanguages` (through the Radarr language catalog), `requiredFlags`
(copied, Radarr's flag values are the global vocabulary), `removeYear` true, or a category outside
the Radarr application's `syncCategories` (2000 to 2999 when the application row has none). A
second copy of one Prowlarr indexer keeps the first copy's override (confirmed match first, then
the lowest Radarr ID) and warns. *(reversible)*

**Reported, not carried:** RSS / automatic / interactive flags on a Radarr copy that differ from
what Prowlarr pushes (`Enable && profile flag`; Prowlarr wins, there is one setting per indexer);
tags on a Radarr copy (they limited the indexer to tagged movies; Plattarr's indexer tags are
Prowlarr's); a download client chosen on a Radarr copy; a match by URL only (no sync records).

**Cardigann.** One warning lists every imported Cardigann indexer with its definition ID: the
definitions are downloaded on the first start, and a custom definition (a `.yml` in Prowlarr's
`Definitions/Custom`) must be copied to Plattarr's `Definitions/Custom` (supported by
`IndexerDefinitionUpdateService`). The owner has one: `torrentdownload-server01`.

**Dry run over protected source copies** (2026-10-25, image `sha-28319e9`, backup-API copies of
`prowlarr.db` (schema 44) and `radarr.db` (242), imported into scratch `/config`): Prowlarr `Indexers`
17 of 17, `IndexerProxies` 1 of 1, `AppSyncProfiles` 1 of 1, Radarr `Indexers` 13 of 13 matched, no
skip; no settings key lost in the round trip; no flag difference (Radarr's four all-off copies
mirror four indexers disabled in Prowlarr); one warning, the Cardigann list. `Applications` (4)
and `ApplicationIndexerMapping` (37) are listed as not imported by design. Step 4 adds the
download-client stage; other non-empty unstaged tables need explicit `/skip=` entries on both
dry-run and committed imports.

Tests (`IndexersStageFixture`, 18, SQLite and Postgres; `ImportReconciliationFixture` gained 2):
each rule above with a read-back through the target repositories, and no warning or skip
detail contains the fixture's API key or a URL. 27 mutants (session 60), all killed.

### TV stages (built)

`NzbDrone.Core.Tv.Upstream`, implemented in session 61 and verified in session 62. Built against the Sonarr 4.0.20 schema
(`sonarr-217.schema.sql`, from a fresh install of that release) and checked against the
owner's live database (schema 217, same release, read-only, counts and flag values only).

| Stage (order) | Tables | Rules |
|---|---|---|
| `TvRootFoldersStage` (510) | `RootFolders` | `Path` through `pathMap`; two source rows that map to one path become one row. An empty path is `CORRUPT`. |
| `SeriesStage` (710) | `Series`, `EpisodeFiles`, `Episodes` | A series needs a resolved quality profile (the Sonarr column is nullable) and a non-empty path, otherwise it, its files and its episodes are skipped as `CORRUPT`. `Seasons` (with each season's monitored flag), `Images`, `Ratings`, `Genres`, `Actors` are parsed by the target's converters; `OriginalLanguage` through the catalog; `Language` is English and `SeasonType` `official` (the target's defaults, Sonarr has neither). Unreadable `AddOptions` are dropped with a warning. Files: `Quality` and `Languages` through the catalog, `IndexerFlags` through `SonarrIndexerFlags.ToGlobal` (unknown bit: `CORRUPT`), `ReleaseType` copied; unreadable `MediaInfo` becomes NULL with one counted warning (the TV module re-reads media info when it is NULL). Episodes keep their own `Monitored` flag (NULL is false); `EpisodeFileId` resolves through the file map or becomes 0, and an episode that points at a file Sonarr does not have gets one counted warning. Skipped episodes are named `<series> SxxEyy`. |
| `TvExtrasStage` (810) | `SubtitleFiles`, `ExtraFiles`, `MetadataFiles` | As `MovieExtrasStage`, with `SeriesId` and `EpisodeFileId`; `SeasonNumber` copied; subtitle `Language` through the catalog. |

TV has no metadata stage: Sonarr keeps series metadata on the `Series` row.

**Indexers.** `IndexersStage` reads Sonarr's synchronized copies too (one `AppCopies` per app).
A TV override (`IndexersStage.TvOverride`, `MediaKind` Tv) carries `multiLanguages` (Sonarr
catalog), `requiredFlags` (Sonarr bits translated), categories outside the Sonarr application's
`syncCategories` (default 5000 to 5999) and the `SeasonSearchMaximumSingleEpisodeAge` column when
above 0. Anime categories that Prowlarr did not sync and `animeStandardFormatSearch` are reported,
not carried. Without Prowlarr every Sonarr indexer is `UNMATCHED_INDEXER`.

**Live Sonarr source snapshot (2026-10-25, read-only):** 32 series, 3170 episodes, 1488 episode files,
30 subtitle files, 6 quality profiles (default names), 1 custom format (no indexer-flag
condition), 14 Torznab indexers (no `requiredFlags`), file flags 0 and 1 only. No series without a
profile, no episode with a NULL `Monitored`. F4 does not affect this data.

Tests (`SonarrStagesFixture`, the two-source tests in `ProfilesStageFixture`, four Sonarr tests in
`IndexersStageFixture`, the Sonarr cases in `UpstreamRealSchemaFixture`,
`ImportStageCoverageFixture` and `ImportRequestFixture`): run in the full Release suite and the
importer Postgres slice. A mutation check confirmed the episode monitored flag assertion catches
an importer that forces episodes unmonitored. Step 3's dry-run is recorded in the project state.

## Mapping

"Remap" means every quality or language ID goes through the upstream maps, including
IDs inside JSON. "Skip" means the row is not written and appears in the report with a
reason code.

### From `radarr.db`

| Source table | Target | Notes |
|---|---|---|
| `Movies` | `Movies_Movies` | `QualityProfileId`, `MovieMetadataId` and `Tags` via `IdMap`. `MovieFileId` is written as 0 first and set after `MovieFiles` is inserted (the two reference each other). `Path` through `pathMap`. |
| `MovieMetadata` | `Movies_MovieMetadata` | `OriginalLanguage` remapped. Other columns and JSON (`Images`, `Genres`, `Ratings`, `Recommendations`) copied. |
| `MovieFiles` | `Movies_MovieFiles` | `Quality` (JSON) and `Languages` (JSON) remapped. `IndexerFlags` is copied as is: Radarr's flag values are the global vocabulary (plan, finding 6). `MediaInfo`, `SceneName`, `RelativePath`, `Edition`, `OriginalFilePath` copied. |
| `AlternativeTitles`, `Credits`, `Collections`, `ImportListMovies` | `Movies_*` | Foreign keys via `IdMap`. Collections: `QualityProfileId`, `Tags`, `RootFolderPath` (through `pathMap`). |
| `MovieTranslations` | `Movies_MovieTranslations` | `Language` remapped. |
| `MetadataFiles`, `ExtraFiles`, `SubtitleFiles` | `Movies_*` | `MovieId` and `MovieFileId` via `IdMap`. `SubtitleFiles.Language` remapped. |
| `RootFolders` | `Movies_RootFolders` | `Path` through `pathMap`. |
| `NamingConfig` | `Movies_NamingConfig` | Copied. The single row is updated if migrations created one. |
| `ImportLists` | `Movies_ImportLists` | Only implementations that still exist (TMDb lists, RSS import). Others skipped as `UNKNOWN_PROVIDER`. `QualityProfileId`, `Tags` via `IdMap`; `RootFolderPath` through `pathMap`. |
| `ImportExclusions` | `ImportExclusions` | Copied. |
| `Metadata` | `Movies_Metadata` | Only Xbmc. Others skipped as `UNKNOWN_PROVIDER`. |
| `QualityProfiles` | `QualityProfiles` | See [Profiles and custom formats](#profiles-and-custom-formats). |
| `CustomFormats` | `CustomFormats` | Same section. |
| `QualityDefinitions` | `QualityDefinitions` | Sizes and titles by remapped quality. Rows for qualities the catalog does not have are skipped. |
| `DelayProfiles` | `DelayProfiles` | The default row (`Order = int.MaxValue`) is updated in place, the rest inserted. `Tags` via `IdMap`. |
| `ReleaseProfiles` | `ReleaseProfiles` | `IndexerId` via the indexer match (see [Indexers](#indexers)); 0 when unmatched. |
| `AutoTagging`, `CustomFilters`, `RemotePathMappings` | same | `Tags` via `IdMap`. `RemotePathMappings.LocalPath` through `pathMap`. |
| `DownloadClients` | `DownloadClients` | See [Download clients](#download-clients). |
| `Notifications` | `Notifications` | Only implementations that still exist. `Tags` via `IdMap`. Boolean `On*` columns copied. |
| `Indexers` | *(matched, not imported)* | See [Indexers](#indexers). |
| `History` | `History` | `MediaKind = Movies` (explicit), `MovieId` to `ParentId` via `IdMap`, `Quality` and `Languages` remapped. `Data` JSON copied. `EventType` through an explicit map (see below). |
| `DownloadHistory` | `DownloadHistory` | `MediaKind = Movies`, `MovieId` to `ParentId`, `IndexerId` and `DownloadClientId` via `IdMap`. `Release` is a serialized `ReleaseInfo` in Radarr's shape and must be converted to Prowlarr's shape (plan, finding 1: `Size` is `long?`, languages are strings, flags are a named set), using `IndexerLanguageMapper` and `IndexerFlagsMapper`. |
| `Blocklist` | `Blocklist` | `MediaKind = Movies`, `MovieId` to `ParentId`, `Quality` and `Languages` remapped. `IndexerFlags` copied. |
| `PendingReleases` | *(skipped)* | Transient: releases held by a delay profile for hours. They return on the next RSS sync. Reported as a count, reason `TRANSIENT`. |
| `Config` | `Config` | Radarr and Sonarr portable media-loop settings go through the allowlist in [Configuration](#configuration); instance credentials and identity values are skipped. |
| `Users`, `ScheduledTasks`, `Commands`, `Logs`, `UpdateHistory`, `VersionInfo`, `IndexerStatus`, `DownloadClientStatus`, `NotificationStatus`, `ImportListStatus` | *(skipped)* | Instance or runtime state. `TRANSIENT` or `INSTANCE`. |

**Event type maps.** `History.EventType` and `DownloadHistory.EventType` are
integers. The adapter holds an explicit map from Radarr's values to Plattarr's, even
if the two are identical today; a test asserts every Radarr value maps and fails when
upstream adds one. An unmapped event type is a skipped row (`UNKNOWN_EVENT`), not a
guess.

**The `DownloadHistory.Release` conversion is the highest-risk mapping.** Wrong
output does not fail the import; it breaks blocklisting and re-grab decisions later.
It gets the most real-row coverage (see [Tests](#tests)).

### From `prowlarr.db`

| Source table | Target | Notes |
|---|---|---|
| `Indexers` | `Indexers` | See [Indexers](#indexers). |
| `AppSyncProfiles` | *(folded into `Indexers`)* | Provides `EnableRss`, `EnableAutomaticSearch`, `EnableInteractiveSearch` and the default `MinimumSeeders`. |
| `IndexerProxies` | `IndexerProxies` | Implementations that exist (FlareSolverr and the rest of Prowlarr's layer). `Tags` via `IdMap`. |
| `Applications` | *(skipped)* | Prowlarr's sync targets (Radarr, Sonarr). Plattarr has no sync. Used only to learn which Radarr indexer came from which Prowlarr indexer. `OBSOLETE`. |
| `DownloadClients` | `DownloadClients` | A matching Prowlarr endpoint maps to the imported Radarr/Sonarr client. A Prowlarr-only client is skipped as `NO_MEDIA_TYPE` (see [Download clients](#download-clients)). |
| `Notifications` | `Notifications` | Only implementations that still exist. |
| `Tags`, `CustomFilters` | `Tags`, `CustomFilters` | Tags merge by label (see below). |
| `Config` | `Config` | Global settings; see [Configuration](#configuration). |
| `IndexerStatus`, `IndexerDefinitionVersions`, `ApplicationStatus`, `DownloadClientStatus`, `NotificationStatus`, `Commands`, `ScheduledTasks`, `Users`, `Logs`, `UpdateHistory`, `VersionInfo` | *(skipped)* | Runtime state; Cardigann definitions are downloaded again on first start. |
| `History` (indexer query and grab statistics) | *(skipped in v1)* | Statistics only; large. An opt-in can come later. `STATS`. |

### From `sonarr.db`

Built (see [TV stages](#tv-stages-built)): `Tags`, `CustomFormats`,
`QualityProfiles`, `QualityDefinitions`, `DelayProfiles`, `Indexers`, `RootFolders`, `Series`,
`Episodes`, `EpisodeFiles`, `SubtitleFiles`, `ExtraFiles`, `MetadataFiles` into the shared or
`Tv_*` tables. Shared stages also import `ReleaseProfiles` and allowlisted `Config` rows.
Deferred by `/skip=` when non-empty and not
staged: `RemotePathMappings`, `Notifications`, `AutoTagging`, `CustomFilters`,
`NamingConfig`, `Metadata`, `ImportLists`, `ImportListExclusions`, `History`, `DownloadHistory`,
`Blocklist`. `DownloadClients` now has a stage. Count only: `PendingReleases`, `SceneMappings`, `ImportListItems`,
`ScheduledTasks`, `Commands` and the four status tables (`TRANSIENT`); `Users`, `VersionInfo`
(`INSTANCE`). `SceneMappings` is downloaded again by the TV module.

### Cutover disposition from the protected-copy rehearsal (2026-10-25)

The first rehearsal read 51 source tables and found 13,663 imported rows plus 16,582 rows in
13 non-empty tables that had no stage at the time. Read-only counts from the protected Radarr,
Sonarr and Prowlarr copies identified these exact rows:

| Source table | Rows | Cutover decision |
|---|---:|---|
| Radarr `Blocklist` | 53 | Defer to Tier 2. Old rejected releases may be retried. |
| Radarr `DownloadHistory` | 352 | Defer to Tier 2. Historical records do not drive the current queue; converting `Release` JSON remains the highest-risk mapping. |
| Radarr `History` | 277 | Defer to Tier 2. Activity history only. |
| Radarr `Metadata` | 5 | Defer. All five providers are disabled. |
| Radarr `NamingConfig` | 1 | Defer; naming will be set by hand after import. |
| Radarr `Config` | 8 | Tier 1 selective stage. Import three portable settings; skip five instance-specific values. |
| Sonarr `Blocklist` | 1,900 | Defer to Tier 2. Old rejected releases may be retried. |
| Sonarr `DownloadHistory` | 6,173 | Defer to Tier 2. Historical records do not drive the current queue; converting `Release` JSON remains the highest-risk mapping. |
| Sonarr `History` | 7,797 | Defer to Tier 2. Activity history only. |
| Sonarr `Metadata` | 5 | Defer. All five providers are disabled. |
| Sonarr `NamingConfig` | 1 | Defer; naming will be set by hand after import. |
| Sonarr `ReleaseProfiles` | 1 | Tier 1 stage. The row is enabled and contains active required/ignored release terms. |
| Sonarr `Config` | 9 | Tier 1 selective stage. Import four portable settings; skip five instance-specific values. |

The release-profile stage remaps tags and assigned indexer IDs. The config stage allows
only `MaximumSize`, both `AutoUnmonitorPreviouslyDownloaded*` flags,
`EnableCompletedDownloadHandling`, and `CleanupMetadataImages`. The two `MaximumSize`
values were equal in the rehearsal; when they conflict, Radarr wins and the Sonarr row is
reported as `CONFIG_CONFLICT`. The ten unportable config rows remain `INSTANCE` skips;
their values never enter a report. After these additions, ten tables and 16,564 rows
remain safe to defer for this cutover. The Prowlarr copies had no deferred non-empty tables.

**Step 4 protected-copy replay (2026-10-25).** After adding the release-profile and selective
Config stages, both dry-run and committed import succeeded against the same read-only backup-API
copies. The report read 30,245 rows, imported 13,671 rows, explicitly deferred 16,564 rows across
the ten safe tables above, and recorded nine warnings. The dry-run target had zero Movies, TV
series and release profiles after rollback. The committed scratch target contained 87 Movies,
32 TV series, one download client, 17 indexers, three tags and one release profile. It had the
five allowlisted Config keys and no HMAC, Rijndael or Plex client identity setting. The scratch
targets and reports were removed; no source database or running app was written.

### Container cutover rehearsal (2026-10-25)

After the TV quality-profile validator fix (`2b539e181`), GitHub Actions image build
`40187227003` passed. Only the isolated `plattarr-cutover` container was recreated on a scratch port, using
`ghcr.io/sixtyatewhiskey/plattarr:sha-2b539e1` and its existing imported `/config`. The app reported version `10.0.0.87`, migration version 11, and `/ping`
passed. `/data` was mounted read-only; `/testdata` was the only writable media mount. The live Radarr and Sonarr containers stayed up.

The base-path routes returned the expected root folders:

| Seerr base | Status route | Root folders |
|---|---|---|
| `/radarr` | `/radarr/api/v3/system/status` | `/data/Movies`, `/testdata/movies` |
| `/sonarr` | `/sonarr/api/v3/system/status` | `/data/Shows`, `/testdata/tv` |

The owner confirmed the dev server connects in live Seerr when its URL Base fields are
`/radarr` and `/sonarr`. The separate rehearsal instance still needs the same app-level
Seerr check; no Seerr settings were changed by this rehearsal.

**Download and import.** The imported download client remained disabled; the separate
scratch qBittorrent client was enabled, and all 17 imported indexers remained disabled.
On the preceding `sha-d5ed40f` run, the companion movie proof added Sintel, direct-pushed
the same CC-licensed torrent and imported it under `/testdata/movies`.
The TV-only quality profile (ID 7) accepted a Game of Thrones add (TVDB 121361) at
`/testdata/tv/Game of Thrones (2011)`. A direct release push for S01E01 was approved and
grabbed by qBittorrent in the TV category. The CC-licensed Sintel torrent completed,
was renamed before import, and produced `downloadFolderImported` history for episode
S01E01. `hasFile` became true and the file was written under the scratch root as
`Season 1/Game of Thrones - S01E01 - Winter Is Coming WEBDL-480p.mp4`. The first push
attempt returned qBittorrent's duplicate-hash conflict because the movie proof's Sintel
torrent was still registered; removing that scratch torrent entry while preserving its
files let the retry complete. The temporary movie and TV records, files and torrent were
removed afterwards, and the original rename settings were restored.

**Scan and counts.** `RescanMovie` and `RescanSeries` completed. After cleanup, the
scratch copy and live apps both reported 87 movies with 75 movie files, and 32 TV series
with 1,504 episode files (31 series have files). Counts were unchanged by the final
scans. The committed import report initially contained 1,488 TV episode-file rows; the
first measured post-start snapshot already had 1,504, matching live Sonarr. This run did
not isolate which startup/background refresh surfaced the additional 16 files.

One scan logged an ffprobe error for the existing read-only file
`/data/Movies/Inception (2010)/Inception (2010) BR-DISK.m2ts`. Both the scratch and live
Radarr records show the same 5,681,651,712-byte BR-DISK file with null media-info fields;
the scan did not change library counts. The three protected source DB copies still pass
`PRAGMA integrity_check` and remain mode 600. A separate mode-600 backup of the scratch
target DB, made before the image update, also passes the integrity check. The rehearsal
test roots contain no files now.

At measurement, Plattarr used about 252 MiB and the scratch qBittorrent container about
23 MiB. The image size was 476,853,826 bytes (about 454.7 MiB).

### Production cutover (2026-10-25)

The production cutover was executed on 2026-10-25 from fresh protected source database
copies. The image was `ghcr.io/sixtyatewhiskey/plattarr:sha-2b539e1` (revision
`2b539e181b09eb7fc06cc43e90945d44500465d4`). Import reporting and the planned
deferrals were checked before starting production. Protected source database copies,
source app configs, and the previous Seerr settings are retained in an owner-only
backup location outside this repository. The backups and their exact location are not
recorded here.

Source app queues were empty before the switch. The original containers were stopped
one at a time and retained for rollback. Production Plattarr is healthy on port 7880
with migration 11. The source containers remain stopped; qBittorrent and Seerr remain
running. The development instance was stopped to free port 7880. A separate Scratch
instance remains isolated on its own port and was not promoted or modified.

The imported library uses the `/data/Movies`, `/data/Shows`, and `/data/Music` roots.
Music monitoring states and the custom audio profile were restored. qBittorrent uses
the existing `radarr`, `tv-sonarr`, and `lidarr` categories. Its downloads path maps
to `/data/Importing` in Plattarr.

Seerr's default movie and TV entries now target port 7880 with URL bases `/radarr` and
`/sonarr`. A temporary request confirmed that the production default routes to
Plattarr; the check was performed with searching prevented and the downloader disabled.
The temporary request and empty library record were removed after verification. No
media files were deleted.

Indexers that passed their connection tests are enabled. Two indexers failed DNS
resolution and remain disabled pending diagnosis; source-disabled indexers remain
disabled. qBittorrent is enabled. Plattarr authentication settings were left as
provisioned; no Forms-auth account or password was created during cutover.

**Rollback.** Stop the production Plattarr container, restore the saved Seerr settings
and restart Seerr, then start the retained `radarr`, `sonarr`, `lidarr`, and `prowlarr`
containers. The old app configs and databases were not changed or removed. Start the
development instance only if the previous port-7880 service is also needed. Keep the
Scratch instance separate.

### Tags

Tags merge across sources by label, compared case-insensitively. The union is written
once; `IdMap` records source tag ID to target tag ID. Every `Tags` JSON array in every
table passes through it (the list of tag-using tables is the same one
`CleanupUnusedTags` keeps; the importer reuses that list so a new tag-using table
cannot be missed). `CleanupUnusedTags` runs on a schedule and deletes tags no row
uses, so importing an unused tag only keeps it until the next cleanup; the importer
does not special-case it. `AutoTagging.Specifications` also holds tag IDs (see
[Building blocks](#building-blocks-built)).

### Profiles and custom formats

Order: custom formats first (profiles reference them), then profiles.

- **Custom formats.** `Specifications` JSON: `LanguageSpecification` values remapped;
  the other specification values are Radarr's vocabulary, which is the global one, so
  they are copied. `AppliesTo` is `["movies"]` for rows from Radarr.
- **Profiles.** `Items` (quality IDs; group IDs from 1000 are profile-local and kept),
  `Cutoff` (a quality or a group ID), and `Language` are remapped. `FormatItems` is
  rewritten with target custom format IDs from `IdMap` and the scores unchanged.
  `AppliesTo` is `["movies"]`.
- **Merging identical items.** After remapping, items are compared by canonical JSON
  excluding `Id` and `AppliesTo`. Identical items from different sources become one
  row with the union of `AppliesTo`. With Radarr and Prowlarr only, nothing merges
  (Prowlarr has no profiles); the rule exists for the Sonarr adapter and is covered
  by a planner test with two synthetic sources.
- **Name collisions** (same name, different content, from different sources) are
  written with the source appended (`HD-1080p (Sonarr)`) and reported.
- **Built for Sonarr (session 61; verified in session 62).** `ProfilesStage` reads Radarr then
  Sonarr. A row is merged only with a same-name row from the other app whose content is equal
  (custom formats: the renaming flag and the serialized specifications; profiles: cutoff,
  language, flags, scores, formats and items), and then `AppliesTo` gets both kinds. Otherwise
  the second app's row is renamed `"<name> (Sonarr)"` with a warning. Sonarr's default profile
  names equal Radarr's, and a Sonarr profile is padded with the qualities Sonarr does not have
  (below), so in practice the six defaults are renamed. *(reversible)*
- **Sonarr specifics.** Sonarr 4 profiles have no `Language` column (the profile gets
  `Unknown`, which means "any"). A Sonarr profile lists Sonarr's qualities only; every catalog
  quality it lacks is added as not allowed at the bottom, so the profile ranks every quality
  the target has. Sonarr's `IndexerFlagSpecification` values use Sonarr's bits (finding F4) and
  are translated with `SonarrIndexerFlags.ToGlobal`; an unknown bit skips the format as
  `CORRUPT`.
- **Quality definitions** are one row per quality in Plattarr (`QualityDefinitionService` uses
  `SingleOrDefault`). The first app (Radarr) wins; the Sonarr row maps to the same target row,
  and one warning lists the qualities whose sizes differed. *(reversible)*
- **Delay profiles** are global. The first app's default updates the target default; a later
  app's default merges when its settings are equal, otherwise it is skipped as
  `CONFIG_CONFLICT`. Other rows merge only with an identical row (settings and tags) from
  another app; Sonarr's are ordered after Radarr's, and a tag shared with another app's profile
  gives a warning (the first in order wins for that tag). *(reversible)*

### Download clients

- Only implementations that still exist are imported (qBittorrent and SABnzbd).
  The rest are skipped as `UNKNOWN_PROVIDER`, with the implementation name and the
  client's name in the report.
- **Settings are converted from the old shape to the migration 003 shape.** For
  qBittorrent and SABnzbd: `movieCategory` becomes `moduleCategories[Movies].category`,
  `movieImportedCategory` becomes `.importedCategory`, `recentMoviePriority` becomes
  `.recentPriority`, `olderMoviePriority` becomes `.olderPriority`; the old keys are
  removed; a value that is not stored stays unstored; an empty category stays empty.
  The importer keeps the user's own category (`radarr`, `movies`, whatever it is), and
  does **not** replace it with `plattarr-movies`, so torrents already in that category
  keep being tracked. A test feeds the same rows through migration 003 and through the
  importer and asserts the settings JSON is equal.
- Sonarr's old `tvCategory`, `tvImportedCategory`, `recentTvPriority` and
  `olderTvPriority` keys become the TV entry. After merging the apps, `EnsureEntries`
  fills any missing media type with the client's default category and priority; existing
  entries are preserved.
- **Matching clients.** Two clients are the same when implementation, host, port, SSL
  flag and URL base are equal. Matching Radarr and Sonarr definitions become one target
  client, retaining each app's category entry and Radarr's other settings if those differ.
  A matching Prowlarr client maps to that target ID.
  A Prowlarr-only client has no media type and no category that means anything to
  Plattarr, so it is skipped and reported (`NO_MEDIA_TYPE`). *(reversible: it could
  instead be imported with a default `plattarr-movies` entry.)*
- `Indexers.DownloadClientId` (Prowlarr's per-indexer client override) is remapped
  through the client `IdMap`, or set to 0 with a warning when that client was skipped.
- `RemoveCompletedDownloads`, `RemoveFailedDownloads`, `Priority`, `Tags`, `Enable`
  are copied.

### Indexers

Indexers come from `prowlarr.db` only. In the owner's setup (and most users') Radarr's
own indexer rows are Torznab entries that Prowlarr synchronized, so importing both
would duplicate every indexer.

- **Prowlarr rows.** `Settings` JSON is already in the shape Plattarr's layer uses (it
  is Prowlarr's layer). The adapter: renames the stored `appMinimumSeeders` to
  `minimumSeeders` in the torrent base settings (the key is optional and absent from
  every row in the owner's data; see [Dump findings](#dump-findings)); sets `EnableRss`, `EnableAutomaticSearch`, `EnableInteractiveSearch` from the
  row's `AppSyncProfile`; takes `MinimumSeeders` from the indexer if set, otherwise
  from the sync profile; drops `AppProfileId`; remaps `Tags` and `DownloadClientId`.
  `Priority`, `Redirect`, `Enable`, `Added`, `Implementation`, `ConfigContract` are
  copied.
- **Cardigann indexers** store a `definitionFile`. The importer does not check that the
  definition exists (the cache is empty on a fresh start). The first start downloads the
  definition list; a missing file is the existing "missing Cardigann definition"
  case. The report lists Cardigann indexers so the owner can confirm after the first
  start.
- **Unknown implementations** (an indexer whose native implementation was removed or
  renamed) are skipped as `UNKNOWN_PROVIDER`, because `RemoveMissingImplementations`
  would delete them anyway.
- **Matching Radarr's indexers to Prowlarr's.** A Radarr indexer that Prowlarr
  synchronized is a Torznab or Newznab row whose `baseUrl` ends in `/<prowlarrIndexerId>/`.
  The adapter extracts that ID and matches it to a Prowlarr indexer, but only when the
  Prowlarr `Applications` rows show a Radarr sync target (otherwise a coincidental URL
  could match). A match gives the Radarr row's per-app tweaks a home:
  `multiLanguages`, `requiredFlags`, `removeYear` and a categories list that differs from
  the Prowlarr indexer's become one `IndexerOverrides` row (`MediaKind = Movies`), written
  only when it differs from the global value. Radarr's per-indexer `EnableRss` and
  `EnableAutomaticSearch` that differ from the Prowlarr value are reported (there is no
  per-media-type enable flag), and the Prowlarr value wins. *(reversible.)*
- **Unmatched Radarr indexers** (added by hand, not through Prowlarr) are not imported
  and are listed as `UNMATCHED_INDEXER` with their name (not the URL, which can carry a
  passkey; session 60). Radarr's old indexer
  settings are not Prowlarr's, and guessing the translation is how data is lost.
- **`ReleaseProfiles.IndexerId`** and **`DownloadHistory.IndexerId`** use the same match
  (Radarr indexer ID to target indexer ID); 0 when unmatched.

### Configuration

The current `ConfigStage` handles Radarr and Sonarr settings that affect core media behavior:
`MaximumSize`, `AutoUnmonitorPreviouslyDownloadedMovies`,
`AutoUnmonitorPreviouslyDownloadedEpisodes`, `EnableCompletedDownloadHandling`, and
`CleanupMetadataImages`. Boolean and integer values are parsed and normalized before being
written. When both apps set the same key to different values, Radarr's value wins and the
Sonarr row is reported as `CONFIG_CONFLICT`.

Every other source config row is reported as `INSTANCE` and omitted. This keeps HMAC and
Rijndael passphrases/salts, Plex client identifiers, API keys, and other machine-bound
values out of Plattarr. Values never appear in logs or reports. Prowlarr Config is count-only
in v1; it is not part of this stage.

### Paths

Upstream containers and Plattarr usually mount media at different container paths (the
owner's live apps mount `Movies`, `Shows` and `Importing` separately; Plattarr's dev
stack mounts one `/data`). `pathMap` rewrites by longest matching prefix, applied to:
`Movies.Path`, `RootFolders.Path`, `Collections.RootFolderPath`,
`ImportLists.RootFolderPath`, `RemotePathMappings.LocalPath`. Relative paths
(`MovieFiles.RelativePath`, extra files) are not touched. Paths inside client settings
are not remapped (they live on the client's side).

After writing, the verifier checks each mapped root folder with the target's own
accessibility test and warns (never fails) for one that is not reachable, since the
container running the import may not mount the same paths as the one that will run
Plattarr.

### Write order

Dependencies decide it; the order is a list in code with a test that every table the
adapter reads has a position.

1. Tags
2. Custom formats, then quality profiles, quality definitions, delay profiles
3. Download clients (their IDs are needed by Prowlarr's per-indexer client overrides)
4. Indexer proxies, indexers and indexer overrides
5. Release profiles (their `IndexerId` is a target indexer ID, which exists only after the
   indexers are written)
6. Remote path mappings, notifications, auto tagging, custom filters
7. Root folders, naming config, import exclusions, metadata consumers, import lists
8. Movie metadata, alternative titles, translations, credits, collections
9. Movies, then movie files, then `Movies.MovieFileId`
10. Import list movies, metadata files, extra files, subtitle files
11. History, download history, blocklist
12. Portable Config settings

## Verification

Runs in the transaction after the writes, with plain SQL on the import's connection
(see [Pipeline](#pipeline)); any failure rolls back and the report says which check
failed.

1. **Reconciliation.** For every source table, rows read = rows written + rows skipped.
   The report prints the table.
2. **References.** Every `ParentId`, `MovieId`, `MovieMetadataId`, `MovieFileId`, profile,
   tag, client and indexer ID that is non-zero resolves to a row.
3. **Catalog.** Every quality and language ID in every JSON column parses back through
   `QualityIntConverter` and `LanguageIntConverter` (a stored ID that is not in the
   catalog throws at read time, so this check catches it at import time).
4. **Providers.** Every provider row resolves with `ProviderConfigTypes.Find` and the
   provider factory's own implementation lookup, and its settings deserialize and
   validate. This is the same test `RemoveMissingImplementations` applies, so a row
   that would be deleted on first start fails here instead.
5. **Uniqueness.** Unique columns (`Indexers.Name`, `Tags.Label`, `QualityProfiles.Name`,
   `Movies_MovieMetadata.TmdbId`) have no collision; the planner has already
   disambiguated names, so a failure here is a planner bug.

## Report

`/config/import-report/import-<timestamp>.json` and `.txt`:

- source files, their `VersionInfo` maxima, and SHA-256 of the copies;
- per table: read, written, skipped, with the reason-code histogram; deferred tables appear
  in a separate "Deferred by the owner with /skip=" section and balance as skipped rows;
- every skipped row: table, source ID, name or title, reason code, one-line detail;
- warnings (path not reachable, config conflict, client override dropped, Cardigann
  definitions to confirm);
- `IdMap` as `import-idmap-<timestamp>.csv`;
- a short "needs attention" list at the top of the text file (anything that changes
  behavior: skipped clients, unmatched indexers, dropped overrides).

Reason codes: `UNKNOWN_PROVIDER`, `UNMATCHED_INDEXER`, `DUPLICATE`, `NO_MEDIA_TYPE`,
`UNKNOWN_EVENT`, `UNKNOWN_KEY`, `CONFIG_CONFLICT`, `INSTANCE`, `TRANSIENT`, `STATS`,
`OBSOLETE`, `CORRUPT` (an ID upstream never had, malformed JSON). `CORRUPT` on a
non-optional table fails the run; on history it skips the row and counts it.

The importer also writes `Config` key `ImportedFrom` (importer version, source file
hashes and versions, date) so a later start can say where the data came from.

## Tests

Rules from the handoff apply: a test that shares a constant with the code under test
proves nothing, so expected values are literals taken from upstream data, and each
new test is shown to fail without the change.

**1. Fixtures from real shape, no private data.** The repo is public. Raw copies of the
owner's databases are never committed (they hold indexer credentials, cookies, API keys,
hostnames and a library listing).

- `tools/upstream-fixtures/` holds a script that runs against a real database and emits
  (a) the schema (`.schema`), committed per supported version, and (b) a small set of
  rows per table, scrubbed: secrets, URLs and hostnames replaced by fixed fake values,
  titles replaced with public-domain titles (Sintel, Big Buck Bunny), IDs and JSON
  structure left exactly as found.
- `src/NzbDrone.Core.Test/Files/Upstream/` holds the output. A builder creates a SQLite
  file from the schema and the rows, so every test starts from a database with the true
  old shape, not a hand-written approximation.
- The first fixtures come from the owner's current Radarr and Prowlarr. A second
  version of each (an older schema) is added if the dump shows the owner's version is
  not the oldest the adapter claims to support.

**2. Unit tests (no database).** Planner tests over synthetic snapshots: tag union and
case folding, profile merge with two sources, name collision handling, skip reasons,
`pathMap` longest-prefix, client matching, indexer matching by `baseUrl` ID (including
the case where `Applications` has no Radarr target), config conflict resolution, the
event type map completeness, ID-map coverage.

**3. Adapter tests (SQLite).** Per table: read the scrubbed rows and compare with
expected typed rows written as literals.

**4. End-to-end tests (SQLite and Postgres).** `DbTest`-based, because core `DbTest`
already runs on Postgres. Build the fixture databases, run the engine against a migrated
empty target, assert on rows with literal expectations: a movie with a file, a
history chain (grab, import, failure), a blocklist row, a pending release (skipped), a
pruned-provider client (skipped and reported), a qBittorrent client in the old shape
(category preserved), a Radarr indexer tied to a Prowlarr one (override row written).
The migration 003 equivalence test lives here.

**5. Verifier tests.** Corrupt a fixture (an unknown quality ID, a dangling movie
reference, a pruned provider row forced past the planner) and assert the transaction
rolls back and the report names the check.

**6. Boot test.** After an end-to-end import, start the application on the result
(the same smoke start used in the handoff) and assert `system/status`, `movie`,
`history`, `indexer`, `downloadclient` and `qualityprofile` return 200 with the expected
counts. Run in CI as a separate job once the importer exists, because it is slow.

**7. Rehearsal (not CI).** The cutover rehearsal (roadmap item 2, second bullet) runs
the importer on copies of the owner's real databases and compares counts, spot-checks
movies and paths, and reads the report with the owner. It is the only test that sees
real data volume and real surprises; the fixtures exist so that what it finds can be
reproduced as a test afterwards.

## Dump findings

Source: consistent copies of the owner's live databases (SQLite backup API, source
opened read-only, containers untouched), taken 2026-10-21. Both passed
`PRAGMA integrity_check`. Raw rows are not in the repository. Fixtures are scrubbed
(see [Tests](#tests)).

### Supported versions

| App | Image | `MAX(VersionInfo.Version)` | Tables |
|---|---|---|---|
| Radarr | hotio/radarr | 242 | 41 |
| Prowlarr | linuxserver/prowlarr | 44 | 20 |
| Sonarr | hotio/sonarr 4.0.20.3014 | 217 | 38 |

The adapters record these as the first supported versions and refuse a database with a
newer `MAX(Version)` unless the user passes an override. A lower version is read with
the columns it has.

### Radarr row counts that shape the design

`Movies` 87, `MovieMetadata` 113, `MovieFiles` 75, `History` 277, `DownloadHistory` 352,
`Blocklist` 53, `Indexers` 13, `DownloadClients` 1, `QualityProfiles` 6,
`CustomFormats` 1, `Tags` 1, `RootFolders` 1. Empty: `Notifications`, `ImportLists`,
`ReleaseProfiles`, `RemotePathMappings`, `PendingReleases`, `MetadataFiles`,
`ExtraFiles`, `ImportExclusions`, `AutoTagging`. `Credits` (4,623) is the largest
imported table; `Commands` (3,504) is transient and skipped.

### Prowlarr row counts

`Indexers` 17 (15 Cardigann, Knaben, SubsPlease), `Applications` 4, `AppSyncProfiles` 1,
`ApplicationIndexerMapping` 37, `IndexerProxies` 1, `Tags` 2, `History` 203,960.
**`DownloadClients` is empty**, so the "Prowlarr-only client" case does not occur in
this data (the rule stays for other users). `Prowlarr.History` is not imported.

### Confirmed shapes

- **Radarr `DownloadHistory.Release`** is populated only for `EventType = 1` (grabbed):
  141 of 141 rows; it is null for all other event types. It is camelCase `ReleaseInfo`
  JSON: `title`, `size`, `guid`, `downloadUrl`, `infoUrl`, `commentUrl`, `magnetUrl`,
  `infoHash`, `seeders`, `peers`, `indexerId`, `indexer`, `indexerPriority`,
  `downloadProtocol` (`"torrent"`), `tmdbId`, `imdbId`, `publishDate`, `languages`,
  `age`, `ageHours`, `ageMinutes`. `imdbId` is an **integer** (not `tt...`), `tmdbId`
  can be 0, `languages` can be empty. **`downloadUrl` embeds the Prowlarr API key** in
  its query string, so the importer must not log it and fixtures must replace it.
- **`DownloadHistory.Data`** holds `downloadClient`, `downloadClientName`, `indexer`
  (grab rows only), `customFormatScore`. `History.Data` for an import adds `fileId`,
  `droppedPath`, `importedPath`, `releaseGroup`, `size`, `indexerFlags` (a string such
  as `G_Freeleech`, not an integer). `History.Quality` and `Languages` are indented
  JSON with upstream IDs, so they go through the catalog maps.
- **Radarr Torznab `Indexers.Settings`**: `baseUrl` ends in `/<prowlarrIndexerId>/`,
  and the ID is the Prowlarr indexer ID (confirmed for 13 of 13 rows against
  `ApplicationIndexerMapping`). Other keys: `apiPath`, `apiKey`, `minimumSeeders`,
  `seedCriteria` (object), `rejectBlocklistedTorrentHashesWhileGrabbing`,
  `requiredFlags`, `categories`, `multiLanguages`, `failDownloads`, `removeYear`.
  `ConfigContract` is `TorznabSettings`; `Tags` is a JSON string; `DownloadClientId` is 0.
- **Radarr `DownloadClients.Settings`** (qBittorrent): `host`, `port`, `useSsl`,
  `username`, `password`, `movieCategory`, `recentMoviePriority`, `olderMoviePriority`,
  `initialState`, `sequentialOrder`, `firstAndLast`, `contentLayout`.
  `movieImportedCategory` is **absent** when unset (unstored stays unstored, as the
  mapping already says). `ConfigContract` is `QBittorrentSettings`.
- **Prowlarr `Indexers.Settings`**: top-level keys seen are `definitionFile`,
  `extraFieldData`, `baseSettings` (`limitsUnit`), `torrentBaseSettings`, `baseUrl`
  (13 of 17). `torrentBaseSettings` contains only `preferMagnetUrl` in all 17 rows.
  **`appMinimumSeeders`, `seedRatio`, `seedTime`, `packSeedTime` are absent** from every
  row: they are written only when set. The adapter treats each as optional and the
  fixture set includes one row with them present, built from upstream's property
  names (camelCase). The key name `appMinimumSeeders` is therefore *not* confirmed by
  real data. Native indexers (Knaben, SubsPlease) use `ConfigContract`
  `NoAuthTorrentBaseSettings`. `Indexers` columns: `Id, Name, Implementation,
  Settings, ConfigContract, Enable, Priority, Added, Redirect, AppProfileId, Tags,
  DownloadClientId`; there is no `Protocol` column.
- **Prowlarr `AppSyncProfiles`** columns: `Id, Name, EnableRss, EnableInteractiveSearch,
  EnableAutomaticSearch, MinimumSeeders`.
- **Prowlarr `Applications`** columns include `Name, Implementation, Settings,
  ConfigContract, SyncLevel, Tags`. `Settings` has `prowlarrUrl`, `baseUrl`, `apiKey`,
  `syncCategories`, `syncRejectBlocklistedTorrentHashesWhileGrabbing`; Sonarr adds
  `animeSyncCategories` and `syncAnimeStandardFormatSearch`. The owner's four rows are
  Radarr, Sonarr, **LazyLibrarian** and Lidarr. LazyLibrarian is not a Plattarr
  application: the importer reads Prowlarr's `Applications` only to confirm that a
  Radarr sync target exists and does not import the table.
- **`ApplicationIndexerMapping`**: `IndexerId` (Prowlarr), `AppId`, `RemoteIndexerId`
  (the app's own indexer ID), `RemoteIndexerName` (null here).
- **`IndexerProxies`**: FlareSolverr with a `host` and `requestTimeout`; tagged. Tags
  are `lazylibrarian` and `flaresolverr`.

### Findings that change the design

1. **`DownloadHistory.IndexerId` is mostly dangling.** 211 of 352 rows are 0 and, of
   the 141 non-zero rows, 81 point to a Radarr indexer ID that no longer exists. The
   "same match as `ReleaseProfiles.IndexerId`" rule stands, but an unmatched ID is the
   normal case and must map to 0 without a warning per row. The report gives one
   count.
2. **Radarr's synchronized indexers are not in sync with Prowlarr's.** 13 Radarr
   indexers map to 13 of Prowlarr's 17; Prowlarr indexers 10, 37, 41 and 46 have no
   Radarr row. They are imported from Prowlarr regardless. Radarr indexers 29, 31, 32,
   34 (Radarr IDs) have all three `Enable*` flags at 0; the importer compares them with
   the Prowlarr value and reports differences (not compared yet).
3. **The `appMinimumSeeders` rename cannot be tested from real rows.** See above.
4. **Prowlarr's `Config` table holds secrets** (keys are not all readable names and
   values are GUIDs or hashes). It is never read by the importer.
5. **The `moduleCategories` check from the live regression is still open.** The dev
   database has no `DownloadClients` row (the test client was removed), so it cannot
   be answered from it. Check by creating a qBittorrent client through the API on the
   dev stack and reading its `Settings` column, then remove it. Not needed for the
   adapter: the importer writes `moduleCategories` itself.

### Live row shapes (session 30)

Read-only through the scrubbed row printer on the owner's Radarr copy (version 242, 6
profiles, 1 custom format, 30 quality definitions, 87 movies). Nothing below is a value; it is
what the stored JSON and constraints look like.

1. **`Tags` is pretty-printed JSON**, not compact: `[]` and `"[\n  1\n]"`. Anything that reads a
   `Tags` column parses it (`TagRemapper` does); a string match or a regex would miss tagged rows.
2. **`QualityProfiles.Items`** holds 26 entries in two shapes. A leaf is
   `{"quality": id, "items": [], "allowed": bool}`; a group is
   `{"id": 1000+, "name": "...", "items": [leaf, ...], "allowed": bool}` with no `quality` key
   (the live data has four groups, ids 1000 to 1003). Group IDs are profile-local, so the same
   ID in two profiles is a different group. No live profile has a group as its cutoff, so the
   group-cutoff path is covered by a synthetic row only.
3. **`QualityProfiles.FormatItems`** is `[{"format": <CustomFormats.Id>, "score": n}]`, one entry
   per custom format. `Language` is an integer in the live data (1 = English), nullable in the schema.
4. **`QualityDefinitions` sizes are nullable.** 9 of 30 rows have `MaxSize` and `PreferredSize` NULL
   (Bluray-1080p, Remux-1080p, the four 2160p qualities, BR-DISK and Raw-HD) and `MinSize` 0. The target
   column is `double?`, so NULL is kept, not defaulted.
5. **`CustomFormats.Specifications`** is `[{"type": "<Name>Specification", "body": {order,
   implementationName, infoLink, name, negate, required, value, ...}}]`, the same wrapper the
   target's converter reads, so a type the target lacks fails the parse and skips that row.
6. **`DelayProfiles`**: one row, the default (`Order` 2147483647, `Tags` `[]`, `MinimumCustomFormatScore`
   NULL).
7. **`Movies.MovieFileId` is 0 for 12 of 87 movies** (monitored, no file yet). All other IDs in
   the copy resolve: no dangling `MovieFileId`, profile, metadata, credit, alternative title,
   translation, history, blocklist, subtitle or download-history row. The planner still treats a
   dangling reference as corrupt data and skips the row, but the real data will not exercise it.
8. **26 `MovieMetadata` rows have no movie** (113 metadata, 87 movies); 60 metadata rows name a
   collection and 21 `Collections` rows exist. Metadata is a catalog of known titles, not a
   library, so all 113 are imported *(reversible: it is a per-table filter in one stage)*.
   `ImdbId` is NULL or empty for 6.
9. **`MovieFiles.MediaInfo`** is a large embedded document (up to 123 KB per row, mostly
   `rawStreamData` and `rawFrameData` text). It is copied as is; 75 rows, so about 5 MB.
   `Quality` is `{"quality": id, "revision": {"version", "real", "isRepack"}}`, `Languages` an
   array of language IDs, `IndexerFlags` an integer.
10. **`AlternativeTitles.CleanTitle` is UNIQUE in the source schema.** Checked: the target's
    `Movies_AlternativeTitles` index on `CleanTitle` is not unique, so nothing is lost on import
    (pinned by `should_allow_two_metadata_rows_to_share_an_alternative_title_clean_title`).
11. **Event types.** `History`: 1 (grabbed) 141, 3 (downloaded) 79, 4 (failed) 53, 6 (file deleted) 4.
    `DownloadHistory`: 1: 141, 2: 79, 3: 53, 5: 79. Every one of these is in `RadarrEventTypes`.
12. **`History.Data` for a grab carries a `downloadUrl` with the Prowlarr API key in its query
    string**, like `DownloadHistory.Release`. The activity stage must not log it, and should decide
    strip the `apikey` parameter on import: a stored key is a secret in the new database that
    nothing needs, because a download link is not reused after the grab. Decided: strip (item 8).
13. **`Config`** holds 8 keys: `autounmonitorpreviouslydownloadedmovies`, `cleanupmetadataimages`,
    `maximumsize`, `plexclientidentifier` and four secrets (`hmacpassphrase`, `hmacsalt`,
    `rijndaelpassphrase`, `rijndaelsalt`). The four secrets are instance keys and are not carried.
14. **`Metadata` (consumers)**: 5 rows, all disabled (Kodi, WDTV, Roksbox, MediaBrowser, Kometa).

## Known costs

- **Cover images** are not copied. IDs differ, and `MediaCover` is a cache that
  refreshes from the stored `remoteUrl`. Expect one image fetch per movie after the
  first refresh.
- **In-flight downloads at cutover.** Plattarr tracks only its own client categories.
  A torrent still downloading in Radarr's category when Plattarr takes over will not be
  imported by Plattarr unless the client keeps Radarr's category (it does: the
  importer preserves the stored category). Cutover instructions: let the Radarr queue
  drain, then stop the container.
- **IDs change.** See [Pipeline](#pipeline).
- **First start after import** refreshes metadata for every movie through the metadata
  proxy (existing behavior for a stale `LastInfoSync`). The importer does not change
  `LastInfoSync`.

## Open items

1. ~~Dump the owner's two databases.~~ Done 2026-10-21 (see
   [Dump findings](#dump-findings)). Supported versions are Radarr 242 and Prowlarr 44.
   Fixtures for the pure building blocks are written (hand-made, fake secrets); row
   extraction from the real databases is `tools/upstream-fixtures`, not built.
2. ~~Confirm the `Release` JSON shape.~~ Done: camelCase `ReleaseInfo`, grabbed rows
   only, integer `imdbId`, `downloadUrl` carries an API key. The conversion to
   Prowlarr's `ReleaseInfo` shape is still required (the sample is Radarr's shape);
   `ReleaseInfoConverter` is built and tested; its real-row fixture is the grab row in
   `UpstreamFixtureTest`.
3. ~~Confirm the synchronized `baseUrl` form.~~ Done: `.../<prowlarrIndexerId>/`, 13 of
   13. ~~`appMinimumSeeders` is not confirmed.~~ Confirmed from Prowlarr's code (session 60;
   still no row in the owner's data sets it). The stage reads `appMinimumSeeders` and keeps a
   `minimumSeeders` already present; see [Indexer stage](#indexer-stage-built).
4. ~~Decide the two reversible calls.~~ Decided by the owner 2026-10-22: Prowlarr-only
   download clients are skipped and reported (none exist in the owner's data, so this
   only matters for other users); instance keys such as the API key are not carried.
5. Confirm that `moduleCategories` is stored for a client created through the API
   (Finding 5).
6. ~~Prowlarr `Config`.~~ Decided 2026-10-22 (owner delegated): import no keys. The table
   holds secrets under unreadable keys and no setting in it is needed for the movie
   loop. The adapter keeps counting it (reason `INSTANCE`). Adding a key later means
   naming it in the adapter and the mapping table; no migration. The "Configuration"
   mapping table should drop its Prowlarr `Config` rows when the settings stage is built.
7. Scrubbed row fixtures: the building blocks have theirs (`UpstreamFixtureTest`:
   tags, a qBittorrent client, five indexers, a grab row, Prowlarr applications and
   sync records), the profile stage has its own (`ProfilesStageFixture`, rows shaped like the
   live ones), and the movie stages have `MovieStagesFixture` (a movie with a file, extras,
   exclusions, collections). Still to write: history chains, blocklist.
8. ~~Strip the `apikey` query parameter from `downloadUrl` in `History.Data` and
   `DownloadHistory.Release`?~~ Decided by the owner 2026-10-22: **yes, strip it.** The
   activity stage removes the `apikey` query parameter (any case) from `downloadUrl` and keeps
   the rest of the URL; the report counts the rows changed and never prints a URL. The
   building block is a pure function with its own tests, written with the activity stage.

Implementation order: adapter read layer (done), building blocks (done), engine and the
Tags, root folder, profile and movie stages (done), then the activity tables (highest risk, most
real-row tests; design the module seam here), then settings and providers (including
the Prowlarr indexer settings translation), then the verifier, the `/import` command,
and the boot test.
