# Feature adds plan — 2026-10-25

Source: the owner's `Plattarr Feature Adds` note. This is a proposed sequence and
acceptance scope, not a request to deploy any of it during the functionality audit.

## 1. Explain and verify the existing workflow

Build a first-run guide that traces a request through lookup, monitoring, indexer
search, release choice, download client category, queue, import, rename, media
server scan, and request-app status. Each step should link to its setting and
show a read-only check. Add setup paths for Jellyfin, Plex, and Emby, then for
Jellyseerr and Ombi. Reuse the current Seerr compatibility routes for
Jellyseerr; capture Ombi's actual calls before claiming compatibility. Include
API examples for Radarr and Sonarr clients, with the required URL base, API-key
placement, profile and root-folder lookups, and one isolated contract replay.

Acceptance: a fresh user can connect one client of each supported class, make a
test request, and see each lifecycle state without reading source code. The
guide must distinguish a missing search result, a blocked release, a client
failure, and an import failure.

## 2. Make failures actionable

Give common failures a stable reason code, a short cause, evidence from the
relevant service, and one or two safe recovery actions. Start with queue import
warnings, metadata lookup outages, indexer connection/category errors, and
download-client category mismatch. Keep technical detail in expandable logs and
redact credentials, query keys, and private paths before display or export.

Acceptance: a failed disposable download identifies the media kind, reason,
affected file, next action, and whether a replacement search was queued.

## 3. Expose the indexer contract

Inventory the Prowlarr API surface that intended clients actually call. Begin
with per-indexer Torznab/Newznab feeds, capability and category responses,
search, and grab/download links; then evaluate `/api/v1/search`, indexer status,
and management routes as separate compatibility work. Use sanitized contract
replays from at least one consuming application. Keep existing module category
routing and named indexer flags at the shared boundary.

