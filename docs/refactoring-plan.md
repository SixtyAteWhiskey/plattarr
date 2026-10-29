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
- [ ] Time `/ping`, status, queue, Hygiene preview, and a representative library
  route at p50 and p95. Profile one queue refresh with small and larger
  synthetic action/state histories; count database queries and rows read.
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
- [ ] Compare initial transfer, parse/execute time, route navigation time, and
  total output against the baseline. Confirm no unacceptable regression for
  repeat navigation or slow connections.
- [ ] Run frontend typecheck, lint, stylelint, production build, and browser
  acceptance across Movies, TV, Music, shared pages, and narrow layouts.

## 2. Bound Hygiene work during queue refresh

`QueueService.Handle` observes strikes and processes Hygiene actions on each
tracked-download refresh. `HygieneQueueStrikeService` and
`HygieneQueueDecisionService` load all states. `HygieneQueueActionService`
loads all actions for recent entries, interrupted work, and existing-action
checks. The action journal is currently small, so this is a scalability
candidate rather than a proven production bottleneck.

- [ ] Profile the refresh path and inspect existing database indexes and query
  plans before changing repository methods.
- [ ] Replace one full-table read at a time with a bounded query where the
  profile shows useful savings. Consider recent actions, unfinished actions,
  identity lookups, and state retention separately.
- [ ] Preserve action identity, retry/backoff, checkpoint recovery, rule
  precedence, private-torrent safety, file-deletion choices, and replacement
  search behavior. Preview and execution must select the same candidates.
- [ ] Compare database rows read, query count, refresh time, and memory at the
  same fixture sizes. Run focused Core/API tests and isolated recovery and
  preview/execution proofs before the full backend gate.

## 3. Remove proven duplication

Consolidate repeated UI or workflow code only where the behavior is genuinely
shared. Prefer a small common component or helper with explicit media-kind
inputs over a generic framework that hides the differences among modules.

- [ ] Inventory repeated filter dialogs, form controls, queue/activity
  presentation, and manual-import UI. Rank candidates by repeated logic,
  change frequency, and defect risk; file length alone is not a reason.
- [ ] Extract one pattern at a time and remove the old copies in the same
  change. Keep module-specific parsing, naming, quality, and import rules in
  their owning modules.
- [ ] Keep the architecture allowlist shrinking or unchanged. Verify behavior
  for all affected media kinds, including empty, error, and narrow-screen
  states where the UI is involved.

## 4. Prune unused surface area with evidence

Previous provider pruning is documented in [pruned-providers.md](pruned-providers.md).
The remaining native indexer definitions are a possible size and maintenance
target, but removal needs an inventory of configured and supported workflows.
Windows/updater paths still participate in build paths and need a product
support decision before any removal.

- [ ] Inventory configured indexers and compare native definitions with
  Cardigann coverage; document the replacement path for each candidate.
- [ ] Check whether a candidate is referenced by migrations, imports, tests,
  generated API resources, or active build and packaging paths.
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

## Progress log

| Date | Slice | Commit or PR | Result and evidence |
| --- | --- | --- | --- |
| 2026-10-29 | Plan created | — | No implementation or production change. |
| 2026-10-29 | Baseline and Settings route loading | `b5bdb529d` | [Measurements and verification](#2026-10-29-baseline-and-first-frontend-slice); no production rollout. |
| 2026-10-29 | Supported Moment locales | `3c677d58b` | [Build and browser comparison](#2026-10-29-supported-moment-locale-slice); no production rollout. |
| 2026-10-29 | Browser benchmark and dependency cleanup | `1a07793a7`, `03fe9f082`, `e90623499` | [Browser and build evidence](#2026-10-29-browser-comparison-and-dependency-audit); no production rollout. |
| 2026-10-29 | Hygiene action identity lookup | `520d809f4` | [Synthetic query comparison and focused verification](#2026-10-29-first-hygiene-action-lookup-slice); broad Core gate stalled; no production rollout. |
| 2026-10-29 | Interrupted Hygiene action read | `320ab6d1a` | [Synthetic read comparison and focused verification](#2026-10-29-interrupted-hygiene-action-read); broad Core gate remains open; no production rollout. |
