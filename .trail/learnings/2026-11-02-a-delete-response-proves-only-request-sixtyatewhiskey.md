---
title: A delete response proves only request acceptance
author: SixtyAteWhiskey
date: 2026-11-02
branch: codex/qbittorrent-removal-evidence
id: l-5410d13b
scope: repo
status: active
src: agent
touches:
  - docs/hygiene.md
  - frontend/src/Activity/Hygiene/Hygiene.tsx
  - src/NzbDrone.Core.Test/Download/DownloadClientTests/QBittorrentTests/QBittorrentFixture.cs
  - src/NzbDrone.Core.Test/QueueTests/HygieneQueueActionServiceFixture.cs
  - src/NzbDrone.Core/Download/Clients/QBittorrent/QBittorrent.cs
  - src/NzbDrone.Core/Hygiene/HygieneActionService.cs
  - src/Radarr.Api.V3/Hygiene/HygieneActionController.cs
  - src/Radarr.Api.V3/openapi.json
---

## Decided
Record client removal and source-file deletion as separate Hygiene observations because qBittorrent's delete API returns before the application has checked either outcome. A job is observed absent only when it was listed before the request and absent immediately afterward. If still listed, keep the action at client removal for retry. A source path is observed absent only when a distinct content path inside the save directory existed before removal and was absent afterward.

## Tried and ruled out
Treating a successful delete call or completed Hygiene action as proof of file deletion was ruled out because the call has no filesystem observation, and older journal entries only retain the requested deletion policy.

## Watch out
Missing or inaccessible content paths must remain unverified because no before-and-after presence check can be made. An immediate present result may change later; an absent path does not prove every file was removed. Keep paths, raw responses, and exception text out of journal and API evidence.

In the code: `QBittorrent.RemoveItemWithEvidence`, `HygieneQueueActionService.Continue`, and migration 022 add fixed status observations; `docs/hygiene.md` explains their limits.
