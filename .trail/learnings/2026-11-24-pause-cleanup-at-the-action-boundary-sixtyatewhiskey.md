---
title: Pause cleanup at the action boundary when the download network fails
author: SixtyAteWhiskey
date: 2026-11-24
branch: codex/final-release-20261123
id: l-7928981e
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Core/Download/Clients/QBittorrent/QBittorrent.cs
  - src/NzbDrone.Core/Download/Clients/QBittorrent/QBittorrentProxyV2.cs
  - src/NzbDrone.Core/Hygiene/HygieneActionService.cs
  - src/NzbDrone.Core/Hygiene/HygieneQueueStrikeService.cs
  - src/NzbDrone.Core/Hygiene/HygieneRuleSafeguards.cs
  - src/NzbDrone.Core/Hygiene/HygieneContentDecisionService.cs
  - src/NzbDrone.Core/Hygiene/HygieneImportRescueService.cs
---

## Decided
Treat a disconnected or unreadable modern qBittorrent connection status as a hold for Hygiene strikes, queue actions, content actions, and import rescue. Recheck the fresh client snapshot immediately before a destructive request, preserving the journal for later recovery.

## Tried and ruled out
A System Health warning alone does not stop Hygiene. Gating only the initial queue preview still leaves a race when connectivity fails between evaluation and removal. Resetting strikes during a network outage would discard useful evidence from before the outage.

## Watch out
A firewalled client is still connected. Legacy clients without a status capability need their existing behavior preserved; unknown or malformed modern responses must fail closed. Network recovery should resume the existing journal, not create duplicate replacement searches. This safeguard is client-specific and is not a universal Internet connectivity probe.

In the code: QBittorrent.GetItems, QBittorrentProxyV2.GetConnectionStatus, HygieneQueueStrikeService, HygieneRuleSafeguards, HygieneContentDecisionService, HygieneImportRescueService, and HygieneActionService.Continue. The outage-after-preview and recovery regression runs for Movies, TV, and Music.
