---
title: Use the journal primary key for newest entries
author: SixtyAteWhiskey
date: 2026-10-29
branch: refactor/settings-route-loading
id: l-c0214df2
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Core.Test/QueueTests/HygieneActionRepositoryFixture.cs
  - src/NzbDrone.Core/Hygiene/HygieneActionService.cs
  - src/NzbDrone.Core/Hygiene/HygieneRepositories.cs
---

## Decided
Keep the Hygiene journal's newest-entry response bounded in SQL with descending ID and a limit. The journal ID is already the row primary key, so SQLite can traverse it in reverse and stop after the requested rows; this avoids a migration or an extra write-time index.

## Tried and ruled out
Reading the whole journal and sorting in memory is harmless with a small journal, but the synthetic 100,000-action fixture materialized every row for a 100-entry response and took about 84 ms. The bounded query materialized 100 and took about 0.06 ms on the same fixture.

## Watch out
This read serves the journal API, not queue refresh. The API also loads command history for replacement outcomes, and the synthetic SQLite timing does not prove end-to-end API latency or PostgreSQL behavior. Keep the existing 1-to-500 response limit and descending ID order.

In the code: `HygieneQueueActionService.Recent`, `HygieneActionRepository.FindRecent`, `tools/benchmark_hygiene_recent_actions.py`, `/api/v3/hygiene/action`.