Prowlarr documents both its [API](https://prowlarr.com/docs/api/) and
[search endpoints](https://wiki.servarr.com/en/prowlarr/search). Acceptance is a
passing client contract, including authentication and category behavior, for
each advertised route.

## 4. Build Cleanuparr-style Hygiene into Plattarr

The target is the functionality of a Cleanuparr-like app **inside Plattarr**.
Hygiene should use Plattarr's own download clients, tracked downloads, queue,
history, blocklists, search commands, media ownership, notifications, and
database. It should run in the same service and UI, with no second app to
configure or keep in sync. Use the UI label **Hygiene**.

Build a parity matrix from [Cleanuparr's feature list](https://cleanuparr.github.io/docs/features/)
and its [Queue Cleaner](https://cleanuparr.github.io/docs/configuration/queue-cleaner/),
[Seeker](https://cleanuparr.github.io/docs/configuration/seeker/),
[Download Cleaner](https://cleanuparr.github.io/docs/configuration/download-cleaner/),
[Malware Blocker](https://cleanuparr.github.io/docs/configuration/malware-blocker/),
and [Blacklist Synchronizer](https://cleanuparr.github.io/docs/configuration/blacklist-sync/)
settings. The complete target includes:

- Queue cleaning: consecutive strikes for errored, stalled, slow, stuck in
  metadata, and failed-import downloads; progress-based resets; rule thresholds;
  private-torrent and ignored-item handling; failed-import reason filters; and
  tightly constrained force-import recovery.
- Searching: immediate replacement searches after removal, plus paced cycles
  for missing Movies, TV episodes or seasons, and Music albums; quality-cutoff
  and custom-format-score upgrades; monitored, tag, release-age, and active
  download limits. Share Plattarr's existing search commands and profiles.
- Download cleanup: ordered seeding rules for ratio and time, stop or delete
  actions, dead-torrent and unlinked-download handling, hardlink and cross-seed
  awareness, and orphaned-file scanning with quarantine and review before purge.
- Content protection: file blocklists and allowlists, configurable action for
  mixed-content downloads, private-torrent policy, and qBittorrent excluded-file
  list synchronization.
- Visibility: per-rule previews, strike counts, reasons and actions in History,
  notifications, health checks, and summary statistics. A rule must explain why
  it did or did not act on a download.

First inventory overlapping queue cleanup, blocklist, redownload, retention,
and download-client settings. Move their UI controls under Hygiene and migrate
stored values while keeping existing API behavior. A single rule engine should
evaluate snapshots and produce proposed actions; a separate executor should
apply those actions through Plattarr's existing queue, client, and search
services. Store strike state, search-cycle state, and an action journal in the
main database. Scope rules by media kind and client. Define client capability
checks so settings appear only where the configured client supports them.
Extend Plattarr's client adapters when parity requires a client it does not
yet support.

Deliver this in usable slices, beginning with read-only evaluation and queue
strikes, then replacement and proactive search, seeding rules, orphan handling,
content protection, and reporting. Keep each automatic action disabled until
its rule is configured. Use dry-run previews and isolated end-to-end tests for
every action, especially category changes, private torrents, hardlinks, and
file deletion. The staged delivery is a route to the full built-in feature set.

Acceptance: every item in the parity matrix has a native Plattarr workflow and
an isolated proof; the main UI and API expose one authoritative setting per
behavior; preview and execution select the same candidates; and every action
records its reason and recovery path. Category changes must preserve the media
kind recorded by grab history. No separate Cleanuparr process is required.

## 5. Security and size gates before a public build

Scan tracked files, Git history, image layers, generated frontend assets, and
sample configuration for personal information and secrets. Rotate anything
found before publishing. Add dependency and container vulnerability scans,
review authentication and authorization on compatibility routes, and redact
secrets from logs and support exports. Onboarding should explicitly warn against
direct DMZ or public-WAN exposure and explain a secured reverse proxy or VPN.

Measure startup time, memory, frontend bundle size, and hot request paths before
structural cleanup. Remove duplication or unused providers only where those
measurements or supported workflows justify it. Keep the current acceptance
and import gates while refactoring.

Acceptance: reproducible clean scans, contract tests, no regression in the
measured budget, and an isolated security review of externally reachable routes.

## Sequence

Finish the functionality audit and its local fixes first. Then ship the
workflow guide and errors, prove request-app and indexer compatibility, build
the complete Hygiene feature set in tested increments, and run the public-build
security and size gates.
Any production rollout remains a separate controlled step.

## Implementation checkpoint — 2026-10-25

The first native Hygiene preview and monitoring-rule slices are committed and
pushed to `develop` at `79becc43b`; they have not been deployed to production.
`GET /api/v3/hygiene/preview`
evaluates the current tracked and pending queue without changing clients or files. The
Activity > Hygiene page shows each download's media kind, client, stable reason
code, short explanation, and suggested manual recovery step. It distinguishes
ignored, failed, import-blocked, warning, unavailable-client, pending, and
otherwise healthy items. All assessments currently report `wouldAct: false`.

Migration 12 adds persisted per-client/download strike state and scoped
monitoring rules. Rules select a reason, media kind (or all),
and download client (or all), with a strike threshold and minimum interval.
Specific rules override broad rules, including when a specific rule is
disabled. Progress, recovery, and a changed reason reset consecutive strikes.
Strike tracking stays off until a matching monitoring rule is enabled. The
Hygiene page can create, edit, and remove these rules and shows the count and
eligibility explanation. It remains read only with respect to downloads:
`eligible` can become true, while `wouldAct` stays false. A future executor
must consume the same `IHygieneQueueDecisionService` output, add a separate
action enablement gate and journal, and pass isolated removal tests before any
client or file action is enabled.

This is not Cleanuparr parity. The other feature areas above remain open.

## Local follow-up — 2026-10-26

On the clean base `7088996e8` (`origin/develop`), a disabled-by-default action
gate and journal now cover the first failed-download/import-blocked slice.
Movie, TV, and Music unit recovery fixtures exercise retained files and
checkpoint resumption. A disposable Movie end-to-end proof passed with local
synthetic Newznab and SABnzbd fixtures. The indexer comparison harness passed
against local Prowlarr and Plattarr APIs using the same synthetic Torznab feed;
a same-indexer comparison with real tracker results remains outstanding.
Ombi contract capture is also outstanding. These changes have not been
deployed.
