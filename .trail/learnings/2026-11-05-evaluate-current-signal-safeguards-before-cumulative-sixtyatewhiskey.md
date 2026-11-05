---
title: Evaluate current signal safeguards before cumulative cleanup
author: SixtyAteWhiskey
date: 2026-11-05
branch: codex/hygiene-queue-content-parity
id: l-f179a32c
scope: repo
status: active
src: agent
touches:
  - docs/hygiene.md
  - frontend/src/Activity/Hygiene/Hygiene.tsx
  - frontend/src/Activity/Hygiene/HygieneRules.tsx
  - src/NzbDrone.Core.Test/Download/DownloadClientTests/QBittorrentTests/QBittorrentFixture.cs
  - src/NzbDrone.Core.Test/QueueTests/HygieneQueueActionServiceFixture.cs
  - src/NzbDrone.Core.Test/QueueTests/HygieneQueueDecisionServiceFixture.cs
  - src/NzbDrone.Core.Test/QueueTests/HygieneQueueStrikeServiceFixture.cs
  - src/NzbDrone.Core/Configuration/ConfigService.cs
  - src/NzbDrone.Core/Configuration/IConfigService.cs
  - src/NzbDrone.Core/Download/Clients/QBittorrent/QBittorrent.cs
---

## Decided
A cumulative slow/stalled Hygiene rule must not act when the current signal's scoped rule holds the download. Check the current signal's ignore and safety conditions before promoting it to the cumulative reason. Recheck current client metadata before a prepared removal, because category, tags, or tracker can change after the queue snapshot.

## Tried and ruled out
Applying safeguards only after selecting the final cumulative rule was insufficient: an old cumulative strike count could bypass an ignore value on the current slow rule. This matters because cumulative strikes intentionally survive healthy intervals.

## Watch out
Strike observation and action decisions classify the same queue item separately. Keep their hold conditions aligned so an ignored item neither gains new strikes nor reaches removal through a different rule.

In the code: src/NzbDrone.Core/Hygiene/HygieneQueueDecisionService.cs, src/NzbDrone.Core/Hygiene/HygieneQueueStrikeService.cs, src/NzbDrone.Core/Hygiene/HygieneRuleSafeguards.cs, src/NzbDrone.Core/Hygiene/HygieneActionService.cs.
