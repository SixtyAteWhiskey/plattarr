---
title: Keep recovery status filtering inclusive
author: SixtyAteWhiskey
date: 2026-10-29
branch: refactor/settings-route-loading
id: l-020fa9d5
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Core/Hygiene/HygieneRepositories.cs
  - src/NzbDrone.Core/Hygiene/HygieneActionService.cs
  - docs/refactoring-plan.md
---

## Decided
Filter the interrupted-action recovery read in SQL with status unequal to completed. That preserves recovery for every non-completed status, including future values, while avoiding completed-row materialization on each queue refresh.

## Tried and ruled out
Adding an ordinary status index was ruled out for this slice. SQLite still scans for the inequality; the existing composite index begins with client and download. An enum-like list of known unfinished statuses could use a status index, but would silently omit an unfamiliar status from recovery.

## Watch out
The query still scans the action table. The synthetic timing measures the read and filter, not whole queue-refresh time or database pages read. Profile larger action histories and state reads before claiming end-to-end scalability.

In the code: `HygieneActionRepository.FindIncomplete`, `HygieneQueueActionService.ResumeInterruptedActions`, `tools/benchmark_hygiene_interrupted_actions.py`; 100, 10,000, and 100,000 synthetic action rows with one unfinished per 1,000.
