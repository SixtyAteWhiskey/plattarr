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
- [ ] Build the current frontend in production mode and record initial route
  transfer size, total and per-chunk JS/CSS sizes, gzip sizes, and source-map
  sizes separately. Capture a webpack module breakdown for the largest chunks.
  HTML/JS/CSS and the module breakdown are recorded below; fonts, images, and
  actual browser transfer remain to be captured.
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
- [ ] Audit production-only build inputs and direct dependencies. Remove or
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

## Progress log

| Date | Slice | Commit or PR | Result and evidence |
| --- | --- | --- | --- |
| 2026-10-29 | Plan created | — | No implementation or production change. |
| 2026-10-29 | Baseline and Settings route loading | `b5bdb529d` | [Measurements and verification](#2026-10-29-baseline-and-first-frontend-slice); no production rollout. |
