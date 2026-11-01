---
title: A blocklisted magnet can return from another indexer with the same hash
author: SixtyAteWhiskey
date: 2026-11-01
branch: codex/container-fatal-startup-exit-20261031
id: l-f2d0072e
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Core/Tv/Blocklisting/BlocklistService.cs
  - src/NzbDrone.Core/Hygiene/HygieneActionService.cs
  - src/NzbDrone.Core/Hygiene/HygieneQueueDecisionService.cs
  - src/NzbDrone.Core/Hygiene/HygieneQueueStrikeService.cs
---

## Decided
The live repeat-download case is caused by two identity rules interacting: TV blocklisting did not have a usable hash match for a replacement listing from another indexer, so the same underlying torrent could be grabbed again; Hygiene then treats its completed action for the same client, hash, and reason as permanently done. The preview does not check that journal state, so it continues to propose cleanup while strikes rise.

## Tried and ruled out
Treating the completed journal badge as proof that the torrent was gone was ruled out. The executor records completion after the qBittorrent delete request returns and tracking stops, without confirming later absence. Treating same title as sufficient identity was also ruled out; the live grab history showed matching torrent hashes across two indexers.

## Watch out
Do not simply remove the completed-action guard: it prevents repeated removal and replacement searches when the same client job remains visible. A fix needs to distinguish a confirmed new grab or reappearance from an unremoved job, and the preview should explain a completed-action hold.

In the code: `src/NzbDrone.Core/Tv/Blocklisting/BlocklistService.cs` uses the candidate hash when available and title/indexer otherwise; `src/NzbDrone.Core/Hygiene/HygieneActionService.cs` returns for a completed action before removal; `src/NzbDrone.Core/Hygiene/HygieneQueueDecisionService.cs` proposes actions without journal state; `src/NzbDrone.Core/Hygiene/HygieneQueueStrikeService.cs` retains absent identities until expiry.
