---
title: TV and Music need torrent hash blocklist adapters before another automatic grab
author: SixtyAteWhiskey
date: 2026-11-02
branch: codex/torrent-blocklist-regrab
id: l-1bf827ef
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Core/Download/ReleaseBlocklist.cs
  - src/NzbDrone.Core/Download/TorrentClientBase.cs
  - src/NzbDrone.Core/Movies/Blocklisting/MovieBlocklist.cs
  - src/NzbDrone.Core/Tv/Blocklisting/BlocklistService.cs
  - src/NzbDrone.Core/Music/Blocklisting/BlocklistService.cs
---

## Decided
Prioritize wiring TV and Music into the shared late torrent-hash blocklist check, with regression tests for the same hash offered by a second indexer. The repeat-cleanup action handles regrabs after they happen, but prevention at grab time is the next focused fix.

## Tried and ruled out
Enabling the per-indexer reject-blocklisted-hash setting is not the remedy for the observed repeat. In a 2026-11-02 read-only production check, both indexers involved in the two grabs, and all 17 configured indexers, already had that flag enabled. The shared `ReleaseBlocklist` asks registered `IMediaBlocklist` implementations; only Movies currently implements that interface, so it returns false for TV and Music even when their blocklist services know the hash.

## Watch out
Do not infer the exact earlier decision-stage values from serialized grab history. `TorrentClientBase.EnsureReleaseIsNotBlocklisted` runs its late check only when `TorrentInfo.InfoHash` was initially blank and the grab was not interactive. Test that case with a TV and Music blocklist hit, plus the known-hash decision path, and ensure a same-hash candidate from another indexer never reaches the download client. Preserve deliberate interactive-search behavior. Use disposable fixtures, not a live regrab.

In the code: `ReleaseBlocklist.IsTorrentHashBlocklisted` selects an `IMediaBlocklist` by media kind; `MovieBlocklist` is the only implementation. `Tv.Blocklisting.BlocklistService.BlocklistedTorrentHash` and `Music.Blocklisting.BlocklistService.BlocklistedTorrentHash` already provide the scoped lookups that adapters can call. `TorrentClientBase.EnsureReleaseIsNotBlocklisted` uses the shared check when the indexer flag is on.
