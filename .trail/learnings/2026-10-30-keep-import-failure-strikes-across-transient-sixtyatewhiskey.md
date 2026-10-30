---
title: Keep import failure strikes across transient processing states
author: SixtyAteWhiskey
date: 2026-10-30
branch: refactor/settings-route-loading
id: l-75b45cac
scope: repo
status: active
src: agent
touches:
  - docs/hygiene.md
  - frontend/src/Activity/Hygiene/Hygiene.tsx
  - frontend/src/Activity/Hygiene/HygieneAttentionBadge.tsx
  - frontend/src/Activity/Hygiene/HygieneRules.tsx
  - src/NzbDrone.Core.Test/Download/DownloadClientTests/QBittorrentTests/QBittorrentFixture.cs
  - src/NzbDrone.Core.Test/QueueTests/HygieneQueueActionServiceFixture.cs
  - src/NzbDrone.Core.Test/QueueTests/HygieneQueueDecisionServiceFixture.cs
  - src/NzbDrone.Core.Test/QueueTests/HygieneStrikeCalculatorFixture.cs
  - src/NzbDrone.Core.Test/QueueTests/QueueServiceFixture.cs
  - src/NzbDrone.Core/Download/Clients/QBittorrent/QBittorrent.cs
---

## Decided
Treat the pre-processing import-pending snapshot as provisional for Hygiene strike observation because each refresh can temporarily mark a still-blocked import pending before the import attempt fails again. Use the processed snapshot to decide whether the failure persists or has recovered.

## Tried and ruled out
A final queue preview alone could not explain why the count stayed at one. Read-only production state showed the same blocked download returning to one strike on successive refreshes; tracing the event pipeline showed an intermediate pending snapshot before the repeated blocked outcome.

## Watch out
Observe a processed snapshot that is still pending because skipping pending states indefinitely would preserve stale strikes after recovery. Keep action eligibility tied to the current assessment, and use disposable fixtures rather than live cleanup or searches.

In the code: `TrackedDownloadRefreshedEvent`, `DownloadMonitoringService.Handle(DownloadsProcessedEvent)`, `QueueService.Handle`, and `QueueServiceFixture` distinguish pre-processing and post-processing snapshots.
