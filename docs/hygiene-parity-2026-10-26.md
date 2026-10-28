# Hygiene and Cleanuparr parity — 2026-10-26

## Verdict and scope

Hygiene is **not yet a 1:1 Cleanuparr implementation**. It supports scoped
queue monitoring and failed-download recovery, and can now remove eligible
stalled and slow downloads after their strike rules authorize action.
Cleanuparr's Queue Cleaner still offers filters and recovery modes that Hygiene
does not implement.

This comparison uses Cleanuparr's [feature list](https://cleanuparr.github.io/docs/features/),
[Queue Cleaner](https://cleanuparr.github.io/docs/configuration/queue-cleaner/),
[supported-client matrix](https://cleanuparr.github.io/docs/supported-apps/),
[Seeker](https://cleanuparr.github.io/docs/configuration/seeker/),
[Download Cleaner](https://cleanuparr.github.io/docs/configuration/download-cleaner/),
[Malware Blocker](https://cleanuparr.github.io/docs/configuration/malware-blocker/),
and [Blacklist Synchronizer](https://cleanuparr.github.io/docs/configuration/blacklist-sync/)
documentation as read on 2026-10-26. It checks Plattarr's current local
checkout; no Cleanuparr executable was run side by side.

## Queue Cleaner comparison

| Behavior | Cleanuparr contract | Plattarr Hygiene evidence | Result |
| --- | --- | --- | --- |
| Scoped strikes | Queue Cleaner uses consecutive runs and a minimum of three strikes for enabled failed import, stalled, and slow rules. | `HygieneQueueStrikeService` persists strikes by client and download ID. `HygieneStrikeCalculator` uses a configured minute interval; rules allow three to 100 strikes and match by reason, media kind, and client. Migration 15 raises earlier one- or two-strike rules to the minimum without lowering stricter rules. | Partial: the three-strike minimum is aligned, while the interval-based counting model still differs. |
| Failed import recovery | Can remove and block after strikes, with include/exclude message patterns, optional force import, private-torrent controls, and a check for a missing client job. | `import-blocked` can reach the gated executor. The action journal and normal failed-download handler provide removal, blocklisting, and conditional replacement. Pattern filters, force import, and those private/client options are absent. | Partial. |
| Failed SABnzbd jobs | Failed SAB jobs still in an *arr queue are removed and searched again; this Cleanuparr behavior is always on. | `client-failed` is supported for tracked downloads only after a scoped rule and the global action gate allow it. A disposable synthetic SAB proof passed removal and replacement. | Partial: prerequisites and coverage differ. |
| Stalled downloads | Queue Cleaner can act after strikes, with privacy and completion scopes, progress reset settings, and a qBittorrent metadata-stall option. | qBittorrent `stalledDL` is classified and can collect strikes; an enabled rule plus the global gate can now authorize removal after the threshold. The client job is removed with files retained, then the normal failed-download path handles blocklisting and configured replacement. No completion/privacy rule scope or metadata-stall action exists. | Removal supported for tracked, public/verified items; broader controls remain absent. |
| Slow downloads | Queue Cleaner can act on low speed or elapsed time, with privacy, size, completion, and alternate-speed safeguards across supported torrent clients. | qBittorrent reported speed below a matching rule's threshold is classified; it can collect strikes while progress continues. An enabled rule plus the global gate can now authorize removal after the threshold, with files retained and the normal failed-download path used. No elapsed-time, size, completion, or alternate-speed rule controls exist. | Removal supported for tracked, public/verified items; broader controls remain absent. |
| Ignored and private items | Configurable ignore patterns include hashes, categories, tags, trackers, and client names; private-torrent behavior has separate controls. | Plattarr's tracked `Ignored` state is excluded. Private or unverified torrents are held from automatic action. Pattern-based Hygiene ignores and private deletion controls are absent. | Partial. |
| Removal and replacement | Queue Cleaner can remove or change category; Seeker handles replacement when enabled. | The tested executor removes the client job with `deleteFiles=false`, records failed history and blocklist, and invokes Plattarr's configured failed-download replacement path. The synthetic category-switch proof kept Movie ownership and queued one distinct Movie replacement. Category-change cleanup is absent. | Tested overlap for one Movie failure path. |

## Other Cleanuparr areas

| Area | Current Hygiene status |
| --- | --- |
| Seeker's paced missing and quality-upgrade cycles | No native Hygiene search-cycle state or proactive search rules. Plattarr's existing failed-download handler can request an immediate replacement for the tested recovery path. |
| Download Cleaner seeding, unlinked-download, and orphaned-file workflows | No native Hygiene equivalents. Stalled/slow removal handles the qBittorrent signals above, but does not delete files or sweep untracked client jobs. |
| Malware Blocker file allowlists/blocklists and mixed-content policy | No native Hygiene equivalent. Plattarr's release blocklist serves a different purpose. |
| qBittorrent excluded-file blacklist synchronization | No native Hygiene equivalent. |
| Strike/removal notifications, Hygiene health checks, and full statistics | The UI has reason totals, saved per-rule queue counts, per-item reasons, and an action journal. Those Cleanuparr reporting integrations are not verified as equivalent. |
| Client coverage | The tested recovery proof uses a synthetic SABnzbd service. Stalled and speed signals are currently sourced from qBittorrent only. The Cleanuparr supported-client matrix covers additional torrent clients; equivalent Hygiene signals and actions are not established. |

## Verification on the local checkout

- The focused Core Hygiene suite passed **40 tests** after this change. It
  covers stalled/slow decisions, global-gate behavior, torrent-only eligibility,
  and removing active torrent jobs while retaining source files.
- The full Release verification passed: solution build with zero warnings,
  **10,807 Core tests** (53 skipped), **209 API tests**, and **78 Host tests**
  (5 skipped).
- Frontend typecheck, lint, stylelint, and production webpack build passed.
- A Release console build succeeded with zero warnings. A disposable instance
  bound only to `127.0.0.1:17882` passed the read-only Hygiene check before and
  after the proof. Afterward, the gate was off, its journal had one completed
  action, and no action needed recovery.
- `tools/liveproof/run.py hygiene-recovery --confirm-disposable` passed all
  checks against that build with local synthetic Newznab and SABnzbd services.
  It verified the Movie grab, mock SAB category change to TV, client removal
  with files retained, Movie failed history and blocklist, one distinct Movie
  replacement, and preserved Movie tracking. The disposable instance, fixture
  files, and listener were removed after the run.

The live proof does not exercise a real qBittorrent category API, a completed
replacement transfer, a real tracker, or a Cleanuparr process. It does not
prove full feature parity or the uncommitted UI's visual behavior on production.

## Reproduction

From the repository root with the SDK pinned by `global.json`, load the
solution-level StyleCop settings for direct project invocations:

```bash
dotnet test src/NzbDrone.Core.Test/Radarr.Core.Test.csproj \
  --filter 'FullyQualifiedName~Hygiene' -p:SolutionDir="$PWD/src/"
dotnet test src/NzbDrone.Api.Test/Radarr.Api.Test.csproj \
  --filter 'FullyQualifiedName~Hygiene' -p:SolutionDir="$PWD/src/"
```

The isolated API proof is described in [liveproof](../tools/liveproof/README.md).
It requires a fresh disposable localhost instance, an empty library and
journal, and the explicit `--confirm-disposable` flag. Never point that
mutation proof at the owner's production app.
