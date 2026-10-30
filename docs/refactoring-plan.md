# Refactoring plan

Status: in progress. Created 2026-10-29 from the clean
`develop` checkout at `633377d0a`. This is the working checklist for reducing
runtime cost and maintenance overhead while preserving Plattarr's behavior.

## How to use this plan

- Work from top to bottom. Mark a task complete only with a commit or pull
  request and its verification result linked in the progress log below.
- Record before and after measurements from the same environment and workload.
  If a change does not improve its target metric or remove meaningful
  duplication, reconsider it before merging.
- Keep each implementation slice small enough to review and revert on its own.
  A production rollout is a separate decision and is not part of checking off
  a refactor.
- Preserve the Movies, TV, and Music module boundary, API and Seerr contracts,
  database behavior, import paths, and existing user workflows. Use the
  `ModuleBoundaryFixture` and the existing acceptance gates during each slice.
- Do not delete shipped migrations, compatibility routes, a framework's last
  working provider, or a supported feature merely to reduce file count.

## 0. Refresh the baseline

The [2026-10-27 measurements](pre-public-release-gates-2026-10-27.md#startup-memory-bundle-and-hot-paths)
are useful historical context, not a baseline for the current checkout. They
reported a 1,119 ms warm start to `/ping`, 197,508 KiB steady RSS, and
49,351,605 bytes of frontend output. The local `_output/UI` may be a development
build; do not compare it with production output.

- [x] Record the exact Git revision, build mode, machine/container limits,
  fixture size, and commands used for all measurements.
- [x] Build the current frontend in production mode and record initial route
  transfer size, total and per-chunk JS/CSS sizes, gzip sizes, and source-map
  sizes separately. Capture a webpack module breakdown for the largest chunks.
  HTML/JS/CSS, fonts, images, module breakdown, and browser transfer are
  recorded below. The original `633377d0a` build was captured in Chrome
  during the browser comparison.
- [x] On a disposable Release instance, record warm startup time, steady RSS,
  and RSS after representative requests. Repeat enough times to see variation.
- [x] Time `/ping`, status, queue, Hygiene preview, and a representative library
  route at p50 and p95; results are in the baseline section below.
- [x] Compare the queue-refresh SQL read sequence with small and larger
  synthetic action/state histories, counting queries and materialized rows;
  see the dated read-path profile below.
- [x] Profile an actual `QueueService.Handle` refresh with the same fixture
  sizes, including writes, recovery, .NET memory, and total elapsed time.
- [x] Record the baseline in a dated section of this document and choose the
  first bounded implementation slice from the measured results.

## 1. Reduce frontend startup work

`frontend/src/App/AppRoutes.tsx` statically imports pages for Movies, TV,
Music, Activity, Settings, and System. Route-level loading is the first
candidate because it could reduce work for a user opening only one page.
`frontend/build/webpack.config.js` also creates `LiveReloadPlugin` in both
build modes. Both are investigation targets; production impact must be
measured before changing them.

- [x] Identify the modules dominating the production entry and initial-route
  chunks, including locale data, polyfills, and third-party packages.
- [x] Load less-used routes on demand in a small first slice; preserve the
  media-kind switcher's behavior, navigation, loading state, and error handling.
- [x] Audit production-only build inputs and direct dependencies. Remove or
  replace a package only after checking all imports and generated output.
- [x] Compare initial transfer, browser compile/script time, route navigation
  time, and total output against the baseline. Confirm no unacceptable
  regression for repeat navigation or slow connections. Chrome's compile
  counter does not isolate parser time; see the acceptance comparison below.
- [x] Run frontend typecheck, lint, stylelint, production build, and browser
  acceptance across Movies, TV, Music, shared pages, and narrow layouts; see
  the 44-route check below.

## 2. Bound Hygiene work during queue refresh

`QueueService.Handle` observes strikes and processes Hygiene actions on each
tracked-download refresh. Earlier slices bounded the queue-state and action
reads; the journal's recent-entry read serves a separate API. The action
journal is currently small, so this is a scalability candidate rather than a
proven production bottleneck. The whole-refresh synthetic profile below
identified retention deletes as the larger cold-refresh cost at scale; the
following slice addresses them.

- [x] Profile the SQL read sequence and inspect the existing database indexes
  and query plans; see the dated read-path profile below.
- [x] Measure an actual queue refresh with synthetic histories before making
  another repository change based on whole-refresh performance.
- [x] Replace one full-table read at a time with a bounded query where the
  profile shows useful savings. Consider recent actions, unfinished actions,
  identity lookups, and state retention separately. The four prior slices and
  the bounded journal read below cover these materialized reads.
- [x] Preserve action identity, retry/backoff, checkpoint recovery, rule
  precedence, private-torrent safety, file-deletion choices, and replacement
  search behavior. Preview and execution must select the same candidates.
  The same-snapshot parity and rule-change proof is recorded below
  (`dfec5dca4`); the existing focused recovery and action fixtures passed.
- [x] Compare database result rows, query count, refresh time, and memory at
  the same fixture sizes. Run focused Core/API tests and isolated recovery and
  preview/execution proofs before the full backend gate. The direct
  before/after whole-refresh trace and verification are recorded below.
- [x] Bound the adjacent Hygiene journal API's recent-action read and verify
  descending ID order and the existing response limit. This is separate from
  queue-refresh profiling; see the dated journal slice below.
- [x] Replace per-row expired-state retention deletes with one cutoff query
  after refreshing current queue states. The same-fixture whole-refresh
  comparison and verification are in the dated retention slice below
  (`e541e1639`).

## 3. Remove proven duplication

Consolidate repeated UI or workflow code only where the behavior is genuinely
shared. Prefer a small common component or helper with explicit media-kind
inputs over a generic framework that hides the differences among modules.

- [x] Inventory repeated filter dialogs, form controls, queue/activity
  presentation, and manual-import UI. Rank candidates by repeated logic,
  change frequency, and defect risk; file length alone is not a reason. See
  the dated inventory below (`f01b20951`).
- [x] Extract one pattern at a time and remove the old copies in the same
  change. Keep module-specific parsing, naming, quality, and import rules in
  their owning modules. The library and Activity filter connector patterns are
  complete (`f01b20951`, `6c9af15a5`); other candidates are listed below.
- [x] Keep the architecture allowlist shrinking or unchanged. Verify behavior
  for all affected media kinds, including empty, error, and narrow-screen
  states where the UI is involved. The Activity slice passed all five affected
  views at both widths and their API-error states; see below.

## 4. Prune unused surface area with evidence

Previous provider pruning is documented in [pruned-providers.md](pruned-providers.md).
The remaining native indexer definitions are a possible size and maintenance
target, but removal needs an inventory of configured and supported workflows.
Windows/updater paths still participate in build paths and need a product
support decision before any removal.

- [x] Inventory configured indexers and compare native definitions with
  Cardigann coverage; document the replacement path for each candidate.
  - [x] Reconcile the documented 2026-10-25 owner snapshot with native
    implementations and the upstream definition tree; see the inventory below.
  - [x] Recheck the current configured indexer names, implementation types, and
    Cardigann definition IDs; see the live reconciliation below.
- [x] Check whether a candidate is referenced by migrations, imports, tests,
  generated API resources, or active build and packaging paths. The five
  same-ID candidates and their runtime discovery behavior are audited below.
- [x] Lock the existing native-row startup and import behavior in focused
  regression tests before retiring a same-ID provider. All five candidates
  still import as native implementations, and an obsolete stored row survives
  startup; see the compatibility gate below (`5c1a3690d`).
- [x] Compare the first candidate's native settings and search requests with
  its versioned Cardigann definition before writing a conversion. FunFile is
  the first plausible candidate, but its IMDb search flags differ; the native
  request behavior is now covered by a focused fixture (`42a55d926`). See
  the feasibility check below.
- [ ] For FunFile, prove settings conversion for stored rows and upstream
  imports, then prove search and result behavior against the replacement
  before deleting the native implementation.
  - [x] Prove the native-to-Cardigann settings shape with a database-backed
    stored row and a disposable Prowlarr import row; see the conversion proof
    below. This does not switch either runtime path.
  - [x] Pin the audited definition and compare movie-term and IMDb requests,
    mocked form submission, and parsed fields from one synthetic result page;
    see the offline parity check below.
  - [x] Prove an offline candidate definition restores the native IMDb request
    and release GUID, with Cardigann parser support for an explicit GUID; see
    the candidate parity slice below (`cbbb43e27`).
  - [x] Compare the corrected candidate's TV term/episode and IMDb, music,
    book, and basic search requests with the native provider using its full
    category map; see the expanded parity slice below (`7242d9a0e`).
  - [x] Put the corrected candidate in a standalone test definition and
    verify the supported request modes, release GUID, and current public
    login-form shape; see the candidate delivery check below (`a0ee8ae14`).
  - [x] Confirm that the live Prowlarr feed is the definition delivery path
    and audit the upstream schema and parser before proposing a correction;
    see the upstream delivery gate below.
  - [ ] Verify authenticated site behavior, ship a corrected definition, then
    wire and verify both conversion paths before removal.
- [ ] Remove only independently justified candidates in bounded changes;
  retain a working implementation for each supported provider framework.
- [ ] Measure source/build/image or bundle savings and verify startup,
  provider discovery, import handling, and affected contracts.

## Completion gate for every implementation slice

- [ ] State the behavior and metric targeted, with baseline evidence.
- [ ] Make a focused, independently revertible change.
- [ ] Pass relevant tests and contracts; run `tools/verify.sh` for backend
  changes and the documented frontend gates for UI/build changes.
- [ ] Record before/after results, any regression or limitation, and the
  commit or pull request in the progress log.
- [ ] Confirm the working tree contains only intended files and generated
  changes are reviewed.

## 2026-10-29 baseline and first frontend slice

The baseline is Git `633377d0a` on Linux Mint 22.3, AMD Ryzen 7 5800XT
(16 logical CPUs), 32,779,972 KiB physical memory. No cgroup CPU or memory
limit was exposed. The browser and Release instance ran on this same machine.
The disposable SQLite fixture had zero Hygiene actions, queue states, rules,
and library records; it had no download client. No production data or service
was used.

Commands: `corepack yarn webpack --config ./frontend/build/webpack.config.js
--env production --json /tmp/plattarr-refactor-baseline-stats.json` after moving
the prior `_output/UI` aside; `. tools/env.sh && dotnet build src/Radarr.sln -c
Release --no-restore --no-incremental`; then `. tools/env.sh && python3
tools/benchmark_refactor_baseline.py`, which launches a loopback Release
instance with `-nobrowser -data=<temporary directory>`,
`BindAddress=127.0.0.1`, `AuthenticationMethod=External`, and an ephemeral
port. The instance was started once to create the fixture, then restarted five
times. Each route was
requested sequentially 60 times with the disposable API key. The instance and
fixture were removed after measurement. The Release build had zero warnings
and errors; webpack reported zero errors and warnings.

The baseline production output contained 50 files and 23,170,463 bytes:
3,945,271 JS bytes (997,726 gzip), 389,907 CSS bytes (59,667 gzip),
16,851,380 source-map bytes, and 1,983,905 other bytes. Gzip figures use
level 6 per asset. The entry loads the bootstrap and vendor chunks before the
first page; the worker is on demand. `index.html` is 1,793 bytes (779 gzip).

| Baseline chunk | JS bytes / gzip | CSS bytes / gzip | Map bytes |
| --- | ---: | ---: | ---: |
| Entry `index` | 29,190 / 7,552 | 256 / 211 | 70,651 |
| Bootstrap `655` | 2,022,664 / 454,041 | 388,629 / 59,219 | 8,043,358 |
| Vendor `187` | 1,874,874 / 529,596 | — | 8,665,002 |
| Worker `7` | 18,543 / 6,537 | — | 72,369 |

The initial HTML, JS, and CSS total 4,317,406 bytes (1,051,398 gzip),
excluding fonts, images, API responses, and the worker. Webpack's unminified
module accounting identified Settings (887,416 bytes), Store (618,418), and
Components (476,080) as the largest app folders in bootstrap. Large vendor
modules included Font Awesome solid icons (1,014,556), brands (629,997),
lodash (545,945), React Virtualized (342,430), and 140 Moment locale modules
(542,292 combined). The entry polyfill module was 1,418 bytes. These module
sizes are attribution clues, not independent transfer sizes.

Warm startup to `/ping` took 1,136, 1,167, 1,221, 1,176, and 1,170 ms;
steady RSS after five seconds was 196,252 KiB, rising to 210,412 KiB after
the requests. The first migrated start took 1,829 ms. Request results were:

| Route | p50 ms | p95 ms | Largest response bytes |
| --- | ---: | ---: | ---: |
| `/ping` | 0.544 | 0.802 | 20 |
| `/api/v3/system/status` | 0.972 | 3.999 | 851 |
| `/api/v3/queue` | 0.992 | 1.183 | 129 |
| `/api/v3/hygiene/preview` | 1.183 | 1.595 | 2 |
| `/api/v3/movie` | 3.139 | 4.484 | 2 |

A second run of the checked-in harness saw warm starts between 1,108 and
1,175 ms, 195,676 KiB steady RSS, and 212,704 KiB after requests. Its
`/api/v3/movie` p95 was 9.570 ms; the other four p95 values were 0.583,
1.344, 1.024, and 1.381 ms in table order. This is normal local variation,
not a code comparison.

The first implementation slice loads the Settings landing and twelve leaf
routes on demand. A shared loading page announces its state, and a route-keyed
error boundary presents the existing page error UI if loading fails. All
existing paths and page components remain. The single Media Management route
experiment increased initial JS, so it was replaced by the Settings group
before keeping the change.

| Metric | Baseline | Settings on demand | Change |
| --- | ---: | ---: | ---: |
| Initial JS bytes / gzip | 3,926,728 / 991,189 | 3,622,285 / 945,294 | −304,443 / −45,895 |
| Initial CSS bytes / gzip | 388,885 / 59,430 | 322,685 / 51,287 | −66,200 / −8,143 |
| Initial HTML + JS + CSS gzip | 1,051,398 | 997,360 | −54,038 (−5.1%) |
| Total output bytes | 23,170,463 | 23,479,418 | +308,955 (+1.3%) |
| Source-map bytes | 16,851,380 | 17,047,042 | +195,662 |

The new production build has 17 chunks, zero webpack errors or warnings.
The 13 deferred Settings chunks total 405,826 JS bytes and 79,132 CSS bytes.
Frontend typecheck, lint, stylelint, formatting, and production build passed.
On a disposable browser instance, the Movies, TV, Music, Hygiene, and System
Status routes rendered; the Settings landing and all twelve Settings leaves
rendered without a console error. Media Management's Movie, TV, and Music
switcher rendered each kind. The Settings landing and Media Management also
loaded under a `/plattarr` URL base. At a 480-pixel viewport, the Media
Management and Settings landing pages had no document overflow. Both browser
instances and their scratch data were removed.

The empty fixture does not establish queue-refresh scalability. Query count,
rows read, larger action/state histories, browser parse/execute time, and slow
connection navigation time remain to be measured before work on section 2 or
further route loading. The complete build grew because shared code and styles
are repeated across deferred chunks; this is the measured cost of the initial
transfer reduction.

## 2026-10-29 supported Moment locale slice

The production module breakdown showed 139 Moment locale files and their
context module in the initial vendor chunk (542,292 unminified module bytes).
The API's `IsoLanguages` mapping can return 61 UI language identifiers. Of
those, 54 have exact Moment locale files; `de-DE`, `fr-FR`, and `pt-PT` use
their base locale, English is built into Moment, and `no`, `rm`, and `tl`
already have no matching Moment locale. The production build now includes
the 57 matching Moment locale files. An explicit comparison of the API codes,
the installed Moment locale files, and webpack's context expression found no
missing or extra locale files. No dependency was removed.

The build-input audit also checked `LiveReloadPlugin`. It starts its server
only in webpack watch mode and did not inject a script or runtime code in the
production output, so removing it from production builds offered no measured
transfer saving. Direct-dependency review remains open.

The comparison used the same machine, Release backend, empty disposable
fixture, webpack production mode, and gzip level 6 as the first slice. The
before build was the committed Settings slice at `a0723899d`; the after build
adds only the locale context filter. Commands were `corepack yarn webpack
--config ./frontend/build/webpack.config.js --env production --json
<stats-file>`, `corepack yarn typecheck`, `corepack yarn lint`, and `corepack
yarn stylelint-linux`. The two webpack builds had zero errors and warnings;
all three frontend checks passed.

| Metric | Settings slice | Supported locales | Change |
| --- | ---: | ---: | ---: |
| Initial JS bytes / gzip | 3,622,285 / 945,294 | 3,486,181 / 916,512 | −136,104 / −28,782 |
| Initial CSS bytes / gzip | 322,685 / 51,287 | 322,685 / 51,287 | unchanged |
| Initial HTML + JS + CSS gzip | 997,360 | 968,578 | −28,782 (−2.9%) |
| Total output bytes | 23,479,418 | 22,955,736 | −523,682 (−2.2%) |
| Source-map bytes | 17,047,042 | 16,659,464 | −387,578 |
| Font / image output bytes | 887,403 / 1,069,583 | 887,403 / 1,069,583 | unchanged |

Headless Chrome fetched the two builds from separate disposable loopback
instances with cache disabled and the same empty fixture. Summed
`Network.loadingFinished` bytes for non-API resources on the Movies landing
route were 1,649,285 before and 1,611,842 after (−37,443). Settings landing
was 1,646,646 before and 1,609,203 after; Media Management was 1,659,977
before and 1,622,538 after. The browser loaded two Roboto WOFF2 files; the
other copied font variants were not requested on these routes. The Movies,
TV, Music, Hygiene, System Status, Settings, and Media Management routes
rendered without runtime exceptions. A scratch French UI setting rendered
the calendar's month and weekday names in French and loaded UI Settings
without runtime exceptions. These browser checks used a one-off Chrome
DevTools Protocol script with cache disabled and three seconds of settle time
after each load event.

One cold navigation per route showed Chrome script duration between 86 and
123 ms and varied in both directions; it does not establish a parse/execute
or route-navigation improvement. A later repeatable comparison measured
slow-connection and repeat-navigation behavior against the original build.
No production rollout occurred.

## 2026-10-29 browser comparison and dependency audit

`tools/benchmark_frontend_routes.py` starts a Release app with a disposable
empty SQLite fixture, a loopback-only listener, and headless Chrome. It
disables browser cache for a cold Movies route load, then enables cache and
clicks Settings, Movies, and Settings again through the app's own links. It
records Chrome DevTools Protocol `Network.loadingFinished` bytes for non-API
resources, the time until the Movies page is visible, Chrome
`Performance.ScriptDuration`, and first/repeat Settings navigation. The
`Network.loadingFinished` total is taken after ordinary static requests
settle; worker requests use another DevTools target and are excluded from
the page-level idle condition. The
`fast` profile is unthrottled; `slow` sets 150 ms network latency and
200,000 bytes/s download and upload throughput (about 1.6 Mbit/s).

The original production UI from Git `633377d0a` and the current production
UI were measured on the same machine and Release backend. For each run, the
desired UI output was placed at `_output/UI`, then
`python3 tools/benchmark_frontend_routes.py --profile fast --samples 5` or
`--profile slow --samples 3` ran; the current UI was restored afterward.
Each sample used a fresh Chrome tab with cache disabled for its cold load.
The first run of each profile can include additional browser warmup. The
table reports medians, with observed ranges in parentheses.

| Profile and metric | Original build | Current build |
| --- | ---: | ---: |
| Fast: first Movies render, ms | 454.5 (443.4–694.5) | 447.6 (426.8–694.5) |
| Fast: initial browser transfer, bytes | 1,710,989 (1,710,989–1,716,041) | 1,606,781 (1,606,776–1,611,843) |
| Fast: script duration, ms | 77.8 (73.7–82.6) | 75.7 (74.2–90.5) |
| Fast: first / repeat Settings navigation, ms | 22.3 / 21.6 | 25.0 / 21.4 |
| Slow: first Movies render, ms | 8,899.0 (8,834.0–8,934.3) | 8,345.5 (8,319.8–8,395.9) |
| Slow: initial browser transfer, bytes | 1,710,992 (1,710,990–1,716,047) | 1,606,781 (1,606,780–1,611,846) |
| Slow: script duration, ms | 79.2 (73.4–82.2) | 80.8 (78.4–84.6) |
| Slow: first / repeat Settings navigation, ms | 22.6 / 21.7 | 184.5 / 21.9 |

The current build transfers about 104 KB less on the initial Movies route.
At the simulated slower connection, first Movies render was about 554 ms
earlier at the median; opening Settings for the first time took about 162 ms
longer because its deferred chunk had to load. Repeat Settings navigation
remained around 21–22 ms. All 16 sampled flows had zero browser runtime
exceptions. Script-duration ranges overlap in the fast profile, and
`ScriptDuration` does not isolate JavaScript parsing. The first-render
marker is visible Movies content, not a paint-timing API. These results
support retaining route loading while leaving precise parse/execute timing
and the full narrow-layout gate open.

The production module inventory was cross-checked against every direct
frontend dependency. Two packages had no source/build references, no
webpack production modules, and no transitive consumer according to
`corepack yarn why`: `@fortawesome/fontawesome-free` and
`react-addons-shallow-compare`. They were removed from `package.json`
and `yarn.lock`. TypeScript and the React/Node type packages remain because
the build and typecheck use them; the SVG Font Awesome packages remain
because application modules import them. The removed first package occupied
41.56 MB in `node_modules` during the
frontend build stage; the second occupied 28 KB. These are build-stage
installation savings, not runtime image savings. A frozen offline install
passed, and the production rebuild was byte-for-byte identical across all
98 UI files, with zero webpack errors or warnings. Typecheck, lint, and
stylelint passed. The runtime image was not rebuilt or measured.

## 2026-10-29 first Hygiene action lookup slice

The queue refresh checks for the latest action with the same download client,
download ID, and reason before executing an eligible action. Previously it
materialized every journal row for each eligible item. The new repository
lookup filters those three fields in SQL, using the existing
`IX_HygieneActions_Client_Download_Status` index on its first two columns.
It retains the newest matching action by ID and therefore keeps completion,
retry, and checkpoint behavior unchanged. No migration or index was added.

`python3 tools/benchmark_hygiene_action_lookup.py` creates an in-memory SQLite
journal with 100 or 10,000 completed actions and measures 20 candidate lookups
for ten repetitions. These are synthetic rows with up to five matching actions
per client/download identity, not a production fixture. Times are median
Python plus SQLite elapsed milliseconds for all 20 lookups; rows are those
materialized by the queries during one repetition.

| Journal rows | Full-journal lookup | Identity lookup |
| --- | ---: | ---: |
| 100 | 1.156 ms; 2,000 rows | 0.057 ms; 5 rows |
| 10,000 | 117.937 ms; 200,000 rows | 0.066 ms; 25 rows |

SQLite's query plan changed from `SCAN HygieneActions` to an indexed search
on `DownloadClientId` and `DownloadId`. The index does not cover `ReasonCode`,
so SQLite filters it after finding the client/download pair. This removes the
full-table materialization for this one lookup. The numbers do not establish
whole-refresh time or memory savings; interrupted-action recovery, strike
observation, and decision preview still load full tables. Those paths and
their larger-fixture proof remain open in section 2.

The non-incremental Release solution build passed with zero warnings and
errors. `tools/verify.sh` passed 216 API tests and 78 Host tests (five skipped).
Its broad Core run made no new progress for five minutes and was stopped; the
same stall was previously observed on this checkout. A serial focused run of
Hygiene and `ModuleBoundaryFixture` passed 80 Core tests with zero failures.
The separately repeated action repository/service fixture run passed 34 tests.
The broad Core gate and an end-to-end queue refresh comparison remain open.

## 2026-10-29 interrupted Hygiene action read

The queue refresh resumes every unfinished Hygiene action before evaluating
new candidates. Previously this step fetched the entire action journal and
filtered completed rows in memory. The repository now applies the same
`Status != "completed"` condition in SQL. It still includes paused, failed,
processing, and any other non-completed status, so the recovery candidate set
is unchanged. The query count remains one; no schema
migration was added.

`python3 tools/benchmark_hygiene_interrupted_actions.py` compares the old and
new queries on the same in-memory SQLite journal, using the existing
client/download/status index and ten repetitions at each size. One in every
1,000 synthetic actions is unfinished. Times are median Python plus SQLite
elapsed milliseconds for the read and filter; they exclude the rest of queue
refresh. Rows are those materialized by the query.

| Journal rows | Full journal | Incomplete only |
| --- | ---: | ---: |
| 100 | 0.066 ms; 100 rows | 0.005 ms; 1 row |
| 10,000 | 7.486 ms; 10,000 rows | 0.379 ms; 10 rows |
| 100,000 | 96.373 ms; 100,000 rows | 5.994 ms; 100 rows |

Both `EXPLAIN QUERY PLAN` results still report `SCAN HygieneActions`. The
existing index starts with download client and ID, so it cannot seek by
status alone. An ordinary status index was not added because this inequality
would still scan it; the measured gain comes from avoiding completed-row
materialization. Database rows scanned, whole-refresh latency, and memory
remain open, as do the full-table state reads in strike observation and
decision preview. These synthetic histories do not model a production journal.

The non-incremental Release solution build passed with zero warnings and
errors. `FILTER='FullyQualifiedName~Hygiene|FullyQualifiedName~ModuleBoundaryFixture'
tools/verify.sh` passed 216 API, 78 Host (five skipped), and 81 focused Core
tests. A serial action repository/service run passed 35 tests, including all
the existing retry, checkpoint, and gate recovery cases and a new repository
test proving non-completed status selection. The full broad Core gate was not
rerun because it previously stalled on this checkout. No production rollout
occurred.

## 2026-10-29 Hygiene decision state lookup

Hygiene preview and action execution both use `HygieneQueueDecisionService`.
Previously each evaluation loaded every queue-state row, including states
for downloads outside the current queue. It now requests only the distinct
download-client and download-ID pairs in that queue. The repository combines
parameterized pair predicates into one query for up to 200 identities, then
uses additional batches for larger queues. It reuses the unique
`IX_HygieneQueueStates_Client_Download` index. No migration was added.

`python3 tools/benchmark_hygiene_state_lookup.py` compares the old read with
the identity query on the same in-memory SQLite fixture. Each size has 20
matching queue identities, ten repetitions, and the same indexed schema.
Times are median Python plus SQLite elapsed milliseconds for this read only.

| State rows | Full read | Queue identities |
| --- | ---: | ---: |
| 100 | 0.061 ms; 100 rows | 0.020 ms; 20 rows |
| 10,000 | 6.676 ms; 10,000 rows | 0.030 ms; 20 rows |
| 100,000 | 87.033 ms; 100,000 rows | 0.024 ms; 20 rows |

`EXPLAIN QUERY PLAN` changed from `SCAN HygieneQueueStates` to
`MULTI-INDEX OR`, with an index search on both identity columns for each
pair. The query count is one for 20 identities; queues over 200 identities
use more queries. The repository tests cover duplicate pairs, same download
ID under another client, no identities, and a 201-identity batch. Existing
decision tests cover strike thresholds, cumulative slow/stalled precedence,
the global gate, private-torrent safety, and replacement-search settings.

The non-incremental Release solution build passed with zero warnings and
errors. `FILTER='FullyQualifiedName~Hygiene|FullyQualifiedName~ModuleBoundaryFixture'
tools/verify.sh` passed 216 API, 78 Host (five skipped), and 83 focused Core
tests. The full broad Core gate was not rerun because it previously stalled
on this checkout. These synthetic query timings do not establish whole queue
refresh latency or memory use, and PostgreSQL was not profiled. Strike
observation and retention are covered by the following section 2 slice. No
production rollout occurred.

## 2026-10-29 Hygiene strike observation state reads

Strike observation previously materialized every queue-state row once per
refresh to update current queue identities and retire states last seen more
than 30 days ago. It now reuses the indexed client/download identity lookup
for current items, then queries only expired states after updating them. A
state that reappears after 30 days is refreshed before the expiry query, so it
is retained. The retention cutoff remains strict (`LastSeenAt < now - 30
days`). No schema migration was added.

`python3 tools/benchmark_hygiene_strike_states.py` compares the old full read
with the two new reads in an in-memory SQLite fixture. Each size has 20 queue
identities, 1% expired states, and ten repetitions. Times are median Python
plus SQLite elapsed milliseconds for the reads only; they exclude strike
updates and deletion writes.

| State rows | Full read | Identity + expired reads |
| --- | ---: | ---: |
| 100 | 0.063 ms; 100 rows | 0.028 ms; 21 rows |
| 10,000 | 6.968 ms; 10,000 rows | 0.418 ms; 120 rows |
| 100,000 | 84.547 ms; 100,000 rows | 3.802 ms; 1,020 rows |

The identity query uses `MULTI-INDEX OR` searches on the existing unique
client/download index. The expiry query still scans the state table because
`LastSeenAt` is not indexed, but avoids materializing the recent rows. The
query count rises from one to two for queues of at most 200 identities; larger
queues already use multiple identity batches. An expiry index was deferred
because this slice shows a substantial materialization saving without adding
a write cost or migration; scan cost at larger real retention histories still
needs observation. Synthetic read timings do not establish whole-refresh
latency, memory use, or PostgreSQL performance.

The non-incremental Release solution build passed with zero warnings and
errors. `FILTER='FullyQualifiedName~Hygiene|FullyQualifiedName~ModuleBoundaryFixture'
tools/verify.sh` passed 216 API, 78 Host (five skipped), and 85 focused Core
tests. Repository and strike-service tests cover the strict expiry boundary,
refreshing an old queued state before cleanup, and retaining recent absent
states. The broad Core gate was not rerun because it previously stalled on
this checkout. No production rollout occurred.

## 2026-10-29 bounded Hygiene journal read

The Hygiene journal API returned at most 1–500 recent actions, but its service
loaded and sorted the entire action table before applying that limit. The
repository now orders by descending primary-key ID and applies the limit in
SQL. The service retains its existing 1–500 clamp, response shape, and order.
The query count remains one, and no schema migration or index was added. This
read serves the journal API, not queue refresh.

`python3 tools/benchmark_hygiene_recent_actions.py` compares the two reads on
the same in-memory SQLite action history, requesting 100 actions with ten
repetitions at each size. Times are median Python plus SQLite elapsed
milliseconds, including the old in-memory sort. The fixture has the existing
client/download/status index and an integer primary-key ID; it is synthetic
and contains completed actions only.

| Journal rows | Full journal then sort | SQL order and limit |
| --- | ---: | ---: |
| 100 | 0.064 ms; 100 rows | 0.060 ms; 100 rows |
| 10,000 | 9.330 ms; 10,000 rows | 0.103 ms; 100 rows |
| 100,000 | 84.471 ms; 100,000 rows | 0.062 ms; 100 rows |

SQLite reports a table scan for both queries, but the bounded query has no
temporary sort and returns only 100 rows. The repository test proves newest-ID
order and a limit above the available count. A non-incremental Release solution
build passed with zero warnings and errors. The focused `tools/verify.sh` run
passed 216 API, 78 Host (five skipped), and 86 Hygiene/module-boundary Core
tests. The broad Core gate remains open from the prior checkout stall. The API
also reads command history to build replacement outcomes, so these query-only
figures do not establish end-to-end journal latency or memory savings.
PostgreSQL was not profiled. No production rollout occurred.

## 2026-10-29 queue-refresh SQL read-path profile

`python3 tools/benchmark_hygiene_refresh_reads.py` compares the repository
read sequence before the four bounded Hygiene queue slices with the current
sequence. The disposable SQLite fixture uses the migration 12 and 13 state
and action indexes, 20 current queue identities, one rule, one expired state
per 100 non-current states, and one unfinished action per 1,000 actions. All
20 items are modeled as eligible for the existing-action lookup, making this
an intentionally read-heavy scenario. Each fixture size has the same number
of state and action rows. Five repetitions run in a fresh process for each
mode and size. The harness verifies that both modes select identical current
states, expired states, unfinished actions, and latest matching action IDs.
It leaves no fixture or listener behind.

| State and action rows each | Full-table reads: median ms / queries / rows materialized / RSS growth KiB | Bounded reads: same metrics |
| ---: | ---: | ---: |
| 100 | 1.480 / 25 / 2,302 / 92 | 0.396 / 26 / 62 / 32 |
| 10,000 | 139.986 / 25 / 230,002 / 9,400 | 1.119 / 26 / 251 / 1,044 |
| 100,000 | 1,451.520 / 25 / 2,300,002 / 86,972 | 10.810 / 26 / 2,141 / 9,092 |

Times are median Python plus SQLite time for the modeled reads, including
Python filtering in the prior path. RSS growth is the worker process's
resident-memory increase during five repetitions; it is not .NET heap or
application RSS. “Rows materialized” counts rows returned to Python, not all
rows SQLite scans. Query count rises by one because expiry now has its own
query. At 100,000 rows, the current path materializes 999 expired states,
100 unfinished actions, and 1,000 matching action-history rows. The identity
state and action queries use the existing client/download indexes. SQLite
still reports a scan for the expiry and non-completed-status predicates;
their benefit here is avoiding materialization of recent or completed rows.

This establishes the combined SQL read cost under a synthetic workload, not
actual `QueueService.Handle` time. The model omits state writes, retention
deletes, interrupted-action recovery, download-client calls, .NET allocation,
and queue/event overhead. PostgreSQL was not profiled. The actual refresh
comparison and recovery/preview proof remain open in section 2. No runtime
code or production state changed.

## 2026-10-29 actual Hygiene queue-refresh profile

`HygieneQueueRefreshBenchmark` calls the real `QueueService.Handle`, strike
observer, action processor, and SQLite repositories in the migrated Core test
database. The disposable fixture has 20 tracked, healthy Movie downloads and
100, 10,000, or 100,000 queue-state rows and the same number of action rows.
Current queue states are stale enough to cause 20 updates. One percent of the
non-current states are over 30 days old and are deleted on the first refresh:
zero, 99, or 999 rows. One action is recoverable at the
`replacement-requested` checkpoint; it completes without a matching tracked
download. Up to one in 1,000 other actions is failed with a future retry time,
so the recovery read sees them without calling a download client. The global
action gate is on, but healthy queue items cannot trigger cleanup.

The command was `. tools/env.sh && dotnet test
src/NzbDrone.Core.Test/Radarr.Core.Test.csproj -c Release --no-restore
--filter FullyQualifiedName~HygieneQueueRefreshBenchmark --logger
'console;verbosity=normal'`. The explicit, nonparallel fixture warmed the
empty service path, seeded each history, then timed the first refresh and five
subsequent refreshes in the same process. It recorded elapsed time for the
whole handler and both Hygiene phases, process-wide .NET allocated bytes, and
working-set RSS deltas. The timed region excludes fixture seeding and final
database assertions. The three cases passed, including the exact state count,
action count, one completed recovery, and 20 queue entries.

| Rows in each history | First refresh ms (strikes / actions) | First .NET allocated / RSS growth | Steady median ms (range; strikes / actions) | Steady median .NET allocated |
| ---: | ---: | ---: | ---: | ---: |
| 100 | 94.744 (80.647 / 7.948) | 1,169,560 / 6,164,480 bytes | 1.544 (1.520–2.260; 0.748 / 0.742) | 281,408 bytes |
| 10,000 | 349.425 (343.225 / 6.009) | 2,620,784 / 5,697,536 bytes | 3.344 (3.235–3.728; 1.733 / 1.493) | 293,144 bytes |
| 100,000 | 2,772.854 (2,754.633 / 17.997) | 17,269,856 / 9,580,544 bytes | 22.333 (21.605–27.843; 11.413 / 10.971) | 416,976 bytes |

The large first-refresh cost is in strike observation, which deletes expired
states one by one through the repository. The 100,000-row case removed 999
states and spent about 2.75 seconds in that phase. On later refreshes, those
states are gone and the 20 current states no longer need a write within one
minute; the whole handler's median was about 22 ms. The 100-row first result
still contains some cold repository and SQLite work despite the empty-path
warmup. RSS is a process working-set delta and is sensitive to allocation and
GC history across cases; it is not retained .NET heap. No download-client
call, replacement search, PostgreSQL run, production data, or direct SQL query
counter was included. The earlier SQL read-path profile supplies the read
comparison. The following slice supplies a same-fixture before/after
implementation comparison and addresses per-row retention deletion. A direct
preview/execution parity proof remains open. No runtime code or production
state changed in this profiling slice.

## 2026-10-29 Hygiene retention deletion slice

The first large queue refresh previously read every expired queue-state row
and deleted each one by ID. Strike observation now updates current downloads
first, then deletes states with `LastSeenAt < now - 30 days` in one SQL
statement. The strict cutoff, queued-download refresh, and retention period
are unchanged. No migration or index was added.

The same `HygieneQueueRefreshBenchmark` command and synthetic migrated SQLite
fixture described above ran before and after commit `e541e1639` on this
machine. Each size contains 20 healthy tracked downloads; 99 or 999 absent
states expire at the 10,000- and 100,000-row sizes. The timed region includes
the real `QueueService.Handle`, state writes and deletes, action recovery, and
decision processing, but excludes seeding. A first refresh occurs once per
fixture; five later refreshes supply each steady median. These results have
normal cold-run variation, especially at 100 rows.

| State and action rows each | First refresh before / after ms | Strike phase before / after ms | First .NET allocation before / after bytes | Steady median before / after ms |
| ---: | ---: | ---: | ---: | ---: |
| 100 | 89.939 / 93.271 | 77.643 / 80.311 | 1,124,152 / 1,157,744 | 1.575 / 1.455 |
| 10,000 | 349.796 / 69.484 | 343.192 / 62.759 | 2,620,784 / 1,024,192 | 3.294 / 3.478 |
| 100,000 | 2,754.661 / 116.985 | 2,737.597 / 97.722 | 17,295,408 / 1,139,472 | 23.600 / 22.462 |

At 100,000 rows, first-refresh time fell by 2,637.676 ms (95.8%). The
retention path no longer materializes 999 expired state rows or issues 999
individual deletes. Structurally, its one read plus 999 deletes became one
delete statement; a direct SQL query counter was not included in the timed
benchmark. Process working-set growth on the first 100,000-row refresh was
10,113,024 bytes before and 897,024 bytes after, but RSS deltas depend on GC
and earlier cases in the process. All three benchmark cases retained the
expected state count and completed the one recoverable action.

Repository and strike-service tests cover the strict cutoff and refreshing an
old queued state before deletion. The isolated focused run passed 24 Core
tests. `FILTER='FullyQualifiedName~Hygiene|FullyQualifiedName~ModuleBoundaryFixture'
tools/verify.sh` passed a non-incremental Release solution build, 216 API
tests, 78 Host tests (five skipped), and 86 focused Core tests. The prior broad
Core run stall was not retried. A direct preview/execution parity proof,
PostgreSQL profile, and production data remain open; no production rollout
occurred.

## 2026-10-29 frontend route acceptance and timing comparison

The checked-in browser benchmark now records Chrome's `ScriptDuration` and
`V8CompileDuration` at the cold Movies render and after first Settings
navigation. On the same machine and disposable empty Release fixture, the
original production UI at `633377d0a` and the clean current production UI
were measured with `python3 tools/benchmark_frontend_routes.py --profile fast
--samples 5` and `--profile slow --samples 3`. The slow profile uses 150 ms
latency and 200,000 bytes/s throughput. Each sample starts a fresh Chrome tab;
browser cache is disabled for the cold load and enabled for route navigation.
Numbers below are medians; the parenthesized range captures variation.

| Metric | Original UI | Current UI |
| --- | ---: | ---: |
| Fast initial browser transfer, bytes | 1,710,991 (1,710,983–1,716,053) | 1,606,788 (1,606,776–1,611,842) |
| Fast first Movies render, ms | 476.6 (458.4–729.2) | 472.6 (454.4–655.3) |
| Fast cold script / compile, ms | 85.0 / 0.1 | 85.7 / 0.1 |
| Fast first / repeat Settings navigation, ms | 22.3 / 21.8 | 23.9 / 21.4 |
| Slow initial browser transfer, bytes | 1,710,994 (1,710,986–1,716,044) | 1,606,790 (1,606,784–1,611,840) |
| Slow first Movies render, ms | 8,920.3 (8,842.6–8,959.1) | 8,452.6 (8,418.3–8,508.6) |
| Slow cold script / compile, ms | 76.0 / 0.1 | 82.1 / 0.2 |
| Slow first / repeat Settings navigation, ms | 22.9 / 21.4 | 184.3 / 22.1 |

Initial browser transfer fell by about 104 KB (6.1%). On the slow profile,
first Movies content appeared about 468 ms earlier at the median. First
Settings navigation cost about 161 ms more because its chunk loads on demand;
repeat navigation stayed around 21–22 ms. Cold script-duration ranges overlap
(fast original 72.6–95.8 ms, current 79.2–96.4 ms; slow original 72.6–79.8
ms, current 73.3–85.9 ms), so these samples do not establish an execution
improvement. Chrome reported only 0.1–0.2 ms of `V8CompileDuration`; that
counter does not isolate parsing or establish a parsing improvement. All 16
sampled flows had zero runtime exceptions. The visible Movies-content marker
is not a paint-timing API.

The clean production output contains 98 files and 22,955,736 bytes, down
214,727 bytes (0.9%) from the original 23,170,463 bytes. A development build
had left stale assets in `_output/UI`; a production rebuild over it reproduced
the same 98 production files byte for byte but left extra development files.
The directory was moved aside before the final production build and restored
after the baseline browser comparison. Total-output figures use only clean
production directories.

`corepack yarn typecheck`, `corepack yarn lint`, `corepack yarn
stylelint-linux`, a clean production webpack build, Python syntax compilation,
and `git diff --check` passed. The read-only
`python3 tools/check_frontend_routes.py` opened 22 representative routes at
both 1440 and 480 pixels on a disposable Release instance: Movies, TV, Music,
Activity Queue/History/Blocklist/Hygiene, Settings and all twelve leaf pages,
System, and System Status. All 44 checks rendered content without a page error,
runtime exception, pending loading state, or document overflow. This empty
fixture does not prove populated-library layouts, provider actions, or a
parser-only speedup. No production rollout occurred.

## 2026-10-29 Hygiene preview and executor parity proof

The new Core fixture uses the real evaluator, decision service, and action
service with one tracked failed Movie at the strike threshold, one below it,
and one private torrent at the threshold. On the same queue snapshot, preview
marked only the first item `WouldAct`; execution created exactly one completed
journal action for that download and made one client removal call. The
below-threshold and private items were not acted on. A second case verified
that disabling the matching rule after preview prevents a new action when the
executor reevaluates it. The existing API fixture separately verifies that
preview includes pending items without mutation; the executor receives only
tracked queue items. Completed journal actions and changed rules can also
prevent a later action, so same-snapshot candidate parity is the precise
claim.

The focused action fixture passed 35 Core tests. `FILTER='FullyQualifiedName~Hygiene|FullyQualifiedName~ModuleBoundaryFixture'
tools/verify.sh` passed a nonincremental Release solution build with zero
warnings and errors, 216 API tests, 78 Host tests (five skipped), and 88 Core
tests. Those focused fixtures also cover identity, retry/backoff, checkpoint
recovery, rule precedence, private torrent safety, deletion choices, and
replacement search. `git diff --check` passed. This proof changes tests and
documentation only; it does not add runtime work or alter production state.
The whole-refresh database query and row comparison is recorded below.

## 2026-10-29 whole-refresh SQL count and row comparison

The explicit `HygieneQueueRefreshBenchmark` now attaches SQLite `Trace2` to
each database connection during the first real `QueueService.Handle` refresh.
It counts executed SQL statements and result rows returned by SQLite, in
addition to elapsed time, .NET allocation, and working-set delta. The trace
starts after seeding, so fixture inserts are excluded. The same instrumented
test ran on the pre-retention revision `62f2336aa` in a disposable worktree
and the current code, with Release, the same 20 healthy tracked Movies, and
100, 10,000, and 100,000 rows in each history. The timed first refresh also
completes one recoverable action and expires zero, 99, or 999 absent states.
Each of the six cases passed state-count, action-count, recovery, and queue
assertions. Both runs used `dotnet test` with
`FullyQualifiedName~HygieneQueueRefreshBenchmark`,
`RunAnalyzers=false`, and `TreatWarningsAsErrors=false` in the same local
environment.

| Rows in each history | SQL statements before / after | SQLite result rows before / after | First refresh ms before / after | First .NET allocated bytes before / after | First RSS delta bytes before / after |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 100 | 27 / 27 | 41 / 41 | 92.066 / 114.719 | 1,147,632 / 1,189,928 | 6,078,464 / 6,066,176 |
| 10,000 | 126 / 27 | 149 / 50 | 335.305 / 65.981 | 2,637,008 / 1,057,104 | 5,492,736 / 1,101,824 |
| 100,000 | 1,026 / 27 | 1,139 / 140 | 2,719.000 / 106.585 | 17,213,224 / 1,179,464 | 10,022,912 / 917,504 |

At 100,000 rows the first refresh used 999 fewer statements and returned
999 fewer rows, matching the replacement of one expired-state SELECT plus
999 per-row DELETEs with one cutoff DELETE. First-refresh elapsed time fell
by about 96.1% in this traced comparison. The 100-row result is cold-run
variation, not an established regression; the earlier untraced comparison
also varied at that size. The steady refresh still scales with the number of
unfinished actions; the earlier read-path profile gives its separate SQL
sequence comparison. Trace row events count returned result rows, not rows
scanned inside SQLite. The callback adds overhead to timing and allocation,
but both revisions used the same harness. RSS deltas depend on process GC
history. This is a synthetic SQLite fixture, not PostgreSQL or production
data. The focused Core/API and same-snapshot preview/executor proofs above
passed. A nonincremental Release solution build had zero warnings and errors;
the API suite passed 216 tests, the Host suite passed 78 tests with five
skipped, and the focused Hygiene/module-boundary Core suite passed 88 tests.
The broad Core suite stopped making progress and was canceled after about
five minutes, consistent with the earlier local stall. No production rollout
occurred.

## 2026-10-29 shared UI duplication inventory and first extraction

The inventory compared repeated behavior, recent changes, and the risk of
merging media-specific workflows. The order below is for future bounded
slices, not a plan to collapse every similar-looking component.

| Rank | Candidate | Repeated logic and change evidence | Defect risk / decision |
| --- | --- | --- | --- |
| 1 | Library index filter connectors | Movie, Series, and Artist each repeated identity selectors, filter action dispatch, and `FilterModal` props across 166 lines. The three files had two commits in the preceding 30 days before this slice. | Low: the same `FilterModal` already owns the UI. Extracted only the connector wiring. |
| 2 | Activity filter connectors | History and Blocklist have five near-identical Movie/TV/Music connectors spanning 272 lines; their paths had two commits in the preceding 30 days. | Moderate: preserve each store slice, action, and custom-filter type when extracting. |
| 3 | Queue and activity presentation | Queue status/row and History/Blocklist rows have shared status and row presentation concerns; the four sampled paths had five commits in the preceding 30 days. | Moderate to high: event and queue meanings differ. Inventory specific shared branches before extracting. |
| 4 | Form controls | `FormInputGroup` already centralizes the primitive. Media Management and its naming form had four commits across the two sampled paths in 30 days. | No new extraction yet: repeated usage of a shared primitive is not duplicated behavior. |
| 5 | Manual import | Movie, TV, and Music interactive import rows each own media-specific selection and validation; three sampled paths had two commits in 30 days. | High: first isolate a proven common behavior without moving parsing, naming, quality, or import rules. |

The first slice replaces the three library index connectors with one shared
connector helper and explicit per-module selectors, filter action, and custom
filter type. The target was maintenance duplication, not runtime speed: the
same three files plus their shared helper fell from 166 to 78 source lines
(−88, −53%) on the same checkout. Their former identity `reselect` wrappers
did not transform data. No backend file or architecture allowlist entry
changed. The production webpack build reported zero errors and warnings;
frontend typecheck, lint, stylelint, and `git diff --check` passed. A disposable
Release browser check rendered 22 routes at 1440 and 480 pixels (44 checks)
with no error page, runtime exception, stuck loading state, or document
overflow, including the three library routes. The empty fixture disables
their Filter buttons, so populated-library filter interaction and explicit
error-state acceptance remain open. No production rollout occurred.

## 2026-10-29 Activity filter connector extraction

The five Movie, TV, and Music History and Movie and TV Blocklist filter modal
connectors repeated identity selectors, filter dispatch, and modal props. Each
now supplies its own store selectors, filter action, and custom-filter type to
the existing shared connector. Music Blocklist has no custom-filter connector
to replace. This is a maintenance slice; the five wrappers fell from 272 to
55 source lines (−217, −80%) on the same checkout. No backend file or module
boundary allowlist entry changed.

Frontend typecheck, lint, stylelint, a production webpack build, and `git diff
--check` passed. The existing disposable Release browser gate passed 44 route
checks at 1440 and 480 pixels with no error page, runtime exception, stuck
loading state, or document overflow. A focused disposable browser pass switched
among all five affected Activity views at both widths, opened each Custom
Filter modal, and found no runtime exception or document overflow. Blocking
each view's History or Blocklist API request produced the expected load-error
alert in all five views, without an error page. The fixture was empty, so
populated custom-filter save and result filtering were not exercised. No
production rollout occurred.

## 2026-10-29 indexer pruning inventory, first pass

The last documented owner Prowlarr snapshot (2026-10-25) had 17 configured
indexers: 15 Cardigann, one Knaben, and one SubsPlease. Its Cardigann IDs were
not recorded individually; one was a custom definition,
`torrentdownload-server01`. The snapshot also matched 13 Radarr indexers to
Prowlarr. It is historical evidence, not a current configuration export. At
this first pass, no Prowlarr database or running instance was available in the
local workspace; the production reconciliation is recorded below. See [the
importer evidence](importer.md#prowlarr-row-counts).

The checked-in native-definition directory has 144 tracked files, totaling
1,462,513 source bytes before build; that is an inventory size, not a projected
binary or image saving. The [upstream Prowlarr definition tree at
`5f5db4e58d06be18619c3a858b9a91ade6f341ca`](https://api.github.com/repos/Prowlarr/Indexers/git/trees/5f5db4e58d06be18619c3a858b9a91ade6f341ca?recursive=1)
contains 580 versioned indexer YAML definitions (579 under `v11`, one under
`v12`) and no `knaben.yml` or `subsplease.yml`.
Plattarr requests version 12 definitions and filters an explicit blocklist of
definitions moved to C#. A matching YAML name alone would therefore not prove
an available replacement in Plattarr; the definition must be served at the
requested version, pass the blocklist, and support the configured workflow.

| Configured provider in historical snapshot | Current replacement evidence | Decision for this slice |
| --- | --- | --- |
| 15 Cardigann entries | Already use the Cardigann implementation. The custom definition needs copying into `Definitions/Custom` when importing. Their IDs and current availability still need checking. | Keep the implementation and definition loader. |
| Knaben | Native C# provider; no same-name upstream YAML in the checked tree. It is also used in documented Movie, TV, and Music acceptance. | Retain; no verified Cardigann replacement. |
| SubsPlease | Native C# provider; no same-name upstream YAML in the checked tree. | Retain; no verified Cardigann replacement. |

Removing either native type would affect existing rows: provider startup
deletes stored definitions whose implementation is missing, and the upstream
importer marks a source indexer with an unavailable implementation as
`UNKNOWN_PROVIDER`. The Knaben import fixture explicitly exercises its type.
No native provider was removed, no runtime metric changed, and no production
rollout occurred. Before a removal slice, compare a sanitized current list
(name, implementation, definition ID only) with the native type list and the
definitions Plattarr actually loads, then inspect that candidate's migrations,
imports, tests, generated resources, and packaging paths. Keep at least one
working implementation per supported provider framework.

## 2026-10-29 live indexer reconciliation and candidate audit

The production Plattarr instance was checked read-only on the owner's host.
`GET /api/v3/indexer` and a read-only SQLite query of its active `Indexers`
table agreed: 17 configured rows, 15 enabled. There are 15 Cardigann rows
(13 enabled, two disabled), one enabled Knaben, and one enabled SubsPlease.
All 15 Cardigann rows have distinct nonempty definition IDs, including the
custom `torrentdownload-server01`; all 15 IDs appear in the instance's
`GET /api/v3/indexer/schema` response. Only implementation, enabled state,
definition ID presence, and counts were used. URLs, keys, cookies, settings,
and the private indexer list were neither printed nor committed. This live
inventory confirms the two native types identified in the historical snapshot
are still in use; both remain protected from pruning.

The following five *unconfigured* native types have same-ID Cardigann entries
in the live schema. This proves discoverability, not equivalent settings,
search results, or a safe migration for existing users. Their source files
total 77,219 bytes; that is a source-only upper bound for this five-file
slice, not a measured build or image saving.

| Native type | Cardigann definition ID | Native add schema | Replacement path before removal |
| --- | --- | --- | --- |
| CGPeers | `cgpeers` | Hidden as obsolete | Convert stored native rows to Cardigann settings and verify category/search behavior. |
| FunFile | `funfile` | Hidden as obsolete | Convert stored native rows to Cardigann settings and verify authentication and search behavior. |
| HDSpace | `hdspace` | Hidden as obsolete | Convert stored native rows to Cardigann settings and verify authentication and search behavior. |
| HDTorrents | `hdtorrents` | Still offered | Convert stored native rows, then check the add-indexer choice and search behavior. |
| PornoLab | `pornolab` | Hidden as obsolete | Convert stored native rows to Cardigann settings and verify authentication and search behavior. |

An exhaustive case-insensitive search of tracked files found each of these
names only in its own C# source file: no explicit migration, importer, test,
frontend, generated API resource, project, or packaging reference. The SDK
Core project includes C# sources by default, and provider discovery is based
on exported runtime types, so the absence of textual callers does not make
deletion safe. `IndexerFactory.GetDefaultDefinitions` excludes obsolete types
from the add schema, while `ProviderFactory.RemoveMissingImplementations`
deletes a stored row if its implementation disappears. The importer also
rejects an implementation that is no longer exported. No conversion of
existing native settings to Cardigann was proved, and no provider was removed.
The removal, build-size comparison, and contract verification checkboxes stay
open.

## 2026-10-29 native indexer compatibility gate

The target is stored-provider and import continuity during future pruning.
The baseline code deletes a stored row at startup when its native
implementation is absent, and the upstream importer skips a source row with
an unknown implementation. The preceding live inventory found five
unconfigured native candidates with same-ID Cardigann definitions, but a
definition ID does not convert their stored settings or prove equivalent
search behavior.

Commit `5c1a3690d` adds a database-backed startup test that retains one
stored, obsolete CGPeers row when its native implementation is available.
Five import cases cover CGPeers, FunFile, HDSpace, HDTorrents, and PornoLab
using their native implementation and settings-contract names. The existing
missing-implementation startup case still verifies that an unknown row is
deleted. This test-only slice changes no production code or build output;
source and image savings are zero. It is a guard against an unproved deletion,
not a provider migration or a Cardigann parity proof.

The focused Core fixtures passed 29 tests. `FILTER='FullyQualifiedName~IndexerServiceFixture|FullyQualifiedName~IndexersStageFixture|FullyQualifiedName~ModuleBoundaryFixture'
tools/verify.sh` passed a nonincremental Release solution build with zero
warnings and errors, 216 API tests, 78 Host tests (five skipped), and 33
focused Core tests. `git diff --check` passed. No production instance or
provider configuration was changed. The removal and savings checkboxes stay
open until settings conversion, search behavior, and affected contracts are
proved for an individual candidate.

## 2026-10-29 FunFile retirement feasibility check

The next implementation target is one unconfigured, obsolete native provider.
FunFile is the first plausible candidate: its native settings and the
[versioned Cardigann definition at the audited upstream revision](https://github.com/Prowlarr/Indexers/blob/5f5db4e58d06be18619c3a858b9a91ade6f341ca/definitions/v11/funfile.yml)
both use username and password. The definition adds optional fields and
categories; matching credentials alone does not convert stored rows or prove
equivalent searches. CGPeers is less suitable as a first conversion because
its native provider uses username/password and a freeleech-token choice while
the versioned definition requires a cookie. HDTorrents is still offered as a
native add choice. The other candidates still need individual analysis.

The native FunFile request generator always sends `s_title=1`, including for
IMDb lookup, and adds `s_desc=1` for IMDb lookup. The audited Cardigann
definition leaves `s_title` empty on IMDb lookup. The new focused fixture
locks the native movie-term and IMDb request shape, including category and
search values, so a replacement can be compared against current behavior.
This static difference is enough to defer deletion; it does not establish
whether the site's result set differs. Live authenticated search, result
parsing, settings conversion, and the saved-row/import gates remain open. No
provider, production configuration, or runtime path changed; source/build/
image savings for this characterization slice are zero.

The two focused Core tests passed. `FILTER='FullyQualifiedName~
FunFileRequestGeneratorFixture|FullyQualifiedName~IndexerServiceFixture|
FullyQualifiedName~IndexersStageFixture|FullyQualifiedName~ModuleBoundaryFixture'
tools/verify.sh` passed a nonincremental Release build with zero warnings and
errors, 216 API tests, 78 Host tests (five skipped), and 35 focused Core
tests. No production rollout occurred.

## 2026-10-29 FunFile settings conversion proof

The target is a lossless settings shape for a future native-to-Cardigann
conversion. A shared converter moves native `username` and `password` into
Cardigann `extraFieldData` and sets `definitionFile` to `funfile`. It keeps the
base URL, common indexer settings, and torrent settings. It rejects missing
credentials or pre-existing Cardigann fields rather than silently dropping
them. Synthetic credentials are used throughout.

A database-backed fixture inserts a converted Cardigann row and reads it back
with its definition ID, credentials, and minimum seeders intact. An upstream
fixture first imports a FunFile row as native, then applies the same converter
to the imported settings and checks the result. A separate fixture covers the
full settings round trip, case-insensitive source keys, and rejected shapes.
These tests prove that both sources can be converted by one rule. The startup
migration and upstream importer have not been switched to Cardigann: doing so
before request, login, and result parity is established could change behavior.
The versioned definition fetches `login.php` before submitting the page's form;
the native implementation posts to `takelogin.php`. The actual form action and
their IMDb `s_title` flags still need parity evidence. This proof changes no runtime path;
before and after source/build/image savings are zero. No production data or
provider configuration was used.

Commit `7aa898d77` passed `FILTER='FullyQualifiedName~
FunFileSettingsConverterFixture|FullyQualifiedName~FunFileRequestGeneratorFixture|
FullyQualifiedName~IndexerServiceFixture|FullyQualifiedName~IndexersStageFixture|
FullyQualifiedName~ModuleBoundaryFixture' tools/verify.sh`: nonincremental
Release build with zero warnings and errors, 216 API tests, 78 Host tests
(five skipped), and 42 selected Core tests. `git diff --check` passed. No
production rollout occurred.

## 2026-10-29 FunFile offline request and result parity check

The target is equivalent search requests and release identity before switching
stored or imported FunFile rows. The exact upstream `v11/funfile.yml` from
`5f5db4e58d06be18619c3a858b9a91ade6f341ca` is pinned as a test asset
(SHA-256 `96d57562b70fe227e9f9d69d288408619270951a80a706723b40c8363945898f`).
The fixture builds native and Cardigann requests from the same converted
synthetic settings. Movie-term query parameters match. IMDb lookup uses the
same search value, category, and `s_desc=1`, but Cardigann omits the native
`s_title=1`. The difference is now checked against the actual request
generators, not inferred only from YAML.

A mocked login page with a `takelogin.php` form action causes Cardigann to
fetch `login.php` and post the converted credentials to `takelogin.php`. This
corrects the earlier assumption that the YAML's login path is necessarily the
POST endpoint. The mock does not establish the live site's form action,
captcha behavior, or successful authentication.

Both parsers read the same synthetic two-row HTML. Title, details/download
URLs, category, size, file/grab/seed/peer counts, volume factors, ratio, seed
time, genres, description, and publish time match. Release GUID does not:
native FunFile uses the details URL while Cardigann uses the download URL.
That may change release identity and deduplication, so it is a retirement
blocker. The fixture represents one page shape; authenticated results and
other layouts remain unproved. No runtime provider, setting, or production
data changed; source/build/image savings remain zero.

The initial analyzer build exposed nine existing StyleCop errors in two queue
test files. Formatting-only commit `aa7b1e196` resolved them. Commit
`d42b6964d` adds the offline parity fixture. Together they passed
`FILTER='FullyQualifiedName~FunFileCardigannParityFixture|FullyQualifiedName~
FunFileSettingsConverterFixture|FullyQualifiedName~FunFileRequestGeneratorFixture|
FullyQualifiedName~IndexerServiceFixture|FullyQualifiedName~IndexersStageFixture|
FullyQualifiedName~ModuleBoundaryFixture' tools/verify.sh`: nonincremental
Release build with zero warnings and errors, 216 API tests, 78 Host tests
(five skipped), and 46 selected Core tests. `git diff --check` passed. No
production rollout occurred.

## 2026-10-29 FunFile candidate definition parity

The preceding offline comparison found two gaps: the audited Cardigann
definition omitted `s_title=1` for IMDb searches and used the download URL
as the release GUID, while native FunFile used the details URL. Commit
`cbbb43e27` adds an optional `guid` result field to the Cardigann parser.
Without that field, a definition still uses the download URL as its GUID.
An explicit GUID takes precedence even when it appears before the download
field, so existing definitions retain their behavior.

The focused fixture changes the pinned definition in memory, setting
`s_title` to `1` and adding a `guid` field derived from its already parsed
details URL. On the same synthetic IMDb criteria, the candidate request now
matches native path and query parameters. On the same synthetic HTML, the
candidate GUID matches native FunFile's details URL while the download URL
stays intact. This is a candidate proof, not a shipped definition update:
the live definition still comes from Prowlarr's updating package. No saved
row, importer, provider implementation, or production setting was switched.
Source/build/image savings are zero; authenticated search, login, and other
result layouts remain unverified.

`FILTER='FullyQualifiedName~FunFileCardigannParityFixture|FullyQualifiedName~
FunFileSettingsConverterFixture|FullyQualifiedName~FunFileRequestGeneratorFixture|
FullyQualifiedName~IndexerServiceFixture|FullyQualifiedName~IndexersStageFixture|
FullyQualifiedName~ModuleBoundaryFixture' tools/verify.sh` passed a
nonincremental Release build with zero warnings and errors, 216 API tests,
78 Host tests (five skipped), and 48 selected Core tests. `git diff --check`
passed. No production rollout occurred.

## 2026-10-29 FunFile expanded offline search parity

The earlier candidate check covered movie term and IMDb requests. Commit
`7242d9a0e` compares the same corrected in-memory definition with native
FunFile for TV term/season/episode and IMDb searches, plus music, book, and
basic term searches. Paths and query parameter dictionaries match for these
cases. The fixture now includes the native provider's TV anime and audiobook
category mappings; omitting the TV anime mapping initially produced a false
`c44` difference because a parent TV category expands to both TV and anime.

This is an offline request check with synthetic settings. It does not verify
the live login form, authenticated search results, or other site layouts. The
corrected definition remains an in-memory test candidate, and neither saved
rows nor imports use Cardigann for FunFile. Source/build/image savings are
zero; no production configuration changed. The authenticated, definition
delivery, conversion, and removal boxes remain open.

`FILTER='FullyQualifiedName~FunFileCardigannParityFixture|FullyQualifiedName~
FunFileSettingsConverterFixture|FullyQualifiedName~FunFileRequestGeneratorFixture|
FullyQualifiedName~IndexerServiceFixture|FullyQualifiedName~IndexersStageFixture|
FullyQualifiedName~ModuleBoundaryFixture' tools/verify.sh` passed a
nonincremental Release build with zero warnings and errors, 216 API tests,
78 Host tests (five skipped), and 49 selected Core tests. `git diff --check`
passed. No production rollout occurred.

## 2026-10-29 FunFile candidate delivery check

Commit `a0ee8ae14` puts the corrected FunFile YAML in a standalone test asset.
It differs from the pinned upstream definition only by setting `s_title` to
`1` for IMDb searches and using the parsed details URL as `guid`. The parity
fixture now loads that file instead of changing a definition object in memory.
The existing movie, TV, music, book, basic-search, and result-identity checks
therefore exercise the candidate file. The public login form checked on
2026-10-29 posts to `https://www.funfile.org/takelogin.php` and has username,
password, return-to, and submit fields; the mock login fixture now uses that
form shape. The page also loads a reCAPTCHA script. This public-page check
cannot prove that an authenticated login or search succeeds, or whether a
challenge appears during login. No test account was available.

The current [upstream v11 definition](https://github.com/Prowlarr/Indexers/blob/master/definitions/v11/funfile.yml)
still matches the pinned file byte for byte (SHA-256
`96d57562b70fe227e9f9d69d288408619270951a80a706723b40c8363945898f`).
The [upstream v11 schema](https://github.com/Prowlarr/Indexers/blob/master/definitions/v11/schema.json)
does not allow `guid` in search fields. Its field-name pattern lists
`details` and `download` but not `guid`. Definition delivery therefore needs
schema support or another compatible way to preserve release identity before
the corrected YAML can be shipped through the upstream feed. This test-only
slice changes no runtime provider, stored row, import path, or production
output; source/build/image savings are zero. Authenticated behavior, schema
compatibility, definition delivery, conversion, and native removal stay open.

`FILTER='FullyQualifiedName~FunFileCardigannParityFixture|FullyQualifiedName~
FunFileSettingsConverterFixture|FullyQualifiedName~FunFileRequestGeneratorFixture|
FullyQualifiedName~IndexerServiceFixture|FullyQualifiedName~IndexersStageFixture|
FullyQualifiedName~ModuleBoundaryFixture' tools/verify.sh` passed a
nonincremental Release build with zero warnings and errors, 216 API tests,
78 Host tests (five skipped), and 49 selected Core tests. A later focused
rebuild and run passed all seven FunFile parity tests. `git diff --check`
passed. No production rollout occurred.

## 2026-10-29 FunFile upstream delivery gate

Plattarr's definition service downloads the Prowlarr package and resolves
FunFile from that feed. The live `master/12/funfile` endpoint currently serves
the same bytes as the pinned v11 definition (SHA-256
`96d57562b70fe227e9f9d69d288408619270951a80a706723b40c8363945898f`).
The v12 feed endpoint falls back to the v11 FunFile definition; the upstream
tree has no separate v12 FunFile file. Both the [v11 schema](https://github.com/Prowlarr/Indexers/blob/5f5db4e58d06be18619c3a858b9a91ade6f341ca/definitions/v11/schema.json)
and [v12 schema](https://github.com/Prowlarr/Indexers/blob/5f5db4e58d06be18619c3a858b9a91ade6f341ca/definitions/v12/schema.json)
reject `guid` in search fields. The [Prowlarr Cardigann parser](https://github.com/Prowlarr/Prowlarr/blob/23b38d77843c5430214a2808532995d4b30bd31a/src/NzbDrone.Core/Indexers/Definitions/Cardigann/CardigannParser.cs)
also assigns the release GUID from `download` and has no explicit `guid`
field handler. The candidate's identity fix therefore needs upstream schema
and parser support as well as a definition change. The IMDb `s_title` change
still needs authenticated site evidence before upstream delivery.

A local bundled-definition attempt was dropped: it would make Plattarr
maintain another copy of an indexer already supplied by Prowlarr, and that
copy could drift when the feed updates. No runtime change from that attempt
remains. The existing test candidate is still useful for offline parity; the
native implementation, stored rows, and upstream imports remain untouched.
This is a delivery-path audit with zero source/build/image savings. The
authenticated behavior and upstream delivery checkboxes remain open.

## Progress log

| Date | Slice | Commit or PR | Result and evidence |
| --- | --- | --- | --- |
| 2026-10-29 | Plan created | — | No implementation or production change. |
| 2026-10-29 | Baseline and Settings route loading | `b5bdb529d` | [Measurements and verification](#2026-10-29-baseline-and-first-frontend-slice); no production rollout. |
| 2026-10-29 | Supported Moment locales | `3c677d58b` | [Build and browser comparison](#2026-10-29-supported-moment-locale-slice); no production rollout. |
| 2026-10-29 | Browser benchmark and dependency cleanup | `1a07793a7`, `03fe9f082`, `e90623499` | [Browser and build evidence](#2026-10-29-browser-comparison-and-dependency-audit); no production rollout. |
| 2026-10-29 | Hygiene action identity lookup | `520d809f4` | [Synthetic query comparison and focused verification](#2026-10-29-first-hygiene-action-lookup-slice); broad Core gate stalled; no production rollout. |
| 2026-10-29 | Interrupted Hygiene action read | `320ab6d1a` | [Synthetic read comparison and focused verification](#2026-10-29-interrupted-hygiene-action-read); broad Core gate remains open; no production rollout. |
| 2026-10-29 | Hygiene decision state lookup | `bfe1396e8` | [Indexed query comparison and focused verification](#2026-10-29-hygiene-decision-state-lookup); broad Core gate remains open; no production rollout. |
| 2026-10-29 | Hygiene strike observation state reads | `740367330` | [Synthetic query comparison and focused verification](#2026-10-29-hygiene-strike-observation-state-reads); broad Core gate remains open; no production rollout. |
| 2026-10-29 | Bounded Hygiene journal read | `19cdbfa10` | [Synthetic read comparison and focused verification](#2026-10-29-bounded-hygiene-journal-read); broad Core gate remains open; no production rollout. |
| 2026-10-29 | Queue-refresh SQL read-path profile | `8c21ed4b6` | [Synthetic combined read comparison](#2026-10-29-queue-refresh-sql-read-path-profile); actual refresh profile remains open. |
| 2026-10-29 | Actual Hygiene queue-refresh profile | `67605605d` | [Real-service SQLite timings and recovery proof](#2026-10-29-actual-hygiene-queue-refresh-profile); no production rollout. |
| 2026-10-29 | Hygiene retention deletion | `e541e1639` | [Same-fixture whole-refresh comparison and verification](#2026-10-29-hygiene-retention-deletion-slice); no production rollout. |
| 2026-10-29 | Frontend route acceptance and timing comparison | `4a88e6123` | [Browser timings and 44-route acceptance](#2026-10-29-frontend-route-acceptance-and-timing-comparison); no production rollout. |
| 2026-10-29 | Hygiene preview/executor parity proof | `dfec5dca4` | [Same-snapshot candidate and rule-change verification](#2026-10-29-hygiene-preview-and-executor-parity-proof); no production rollout. |
| 2026-10-29 | Whole-refresh SQL trace comparison | `97ec76b28` | [Same-fixture before/after statements, result rows, time, memory, and verification](#2026-10-29-whole-refresh-sql-count-and-row-comparison); no production rollout. |
| 2026-10-29 | Shared UI inventory and library filter connector | `f01b20951` | [Duplication ranking, source reduction, and frontend verification](#2026-10-29-shared-ui-duplication-inventory-and-first-extraction); populated filter interaction remains open. |
| 2026-10-29 | Activity filter connector extraction | `6c9af15a5` | [Five-view modal, error, and narrow-screen verification](#2026-10-29-activity-filter-connector-extraction); populated custom-filter behavior remains open. |
| 2026-10-29 | Indexer pruning inventory, first pass | `ffdb9e065` | [Historical configuration and replacement evidence](#2026-10-29-indexer-pruning-inventory-first-pass); superseded by the live reconciliation below; no provider removed. |
| 2026-10-29 | Live indexer reconciliation and candidate audit | `c9513b0cb` | [Read-only production counts, live Cardigann coverage, and dependency audit](#2026-10-29-live-indexer-reconciliation-and-candidate-audit); no provider removed. |
| 2026-10-29 | Native indexer compatibility gate | `5c1a3690d` | [Startup and import regression proof](#2026-10-29-native-indexer-compatibility-gate); no provider removed or production rollout. |
| 2026-10-29 | FunFile retirement feasibility | `42a55d926` | [Native request gate, replacement gap, and backend verification](#2026-10-29-funfile-retirement-feasibility-check); retirement and savings remain open. |
| 2026-10-29 | FunFile settings conversion shape | `7aa898d77` | [Stored-row and imported-source proof](#2026-10-29-funfile-settings-conversion-proof); runtime conversion and search parity remain open. |
| 2026-10-29 | FunFile offline request/result parity | `aa7b1e196`, `d42b6964d` | [Pinned definition, request/login/result comparison and backend gate](#2026-10-29-funfile-offline-request-and-result-parity-check); authenticated parity and provider retirement remain open. |
| 2026-10-29 | FunFile candidate definition parity | `cbbb43e27` | [Offline IMDb request and GUID candidate with parser support](#2026-10-29-funfile-candidate-definition-parity); shipped definition, authenticated parity, and retirement remain open. |
| 2026-10-29 | FunFile expanded offline search parity | `7242d9a0e` | [TV, music, book, and basic request comparison with the complete native category map](#2026-10-29-funfile-expanded-offline-search-parity); authenticated parity and retirement remain open. |
| 2026-10-29 | FunFile candidate file and public login form | `a0ee8ae14` | [Standalone corrected YAML, public form observation, and upstream schema limit](#2026-10-29-funfile-candidate-delivery-check); authenticated parity and retirement remain open. |
| 2026-10-29 | FunFile upstream delivery audit | `57a133562` | [Live feed, active schemas, parser behavior, and rejected local bundle](#2026-10-29-funfile-upstream-delivery-gate); authenticated parity and retirement remain open. |
