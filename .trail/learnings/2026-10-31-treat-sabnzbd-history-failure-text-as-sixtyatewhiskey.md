---
title: Treat SABnzbd history failure text as untrusted queue data
author: Codex
date: 2026-10-31
branch: codex/queue-health-audit-20261031
id: l-9ae72a24
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Core.Test/Download/DownloadClientTests/SabnzbdTests/SabnzbdFixture.cs
  - src/NzbDrone.Core/Download/Clients/Sabnzbd/Sabnzbd.cs
  - src/NzbDrone.Core/Localization/Core/en.json
  - src/NzbDrone.Core/Queue/QueueService.cs
  - src/Radarr.Api.V3/Queue/QueueResource.cs
  - frontend/src/Activity/Queue/QueueStatus.tsx
---

## Decided
SABnzbd history failure text must be classified at the download-client boundary before it becomes a Queue message. Use fixed localized guidance for the known unpack warning and for other failed jobs; leave completed jobs without a failure message. This keeps useful next steps while avoiding publication of client-supplied paths or query values.

## Tried and ruled out
Passing the raw SABnzbd failure text through to the Queue was ruled out because the Queue API serializes it as `errorMessage`, and the warning tooltip displays it. A synthetic private path and query value proved that the old assignment would expose those details.

## Watch out
The exact SABnzbd unpack failure phrase still determines warning status; other failed jobs retain failed status. Keep that status behavior when changing the message. The Queue resource copies the item message without sanitizing it, so any new client message source needs the same boundary review.

In the code: `src/NzbDrone.Core/Download/Clients/Sabnzbd/Sabnzbd.cs` maps history items; `src/NzbDrone.Core/Queue/QueueService.cs` copies `DownloadClientItem.Message`; `src/Radarr.Api.V3/Queue/QueueResource.cs` serializes `errorMessage`; `frontend/src/Activity/Queue/QueueStatus.tsx` displays warnings. The disposable regression fixture is `src/NzbDrone.Core.Test/Download/DownloadClientTests/SabnzbdTests/SabnzbdFixture.cs`.
