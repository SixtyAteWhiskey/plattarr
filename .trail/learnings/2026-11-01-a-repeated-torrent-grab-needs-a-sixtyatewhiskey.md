---
title: A repeated torrent grab needs a new cleanup without another automatic search
author: SixtyAteWhiskey
date: 2026-11-01
branch: codex/hygiene-repeat-grab-fix
id: l-3106d727
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Core/Hygiene/HygieneActionService.cs
  - src/NzbDrone.Core/Hygiene/HygieneQueueDecisionService.cs
---

## Decided
Treat a new grab of the same torrent hash by the same download client after a completed Hygiene action began as a new occurrence. Permit a fresh eligible cleanup, but skip its replacement search so the automatic recovery cannot immediately grab the same hash again. Keep completed actions held when there is no newer grab, since a lingering client job is ambiguous.

## Tried and ruled out
Blindly retrying every completed action would repeatedly remove a client job that may never have disappeared. Blocking all later actions by client, hash, and reason prevents cleanup when a replacement search actually regrabs the same torrent. Using the action completion time as the boundary would miss a grab that the action's own replacement search initiated before the journal status changed to completed.

## Watch out
A new occurrence is supported by application grab history, not just a queue item with the same hash. The current rule and global gate still decide eligibility. A manually readded client job without a recorded grab stays in the manual recovery path.

In the code: `HygieneActionOccurrence.WasGrabbedAgain` compares `DownloadHistory.Date` to `HygieneAction.CreatedAt` and checks download client ID, download ID, and event type. `HygieneQueueActionService` sets `SearchForReplacement` false on the repeat action; `HygieneQueueDecisionService` reports whether the occurrence is held or actionable.
