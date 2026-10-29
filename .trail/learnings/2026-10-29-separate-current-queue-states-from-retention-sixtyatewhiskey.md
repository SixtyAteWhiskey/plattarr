---
title: Separate current queue states from retention cleanup
author: SixtyAteWhiskey
date: 2026-10-29
branch: refactor/settings-route-loading
id: l-434cc9f6
scope: repo
status: active
src: agent
touches:
  - docs/refactoring-plan.md
  - src/NzbDrone.Core.Test/QueueTests/HygieneQueueStateRepositoryFixture.cs
  - src/NzbDrone.Core.Test/QueueTests/HygieneQueueStrikeServiceFixture.cs
  - src/NzbDrone.Core/Hygiene/HygieneQueueStrikeService.cs
  - src/NzbDrone.Core/Hygiene/HygieneRepositories.cs
---

## Decided
Read strike states for the current client/download identities, then query expired states after refreshing queued items. This preserves a job that returns after 30 days and avoids loading all recent state history on every queue refresh. Keep the strict 30-day cutoff.

## Tried and ruled out
A new retention index was considered but deferred. With 1% expired rows in synthetic SQLite fixtures, splitting the read cut materialization substantially even though the expiry query still scans the table; an index would add a migration and write cost without measured need at this size.

## Watch out
The split raises query count from one to two for queues of at most 200 identities. The expiry scan, deletion writes, whole-refresh latency and memory, and PostgreSQL behavior remain unmeasured. Revisit an index if real retention histories make the scan material.

In the code: `HygieneQueueStrikeService.Observe`, `HygieneQueueStateRepository.FindExpired`, `tools/benchmark_hygiene_strike_states.py`; 20 identities, 1% expired, and 100 to 100,000 synthetic rows.
