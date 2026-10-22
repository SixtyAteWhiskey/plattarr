# Importer for `radarr.db` and `prowlarr.db`

Status: **design note, not built** (roadmap Phase 2, item 1). Decisions marked
*(reversible)* were made by Claude under the owner's standing delegation
("least technical debt later, most efficient") and can change without rework
elsewhere.

Goal: a user with running Radarr and Prowlarr containers starts Plattarr on an
empty `/config`, runs one command, and gets their movies, history, profiles,
indexers, clients and settings, without rebuilding the library. The upstream
containers are never modified and keep running until the owner cuts over.

Scope of this note: Radarr and Prowlarr only. `sonarr.db` and `lidarr.db` add
an adapter each (see [Adapters](#adapters)) and no other change.

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

Not checked, and therefore not designed around: the exact schema and
`VersionInfo` of the owner's running Radarr and Prowlarr databases. The first
implementation task is to dump both (`.schema`, `SELECT MAX(Version) FROM
VersionInfo`, row counts) and record them in the adapters as the supported
range. Upstream column names in this note come from Radarr and Prowlarr
`develop` as remembered; every one is confirmed against the real dump before
code is written.

## How it runs

```
docker compose run --rm plattarr /import /radarr=/import/radarr.db /prowlarr=/import/prowlarr.db /options=/import/options.json
```

- **Utility mode, not a web feature (first version).** `ApplicationModes`
  gains `Import`, routed in `Bootstrap` before the web host starts. It runs the
  migrations on the target (`-data`), runs the import, writes the report, and
  exits. The engine lives in Core (`NzbDrone.Core.Upstream`), so a UI wizard or
  a command in Phase 3 calls the same code.
- **Arguments:** `/radarr=<path>`, `/prowlarr=<path>` (either may be omitted),
  `/options=<json file>`, `/dry-run`, `/report=<dir>` (default `/config/import-report`).
  Each source path is a `.db` file or a backup `.zip` (upstream's
  `radarr_backup_*.zip` contains `radarr.db`; this is the easiest input while
  the upstream container keeps running).
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
  to target ID), the merge rules, and the skip list. Pure function of the snapshots and
  options, so it is unit-testable without a database.
- **Writer**: inserts through the existing repositories (`InsertMany`), so the table
  mapper and its converters (`QualityIntConverter`, `LanguageIntConverter`) apply and
  Postgres behaves the same as SQLite. Order is fixed by dependencies (see
  [Write order](#write-order)).
- **Verifier**: runs inside the transaction after the writes. Any failure rolls back
  (see [Verification](#verification)).

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
| `Config` | `Config` | See [Configuration](#configuration). |
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
| `DownloadClients` | `DownloadClients` | Only when no Radarr client matches (see [Download clients](#download-clients)). |
| `Notifications` | `Notifications` | Only implementations that still exist. |
| `Tags`, `CustomFilters` | `Tags`, `CustomFilters` | Tags merge by label (see below). |
| `Config` | `Config` | Global settings; see [Configuration](#configuration). |
| `IndexerStatus`, `IndexerDefinitionVersions`, `ApplicationStatus`, `DownloadClientStatus`, `NotificationStatus`, `Commands`, `ScheduledTasks`, `Users`, `Logs`, `UpdateHistory`, `VersionInfo` | *(skipped)* | Runtime state; Cardigann definitions are downloaded again on first start. |
| `History` (indexer query and grab statistics) | *(skipped in v1)* | Statistics only; large. An opt-in can come later. `STATS`. |

### Tags

Tags merge across sources by label, compared case-insensitively. The union is written
once; `IdMap` records source tag ID to target tag ID. Every `Tags` JSON array in every
table passes through it (the list of tag-using tables is the same one
`CleanupUnusedTags` keeps; the importer reuses that list so a new tag-using table
cannot be missed). Unused tags are still imported (a user may tag ahead).

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
- **Matching a Prowlarr client.** Two clients are the same when implementation, host,
  port, SSL flag and URL base are equal. A Prowlarr client that matches a Radarr one
  is dropped (reported as `DUPLICATE`, naming the Radarr client it matched).
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
  `minimumSeeders` in the torrent base settings (confirm both keys against a real
  row); sets `EnableRss`, `EnableAutomaticSearch`, `EnableInteractiveSearch` from the
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
  and are listed as `UNMATCHED_INDEXER` with their name and URL. Radarr's old indexer
  settings are not Prowlarr's, and guessing the translation is how data is lost.
- **`ReleaseProfiles.IndexerId`** and **`DownloadHistory.IndexerId`** use the same match
  (Radarr indexer ID to target indexer ID); 0 when unmatched.

### Configuration

Two kinds of keys, per the spec ("Configuration"):

- **Global-only and override-capable keys** are imported. `prowlarr.db` supplies the
  global value for keys it has; a differing `radarr.db` value for a key on the override
  list becomes `Movies.<key>`; a differing value for a global-only key is reported
  (`CONFIG_CONFLICT`) and the Prowlarr value wins.
- **Instance and environment keys** are not imported: `Port`, `SslPort`, `BindAddress`,
  `UrlBase`, `EnableSsl`, `SslCertPath`, `SslCertPassword`, `ApiKey`, `AuthenticationMethod`,
  `AuthenticationRequired`, `InstanceName`, `LogLevel`, `Branch`, `UpdateMechanism`,
  `UpdateAutomatically`, `UpdateScriptPath`, `ProxyHostname` and the other proxy keys,
  `BackupFolder`, `BackupInterval`, `BackupRetention`. Plattarr's port is 7880 and its
  authentication starts unset; copying a second app's values would mix two identities.
  Users and API keys are skipped for the same reason. Each is listed once under
  `INSTANCE`. *(reversible: carrying over the Radarr API key is a one-line opt-in if the
  compatibility APIs arrive and a tool needs it.)*
- `UILanguage` is imported through `RadarrLanguageIds` (it stores a language ID).
- A key the target does not know is skipped as `UNKNOWN_KEY`.

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
2. Custom formats, then quality profiles, quality definitions, delay profiles, release profiles (needs the indexer match)
3. Indexer proxies, indexers, indexer overrides
4. Download clients, remote path mappings, notifications, auto tagging, custom filters
5. Root folders, naming config, import exclusions, metadata consumers, import lists
6. Movie metadata, alternative titles, translations, credits, collections
7. Movies, then movie files, then `Movies.MovieFileId`
8. Import list movies, metadata files, extra files, subtitle files
9. History, download history, blocklist
10. Config

## Verification

Runs in the transaction after the writes; any failure rolls back and the report says
which check failed.

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
- per table: read, written, skipped, with the reason-code histogram;
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

1. Dump the owner's two databases and fill in `SupportedVersions`, the confirmed column
   and key names, and the first fixtures. Blocks all code.
2. Confirm the `Release` JSON shape in Radarr's `DownloadHistory` and the Prowlarr
   `ReleaseInfo` shape it converts to, from real rows.
3. Confirm Prowlarr's per-indexer `AppMinimumSeeders` key and that its indexer
   `baseUrl` form in Radarr's synchronized rows carries the Prowlarr indexer ID.
4. Decide, with the owner, the two reversible calls most likely to matter in practice:
   Prowlarr-only download clients (skipped) and instance keys such as the API key
   (not carried).

Implementation order once the dump is in: adapter read layer and fixtures, then the
planner with its unit tests, then the writer for the movie tables, then the activity
tables (highest risk, most real-row tests), then settings and providers, then the
verifier, the command, and the boot test.
