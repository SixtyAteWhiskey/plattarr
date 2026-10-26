# Plattarr functionality audit — 2026-10-25

## Current status

This document preserves the dated audit evidence below. Its original “local,
not deployed” labels describe the state during that audit and may have changed.
As of `79becc43b` on 2026-10-25, the Music Manual Import collision guard,
cover backfill, replacement-flow fixes, indexer catalog indicator, and Hygiene
preview/rule work are committed and pushed to `develop`. None of these updates
has been deployed to production. Production rollout remains a separate,
owner-controlled step.

## Scope and evidence

This began as an acceptance checkpoint for commit `421f334f6`; later passes
include local fixes that have not been deployed. The first pass used a clean
local build, the backend suite, frontend checks, a disposable instance on port
7881, and the production acceptance evidence recorded in the private handoff.
The first disposable instance had no real indexer, download client, or media.
The later pass used an isolated Plattarr and qBittorrent stack with synthetic
media on ports 7882 and 8086. No production action was taken in this audit.

The second pass used community reports to choose high-value scenarios. These
posts are examples of user workflows, not a statistical ranking of feature use:

- [Radarr quality profiles and upgrades](https://www.reddit.com/r/radarr/comments/13cje74/quality_profiles_and_upgrades/) and [custom format selection](https://www.reddit.com/r/radarr/comments/1gqchix/upgrade_based_on_custom_format/) emphasize monitoring, quality order, cutoff, and scores.
- [Sonarr season-pack import failures](https://www.reddit.com/r/sonarr/comments/1wbdydq/season_packs_always_fail_to_import/) and [queue cleanup after manual import](https://www.reddit.com/r/sonarr/comments/1w59qsf/activity_queue_clearing_downloaded_waiting_to/) emphasize multi-file mapping and recovery.
- [Stuck Sonarr/Radarr imports](https://www.reddit.com/r/selfhosted/comments/1x1c51z/how_do_you_handle_stuck_imports_in_sonarrradarr/) emphasize actionable queue errors, failed-download handling, category routing, and notifications.
- [Lidarr manual album import](https://www.reddit.com/r/Lidarr/comments/1rg3jn3/manual_import_failing_to_see_album/) emphasizes metadata-profile filtering and album selection.
- [Prowlarr category syncing](https://www.reddit.com/r/prowlarr/comments/tac2rf/categories_syncing_i_think_but_not_searching_or/) emphasizes indexer categories and per-app behavior.

## First-pass result

| Workflow | Evidence | Result |
|---|---|---|
| Startup, migration, API, and scheduled tasks | Fresh SQLite config reached migration 11. Status, task, health, and module API reads returned 200; health was empty. | Pass on scratch. |
| Movies | Existing production acceptance: Training Day was grabbed, imported once, matched to one file, and cleared from queue. Fresh scratch Movie UI/API rendered. | Pass for the observed single-movie path. |
| TV | Existing production acceptance: PLUR1BUS interactive search returned releases; one S01E01 grab imported once and cleared from queue. Fresh scratch TV UI/API rendered. | Pass for the observed single-episode path. |
| Music | Existing production acceptance: Trench imported 14/14 FLAC tracks and Blurryface 14/14; paths, sizes, history, and album UI matched. Fresh scratch Music UI/API rendered. | Pass for the observed albums. |
| Indexers and search | Existing production acceptance: 1337x, EZTV, Nyaa.si, and nekoBT connection tests passed; PLUR1BUS interactive search worked. Scratch indexer schema loaded. | Pass for those providers; flag interpretation remains open. |
| Download client, queue, history, and wanted | Existing production grabs/imports cleared queue with linked history. Scratch queue, history, blocklist, and wanted APIs and UI rendered. | Pass for completed imports; failure recovery needs proof. |
| Profiles, quality, and custom formats | Backend fixtures exist; scratch video and audio default profiles and Music settings rendered. | Basic path passes; TV flag custom formats remain incomplete. |
| Seerr compatibility | Earlier full scratch contract replay is recorded in the project handoff; this run confirmed Radarr/Sonarr status, profile, root folder, queue, tag, and language-profile reads using query-key authentication. | Read contract passes; mutations were not replayed in this run. |
| UI and assets | Scratch Movies, TV, Music, Activity, History, and settings rendered; History kept the media selector. The prior production UI corrections and 242/242 Movie/TV cover requests were verified after repair. | Current UI passes those checks; future imports need cover backfill. |

Verification commands and limits: the nonincremental Release build passed with
zero warnings; API, Host, and Core tests passed (206, 77, and 10,753,
respectively; 59 tests were skipped by their categories or fixtures). Frontend
ESLint and webpack passed. TypeScript had no `frontend/src` diagnostics but
exited with dependency declaration errors under `node_modules`. The fresh
config `defaults` liveproof passed.

## Second-pass gaps and plans

| Priority | Gap or unproved scenario | Plan and acceptance criterion |
|---|---|---|
| P0 | A future Radarr/Sonarr import can carry image URLs without cached covers. Cover backfill is implemented and tested locally, but not deployed. | Finish integration review, then include the backfill in the next controlled release. Recheck colliding Movie/TV IDs and retry behavior on the release build. |
| P1 | Failed-download and queue recovery pass for all three modules on disposable qBittorrent. A full Movie replacement grab now passes. TV/Music replacement grabs and category handoff semantics remain unproved. | Extend the fake-indexer replacement flow to TV and Music. Define whether a client category change should transfer ownership; test that rule. Preserve these recipes in `tools/liveproof` with an undo journal. |
| P1 | TV custom formats cannot yet compare a global indexer-flag condition. Sonarr and Radarr numeric flags overlap with different meanings. | Define a stable named global flag vocabulary at the API/storage boundary, translate Sonarr and Radarr flags there, and implement the TV flag specification. Add collision fixtures (for example numeric 8 and 128) and a release-decision test proving a TV custom format changes scoring as intended. |
| P1 | Season-pack recovery and Music manual import now pass in disposable tests. Music manual import exposed an overwrite bug, fixed locally. External album text search returned 503 during this pass. | Keep the new Music regression test. Add repeatable end-to-end proofs for season packs and manual imports. Investigate metadata search fallback when the external service fails; retain exact-ID lookup as a recovery path. |
| P2 | Indexer flag reporting and Prowlarr parity are not fully checked. A previous baseline appeared to mark every result freeleech; proxy and VIP health checks are not ported. | Capture sanitized results from the same indexer query in Prowlarr and Plattarr, compare raw attributes and mapped flags, then fix the mapper only if the values diverge. Add health checks for missing Cardigann definitions, proxy failure, and expiring VIP status where supported. |
| P2 | The TypeScript gate is noisy and public branding still points to Radarr services. Scratch startup logged HTTP 521 for upstream notification/time endpoints while core health and APIs remained available. | Make the frontend type check deterministic by resolving duplicate/incompatible dependency declarations. Separately replace or disable inappropriate Radarr update/notification links and titles before public release; verify a clean startup when those upstream services are unavailable. |

Cover backfill is implemented locally after this audit. A disposable import
downloaded and resized a poster despite a recent `LastInfoSync`; another
committed with a missing-cover warning while its image source was down, then
served the original and both resized images after the retry command ran. The
final local gate passed: 206 API, 78 Host, and 10,763 Core tests. The new tests
cover a committed report before image downloads, retry after a failure, and
repair of a missing resized image when its original is present.

The third pass below covers failed downloads and manual recovery. The fourth
pass proves Movie replacement grabbing through an indexer. Category behavior
and equivalent TV/Music replacement flows remain open.

## Third-pass disposable results

The isolated stack used a fresh Plattarr database, separate qBittorrent data,
throwaway torrent categories, and synthetic files. Production on port 7880 was
not changed.

| Scenario | Observation | Result |
|---|---|---|
| Failed grab/import recovery | Movie, TV, and Music each grabbed a synthetic torrent. Unsupported `.txt` payloads produced module-specific queue reasons. Removing each queue item with `removeFromClient=true`, `blocklist=true`, and `skipRedownload=true` cleared the queue and torrent, and produced `downloadFailed` history plus a blocklist entry in the correct module. | Pass for failed-import cleanup and blocklisting. |
| Optional redownload | Removing another failed Movie item with `skipRedownload=false` started and completed `MoviesSearch`. No indexer was configured in that run. | Search trigger passed; the later fourth pass proved replacement grabbing. |
| Client category change | Changing a grabbed Movie torrent from the Movie to the TV qBittorrent category left its Plattarr queue item linked to the original Movie. | Observed behavior; intended handoff semantics need a product decision and test. |
| TV season pack | A ten-file S02 pack had one file named E99 in place of E03. Nine files imported automatically; the queue identified the unmatched file. Manual Import mapped E99 to E03. All ten S02 episode files were present and the Plattarr queue cleared. | Pass. |
| Music metadata selection | An artist added with metadata profile `None` initially had no albums. Exact MusicBrainz album lookup/add for *Trench* populated fourteen tracks, and a tagged FLAC mapped to the intended track through Manual Import. Text album search returned 503 while the upstream Lidarr search returned 500. | Exact-ID selection and manual mapping pass; text search outage remains open. |
| Music replacement disabled | A second FLAC targeting an existing track destination overwrote it on the original published binary despite `replaceExistingFiles=false`. A local import guard now rejects that collision before changing the monitored release. Repeating the attempt on a scratch instance with the patched Core DLL preserved the original file ID, size, and monitored release. The progress message now reports `0 of 1 files` for that rejection. | Bug found and fixed locally; not deployed. |

After the Music import guard, `tools/verify.sh` passed with zero build warnings:
206 API, 78 Host, and 10,764 Core tests passed (54 Core skips). A subsequent
progress-message change passed a zero-warning Core test build and 54 focused
tests; the patched scratch run also confirmed that message. The full gate was
not repeated after that one-line message change.

## Fourth-pass automatic replacement proof

A fresh disposable Plattarr and qBittorrent stack used a fake Torznab indexer
with two approved releases for *Sintel* (2010). Release A held a `.txt` payload;
release B held a valid synthetic video. Both torrent contents were preplaced in
the isolated client's save path. This was a controlled Movie import failure,
not a real swarm or a production download.

Plattarr grabbed A once and reported `Invalid video file, unsupported extension:
'.txt'` in the queue. Removing it with `removeFromClient=true`,
`blocklist=true`, and `skipRedownload=false` recorded one `downloadFailed` event
and one Movie blocklist entry for A. The `MoviesSearch` command completed, B
was grabbed once, and B produced one `downloadFolderImported` event. The final
state had one Movie file on disk, `movieFileId` set, an empty Plattarr queue,
and no A torrent in the client. The disposable stack was removed after these
assertions. The result closed the Movie replacement-grab gap; the fifth pass
below covers TV and Music.

## Fifth-pass TV and Music replacement proof

A new disposable Plattarr and qBittorrent stack used separate configuration,
synthetic files under `/testdata`, and a fake Torznab indexer. It used the
`sha-421f334` image. No production or Scratch setting or media was changed.
The test stack had no real indexer or client credentials.

| Kind | Failed release and replacement | Final result |
|---|---|---|
| TV | A `.txt` payload produced `Invalid video file, unsupported extension: '.txt'`. Queue removal with client removal, blocklisting, and `skipRedownload=false` completed an `EpisodeSearch` and grabbed the next indexed release automatically. Two intermediate synthetic videos were rejected as samples (the second was five minutes long; this show's minimum is ten). After blocklisting each failed fixture, another automatic search grabbed the 11-minute release. | Three TV `downloadFailed` rows and three TV blocklist entries matched the rejected fixture releases. The final release had one `downloadFolderImported` event, one episode file on disk, and no queue row. |
| Music | A `.txt` payload produced `Invalid audio file, unsupported extension: '.txt'`. Removal with the same flags completed an `AlbumSearch` and automatically grabbed the next indexed release. Its three-second synthetic tracks scored 75.1% against an 80% album-match threshold; after blocklisting it, another `AlbumSearch` grabbed tagged tracks with the source album's durations and MusicBrainz IDs. | Two Music `downloadFailed` rows and two Music blocklist entries matched the rejected fixtures. The final release recorded `downloadImported`, created 11 track files, and left no queue row. |

The scratch health page's only item was the expected RSS warning because the
proof indexer had RSS disabled. All isolated containers, listener, media, and
temporary scripts were removed after the assertions. The production container
was checked read-only afterward.

## F7 category-switch recovery proof (2026-10-26)

A fresh loopback-only scratch instance running `sha-ad98a7c` used the existing
`hygiene-recovery` liveproof with local synthetic Newznab and SABnzbd services.
After a Movie release was grabbed and its grab-history row was recorded, the
SAB fixture changed that job's reported category from the Movie category to
the TV category before Plattarr refreshed the queue. A one-strike
Movie/client-failed rule and the global Hygiene gate were enabled only in this
scratch instance.

The action journal completed as Movie / `client-failed` at the
`replacement-requested` checkpoint. SAB removed the failed job with
`deleteFiles=0`, and the source file remained. Movie failed history and a
matching Movie blocklist entry were recorded. The replacement search returned
a different synthetic release; exactly one active replacement job entered the
Movie category and Plattarr tracked it as a Movie. The original release was
rejected by the normal blocklist decision. The gate reset to off, then the
scratch container, config, data, and newly pulled image tag were removed.

The category update and replacement transfer were simulated by the SAB
fixture; this proves Plattarr's history-first routing, action journal,
blocklist, and replacement handoff, not a real client's category API or a
completed download. No production or dev app was changed.

## Audit decisions and local changes

- A download with a Plattarr grab-history row keeps that media kind when its
  download-client category changes. Category routes only downloads without
  known grab history. A regression test now covers a category change after a
  Movie grab.
- Sonarr flag bits 8 and 128 already translate to the global named vocabulary
  during import. A new TV scoring test proves that `Internal` adds the profile
  score while the colliding Radarr `PTP_Golden` bit and `Nuked` do not.
- The observed Music text-search failure came from an upstream HTTP 500. The
  local code's actionable Plattarr 503 identifies that upstream status and points to
  exact `mbid:` lookup as the recovery path. Album and combined-search tests
  cover it. Exact-ID lookup itself remained functional during the disposable
  test.
- The owner feature requests are scoped in `docs/feature-adds-plan.md`.

## Local follow-up on the clean development base — 2026-10-26

The current change set is based on `7088996e8` (`origin/develop`) and has not
been deployed.

- The frontend now has a `typecheck` script and `skipLibCheck` in its project
  configuration. This keeps checking application source while skipping noisy
  dependency declaration diagnostics; a typecheck pass does not establish
  correctness of third-party declarations.
- The Hygiene action gate defaults off. The executor consumes the same
  decision service as preview, journals the reason/rule/checkpoint/recovery
  path, retains files when removing client jobs, and routes Movie, TV, and
  Music failures through the existing failure services. Unit fixtures cover
  all three media kinds and retries. A disposable Movie client/indexer
  end-to-end proof passed with local synthetic Newznab and SABnzbd fixtures;
  the action gate remains off by default.
- The liveproof harness has a read-only Hygiene idle-state check and a
  same-query Prowlarr/Plattarr flag comparison. The comparison passed against
  local app APIs using the same synthetic Torznab feed. A later real
  single-indexer capture matched 32 releases with no mapped-flag differences.
  The unmatched releases were captured under different query terms and
  categories, so full search-result parity remains untested; no mapper change
  is claimed.
- `docs/first-run.md` now covers the request, download, import, library scan,
  compatibility API checks, security boundary, and current Jellyfin/Emby/Plex
  and Jellyseerr/Ombi limits. A later disposable Ombi v4.53.10 AddOnly movie
  and one-episode TV runtime capture passed; other Ombi modes remain unverified.
- The UI's inherited Radarr help and update links now point to Plattarr's
  first-run guide and release page; upstream donations are labeled as upstream
  projects. Radarr package updates, hosted notification checks, and the hosted
  time check are disabled until Plattarr-owned services exist. The operating-
  system clock remains outside Plattarr health reporting.

After all code changes, `tools/verify.sh` passed with zero build warnings:
206 API, 78 Host, and 10,768 Core tests passed (54 Core skips). The cover
backfill and Music collision guard are in local commit `ff1d9e1ec`. The audit
follow-ups were also committed locally. Neither commit was pushed or deployed.
